using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Eleicoes;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Eleicoes;

/// <summary>
/// Carrega a eleição de um ano a partir dos arquivos do TSE — a Fase 7 do plano de execução.
///
/// <para><b>Três passos, nesta ordem:</b></para>
/// <list type="number">
/// <item><b>Nome civil.</b> A lista da Câmara só traz o nome parlamentar, e ele raramente é o de
/// urna ("GLEISI", "FRAGA"). O civil, que o TSE também publica, vem do detalhe de cada
/// deputado.</item>
/// <item><b>Candidatura.</b> Nome civil + UF + cargo, normalizados e comparados por igualdade.
/// Se dois candidatos casam com a mesma chave, nenhum é ligado — homônimo não se resolve no
/// palpite.</item>
/// <item><b>Doações.</b> Só pessoa física, e só as que alimentam alguma coisa: as que foram
/// para um parlamentar da base, e as que vieram de quem é sócio de um fornecedor.</item>
/// </list>
/// </summary>
public sealed class ServicoDeImportacaoDoTse(
    IFonteDoTse fonte,
    IRepositorioDoTse repositorio,
    IFonteDeParlamentares camara)
{
    private const int CargoDeputadoFederal = 6;
    private const int CargoSenador = 5;

    /// <summary>Quantas linhas acumular antes de gravar. Dezenas de milhares no total.</summary>
    private const int Lote = 5_000;

    public async Task<ResultadoDaImportacaoDoTse> ImportarAsync(
        int ano,
        CancellationToken cancelamento = default)
    {
        var parlamentares = await repositorio.ObterParlamentaresAsync(cancelamento).ConfigureAwait(false);

        var completados = await CompletarNomesCivisAsync(parlamentares, cancelamento).ConfigureAwait(false);

        await repositorio.LimparAnoAsync(ano, cancelamento).ConfigureAwait(false);

        var ligacao = await LigarCandidaturasAsync(ano, parlamentares, cancelamento).ConfigureAwait(false);

        var socios = await repositorio.ObterChavesDeSociosAsync(cancelamento).ConfigureAwait(false);

        var (lidas, paraParlamentares, deSocios) =
            await ImportarDoacoesAsync(ano, ligacao.PorSequencial, socios, cancelamento).ConfigureAwait(false);

        return new ResultadoDaImportacaoDoTse
        {
            Parlamentares = parlamentares.Count,
            NomesCivisCompletados = completados,
            CandidaturasLigadas = ligacao.PorSequencial.Count,
            Ambiguos = ligacao.Ambiguos,
            SemCandidatura = ligacao.SemCandidatura,
            MunicipiosComVoto = ligacao.Municipios,
            ReceitasDePessoaFisicaLidas = lidas,
            DoacoesParaParlamentares = paraParlamentares,
            DoacoesDeSocios = deSocios,
        };
    }

    /// <summary>
    /// Busca o nome civil de quem ainda está com o parlamentar nos dois campos. Só deputados:
    /// o cadastro do Senado já grava o nome completo.
    /// </summary>
    private async Task<int> CompletarNomesCivisAsync(
        IReadOnlyList<Parlamentar> parlamentares,
        CancellationToken cancelamento)
    {
        var completados = 0;

        foreach (var parlamentar in parlamentares.Where(p =>
            p.Tipo == TipoParlamentar.DeputadoFederal
            && NomeNormalizado.De(p.NomeCivil) == NomeNormalizado.De(p.NomeParlamentar)))
        {
            var detalhe = await camara.ObterDetalheAsync(parlamentar.IdExterno, cancelamento)
                .ConfigureAwait(false);

            if (detalhe is { NomeCivil.Length: > 0 }
                && NomeNormalizado.De(detalhe.NomeCivil) != NomeNormalizado.De(parlamentar.NomeCivil))
            {
                parlamentar.CompletarNomeCivil(detalhe.NomeCivil);
                completados++;
            }
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return completados;
    }

    private sealed record Ligacao(
        Dictionary<long, CandidaturaDoParlamentar> PorSequencial,
        int Ambiguos,
        IReadOnlyList<string> SemCandidatura,
        int Municipios);

    private async Task<Ligacao> LigarCandidaturasAsync(
        int ano,
        IReadOnlyList<Parlamentar> parlamentares,
        CancellationToken cancelamento)
    {
        // Duas chaves por parlamentar, as duas por igualdade: o nome civil, que é o certo, e o
        // nome parlamentar contra o de urna, para quem a Câmara não devolveu o civil.
        var procurados = new Dictionary<(string Nome, string Uf, int Cargo), List<Parlamentar>>();

        foreach (var parlamentar in parlamentares)
        {
            var cargo = parlamentar.Tipo == TipoParlamentar.Senador ? CargoSenador : CargoDeputadoFederal;

            foreach (var nome in new[] { parlamentar.NomeCivil, parlamentar.NomeParlamentar }.Distinct())
            {
                var chave = (NomeNormalizado.De(nome), parlamentar.Uf, cargo);

                if (!procurados.TryGetValue(chave, out var lista))
                {
                    procurados[chave] = lista = [];
                }

                if (!lista.Contains(parlamentar))
                {
                    lista.Add(parlamentar);
                }
            }
        }

        // Quem casou com quem, antes de gravar: um parlamentar com dois candidatos, ou um
        // candidato com dois parlamentares, é ambíguo e fica de fora.
        var candidatosPorParlamentar = new Dictionary<Guid, List<CandidatoNaVotacao>>();

        await foreach (var candidato in fonte.LerVotacaoAsync(ano, cancelamento).ConfigureAwait(false))
        {
            var encontrados = new HashSet<Parlamentar>();

            foreach (var nome in new[] { candidato.NomeCivil, candidato.NomeDeUrna })
            {
                if (procurados.TryGetValue((NomeNormalizado.De(nome), candidato.Uf, candidato.CodigoDoCargo), out var lista))
                {
                    encontrados.UnionWith(lista);
                }
            }

            foreach (var parlamentar in encontrados)
            {
                if (!candidatosPorParlamentar.TryGetValue(parlamentar.Id, out var lista))
                {
                    candidatosPorParlamentar[parlamentar.Id] = lista = [];
                }

                lista.Add(candidato);
            }
        }

        var ligados = new Dictionary<long, CandidaturaDoParlamentar>();
        var usados = candidatosPorParlamentar.Values
            .Where(lista => lista.Count == 1)
            .GroupBy(lista => lista[0].Sequencial)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToHashSet();

        var ambiguos = 0;
        var municipios = 0;
        var pendentes = 0;

        foreach (var parlamentar in parlamentares)
        {
            if (!candidatosPorParlamentar.TryGetValue(parlamentar.Id, out var lista))
            {
                continue;
            }

            if (lista.Count != 1 || usados.Contains(lista[0].Sequencial))
            {
                ambiguos++;
                continue;
            }

            var candidato = lista[0];
            var candidatura = CandidaturaDoParlamentar.Criar(
                parlamentar.Id,
                ano,
                candidato.Sequencial,
                candidato.NumeroNaUrna,
                candidato.NomeDeUrna,
                candidato.Cargo,
                candidato.Uf,
                candidato.SiglaPartido,
                candidato.Situacao,
                candidato.PorMunicipio.Sum(m => m.Votos));

            repositorio.Adicionar(candidatura);
            ligados[candidato.Sequencial] = candidatura;

            foreach (var municipio in candidato.PorMunicipio.Where(m => m.Votos > 0))
            {
                repositorio.Adicionar(VotosNoMunicipio.Criar(
                    candidatura, municipio.CodigoDoMunicipio, municipio.NomeDoMunicipio, candidato.Uf, municipio.Votos));
                municipios++;
                pendentes++;
            }

            if (pendentes >= Lote)
            {
                await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);
                pendentes = 0;
            }
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        var semCandidatura = parlamentares
            .Where(p => !ligados.Values.Any(c => c.ParlamentarId == p.Id))
            .Select(p => $"{p.NomeParlamentar} ({p.Uf})")
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        return new Ligacao(ligados, ambiguos, semCandidatura, municipios);
    }

    private async Task<(int Lidas, int ParaParlamentares, int DeSocios)> ImportarDoacoesAsync(
        int ano,
        Dictionary<long, CandidaturaDoParlamentar> ligadas,
        IReadOnlySet<ChaveDeSocio> socios,
        CancellationToken cancelamento)
    {
        var lidas = 0;
        var paraParlamentares = 0;
        var deSocios = 0;
        var pendentes = 0;

        await foreach (var receita in fonte.LerReceitasAsync(ano, cancelamento).ConfigureAwait(false))
        {
            lidas++;

            var cpf = CpfParcial.DeCpfCompleto(receita.DocumentoDoDoador);

            if (!cpf.TemDigitos)
            {
                continue;
            }

            CandidaturaDoParlamentar? candidatura = null;

            if (receita.Destino == DestinoDaDoacao.Candidato
                && receita.SequencialDoCandidato is { } sequencial)
            {
                ligadas.TryGetValue(sequencial, out candidatura);
            }

            var ehDeSocio = socios.Contains(
                new ChaveDeSocio(NomeNormalizado.De(receita.NomeDoDoador), cpf.DigitosVisiveis));

            if (candidatura is null && !ehDeSocio)
            {
                continue;
            }

            repositorio.Adicionar(DoacaoEleitoral.Criar(
                ano,
                receita.Sequencial,
                receita.Destino,
                receita.SequencialDoCandidato,
                candidatura?.ParlamentarId,
                receita.NomeDoDoador,
                receita.DocumentoDoDoador,
                receita.NomeDoDestinatario,
                receita.SiglaPartidoDoDestinatario,
                receita.UfDoDestinatario,
                receita.CargoDoDestinatario,
                receita.Data,
                receita.Valor));

            if (candidatura is not null)
            {
                paraParlamentares++;
            }

            if (ehDeSocio)
            {
                deSocios++;
            }

            if (++pendentes >= Lote)
            {
                await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);
                pendentes = 0;
            }
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return (lidas, paraParlamentares, deSocios);
    }
}
