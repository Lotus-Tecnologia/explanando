using Explanando.Application.Consultas;
using Explanando.Domain.Comum;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Rede;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// O mapa de fornecedores compartilhados sobre EF Core.
///
/// <para><b>São três consultas, e a ordem entre elas é o ponto.</b> A primeira agrupa as
/// 711 mil despesas por CNPJ no banco e devolve só os que passam do piso de valor — a
/// agregação acontece onde os dados estão. Só então a segunda pergunta quem são os
/// parlamentares de cada um dos ~600 sobreviventes, e a terceira busca os cadastros.</para>
///
/// <para>A alternativa — trazer as despesas e agrupar em memória — carregaria centenas de
/// megabytes para produzir um desenho de seiscentas linhas.</para>
/// </summary>
public sealed class ConsultasDaRedeEfCore(ExplanandoDbContext contexto) : IConsultasDaRede
{
    public async Task<MapaDaRede> ObterMapaAsync(CancellationToken cancelamento = default)
    {
        var candidatos = await AgruparPorFornecedorAsync(cancelamento).ConfigureAwait(false);

        // Os dois numeros que dao escala ao corte. Sao contados sobre o mesmo agrupamento,
        // e nao sobre uma consulta separada, para nao poderem discordar do que foi filtrado.
        var fornecedoresNaBase = candidatos.Count;
        var compartilhados = candidatos.Count(
            candidato => candidato.Parlamentares >= CriteriosDaRede.ParlamentaresMinimos);

        var maior = candidatos.OrderByDescending(candidato => candidato.Parlamentares).FirstOrDefault();

        var doMapa = candidatos
            .Where(candidato => CriteriosDaRede.Liga(candidato.Parlamentares, candidato.Valor))
            .ToList();

        if (doMapa.Count == 0)
        {
            return new MapaDaRede
            {
                Parlamentares = [],
                Ligacoes = [],
                Pares = [],
                ParesComUmFornecedorSo = 0,
                FornecedoresNaBase = fornecedoresNaBase,
                FornecedoresCompartilhados = compartilhados,
                MaiorFornecedorCompartilhado = maior?.Nome,
                ParlamentaresAtendidosPeloMaior = maior?.Parlamentares ?? 0,
            };
        }

        var documentos = doMapa.Select(candidato => candidato.Documento).ToList();

        // Quem cada fornecedor atende. Distinct no banco: uma agencia com quatrocentas
        // notas para o mesmo gabinete e uma linha, nao quatrocentas.
        var pares = await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => documentos.Contains(despesa.DocumentoFornecedor))
            .Where(despesa => despesa.ValorLiquido >= 0m)
            .Select(despesa => new
            {
                Documento = despesa.DocumentoFornecedor,
                despesa.ParlamentarId,
            })
            .Distinct()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porFornecedor = pares
            .GroupBy(par => par.Documento.Numero, StringComparer.Ordinal)
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(par => par.ParlamentarId).Distinct().ToList(),
                StringComparer.Ordinal);

        var cadastros = await contexto.Fornecedores
            .AsNoTracking()
            .Where(fornecedor => documentos.Contains(fornecedor.Documento))
            .Select(fornecedor => new
            {
                fornecedor.Documento,
                fornecedor.CnaePrincipal,
                fornecedor.DataInicioAtividade,
                fornecedor.Endereco,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var cadastroPorDocumento = cadastros.ToDictionary(
            cadastro => cadastro.Documento.Numero,
            StringComparer.Ordinal);

        var ligacoes = new List<LigacaoDaRede>(doMapa.Count);

        foreach (var candidato in doMapa.OrderByDescending(candidato => candidato.Valor))
        {
            if (!porFornecedor.TryGetValue(candidato.Documento.Numero, out var envolvidos))
            {
                continue;
            }

            // A contagem do agrupamento e a lista precisam concordar. Quando nao concordam,
            // vale a LISTA — e o criterio e reaplicado sobre ela, porque e ela que sera
            // desenhada. Um mapa nunca deve mostrar uma aresta que o criterio recusa.
            if (!CriteriosDaRede.Liga(envolvidos.Count, candidato.Valor))
            {
                continue;
            }

            cadastroPorDocumento.TryGetValue(candidato.Documento.Numero, out var cadastro);

            ligacoes.Add(new LigacaoDaRede
            {
                // Formatado, nunca Numero: o CPF de fornecedor pessoa física chega inteiro da
                // Câmara, e o Formatado é o único lugar que o mascara. Com Numero, os 11
                // dígitos iam para o JSON público (auditoria de 2026-09-25).
                Documento = candidato.Documento.Formatado,
                Fornecedor = candidato.Nome,
                Parlamentares = envolvidos,
                Valor = candidato.Valor,
                Notas = candidato.Notas,
                CnaePrincipal = cadastro?.CnaePrincipal,
                Municipio = cadastro?.Endereco?.Municipio,
                Uf = cadastro?.Endereco?.Uf,
                AbertaEm = cadastro?.DataInicioAtividade,
            });
        }

        var parlamentares = await MontarParlamentaresAsync(ligacoes, cancelamento).ConfigureAwait(false);
        var (paresDensos, soltos) = MontarPares(ligacoes, parlamentares);

        return new MapaDaRede
        {
            Parlamentares = parlamentares,
            Ligacoes = ligacoes,
            Pares = paresDensos,
            ParesComUmFornecedorSo = soltos,
            FornecedoresNaBase = fornecedoresNaBase,
            FornecedoresCompartilhados = compartilhados,
            MaiorFornecedorCompartilhado = maior?.Nome,
            ParlamentaresAtendidosPeloMaior = maior?.Parlamentares ?? 0,
        };
    }

    public async Task<IReadOnlyList<LigacaoDaRede>> ObterLigacoesAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        var mapa = await ObterMapaAsync(cancelamento).ConfigureAwait(false);

        return [.. mapa.Ligacoes.Where(ligacao => ligacao.Parlamentares.Contains(parlamentarId))];
    }

    /// <summary>
    /// O agrupamento por CNPJ, feito no banco.
    ///
    /// <para>Estornos ficam de fora — um valor negativo é a anulação de uma nota, e
    /// somá-lo produziria um total que não corresponde a dinheiro que saiu. É a mesma
    /// decisão que a <c>REG_NET_004</c> toma, pelo mesmo motivo.</para>
    /// </summary>
    private async Task<List<CandidatoAFornecedor>> AgruparPorFornecedorAsync(
        CancellationToken cancelamento)
    {
        return await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ValorLiquido >= 0m)
            .GroupBy(despesa => despesa.DocumentoFornecedor)
            .Select(grupo => new CandidatoAFornecedor
            {
                Documento = grupo.Key,
                Nome = grupo.Max(despesa => despesa.NomeFornecedor)!,
                Parlamentares = grupo.Select(despesa => despesa.ParlamentarId).Distinct().Count(),
                Valor = grupo.Sum(despesa => despesa.ValorLiquido),
                Notas = grupo.Count(),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Os nós do mapa, com grau e alcance.
    ///
    /// <para><b>O valor somado a cada pessoa é o valor cheio de cada fornecedor que ela
    /// divide, e não um rateio.</b> Ratear por cabeça produziria um número que ninguém
    /// gastou. O rótulo na tela diz exatamente isso — "movimentado pelos fornecedores que
    /// ele divide" — e é por isso que o campo se chama <c>ValorNoMapa</c> e não
    /// <c>Gasto</c>.</para>
    /// </summary>
    private async Task<List<ParlamentarNaRede>> MontarParlamentaresAsync(
        List<LigacaoDaRede> ligacoes,
        CancellationToken cancelamento)
    {
        var grau = new Dictionary<Guid, int>();
        var valor = new Dictionary<Guid, decimal>();
        var vizinhos = new Dictionary<Guid, HashSet<Guid>>();

        foreach (var ligacao in ligacoes)
        {
            foreach (var id in ligacao.Parlamentares)
            {
                grau[id] = grau.GetValueOrDefault(id) + 1;
                valor[id] = valor.GetValueOrDefault(id) + ligacao.Valor;

                if (!vizinhos.TryGetValue(id, out var conjunto))
                {
                    conjunto = [];
                    vizinhos[id] = conjunto;
                }

                foreach (var outro in ligacao.Parlamentares)
                {
                    if (outro != id)
                    {
                        conjunto.Add(outro);
                    }
                }
            }
        }

        var ids = grau.Keys.ToList();

        var pessoas = await contexto.Parlamentares
            .AsNoTracking()
            .Where(parlamentar => ids.Contains(parlamentar.Id))
            .Select(parlamentar => new
            {
                parlamentar.Id,
                parlamentar.NomeParlamentar,
                parlamentar.SiglaPartido,
                parlamentar.Uf,
                parlamentar.Tipo,
                parlamentar.FotoUrl,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return
        [
            .. pessoas
                .Select(pessoa => new ParlamentarNaRede
                {
                    Id = pessoa.Id,
                    Nome = pessoa.NomeParlamentar,
                    Partido = pessoa.SiglaPartido,
                    Uf = pessoa.Uf,
                    Casa = pessoa.Tipo == TipoParlamentar.Senador ? "Senador" : "Deputado",
                    ChaveDaCasa = pessoa.Tipo == TipoParlamentar.Senador ? "senado" : "camara",
                    FotoUrl = pessoa.FotoUrl,
                    Ligacoes = grau.GetValueOrDefault(pessoa.Id),
                    Vizinhos = vizinhos.TryGetValue(pessoa.Id, out var conjunto) ? conjunto.Count : 0,
                    ValorNoMapa = valor.GetValueOrDefault(pessoa.Id),
                })
                .OrderByDescending(pessoa => pessoa.Ligacoes)
                .ThenByDescending(pessoa => pessoa.ValorNoMapa),
        ];
    }

    /// <summary>
    /// Os pares densos, e a contagem dos que ficaram de fora.
    ///
    /// <para>Um fornecedor que atende cinco gabinetes produz dez pares. A conta é feita
    /// aqui, em memória, e não no banco: são 621 arestas de no máximo cinco pontas, e
    /// pedir ao Postgres um produto cartesiano por grupo custaria mais do que percorrer
    /// isso duas vezes.</para>
    ///
    /// <para><b>O par guarda a soma dos fornecedores que os dois dividem</b> — e o nome do
    /// campo é <c>Valor</c>, não <c>Gasto</c>, porque não é o gasto de nenhum dos dois:
    /// é o tamanho do que passa pelas empresas em comum.</para>
    /// </summary>
    private static (List<ParDaRede> Pares, int Soltos) MontarPares(
        List<LigacaoDaRede> ligacoes,
        List<ParlamentarNaRede> parlamentares)
    {
        var contagem = new Dictionary<(Guid, Guid), int>();
        var valor = new Dictionary<(Guid, Guid), decimal>();

        foreach (var ligacao in ligacoes)
        {
            var ordenados = ligacao.Parlamentares.Order().ToList();

            for (var i = 0; i < ordenados.Count; i++)
            {
                for (var j = i + 1; j < ordenados.Count; j++)
                {
                    var chave = (ordenados[i], ordenados[j]);

                    contagem[chave] = contagem.GetValueOrDefault(chave) + 1;
                    valor[chave] = valor.GetValueOrDefault(chave) + ligacao.Valor;
                }
            }
        }

        var porId = parlamentares.ToDictionary(pessoa => pessoa.Id);

        var densos = new List<ParDaRede>();
        var soltos = 0;

        foreach (var (chave, fornecedores) in contagem)
        {
            if (fornecedores < CriteriosDaRede.FornecedoresParaParDenso)
            {
                soltos++;
                continue;
            }

            if (!porId.TryGetValue(chave.Item1, out var um) || !porId.TryGetValue(chave.Item2, out var outro))
            {
                continue;
            }

            densos.Add(new ParDaRede
            {
                De = chave.Item1,
                Para = chave.Item2,
                Fornecedores = fornecedores,
                Valor = valor[chave],
                MesmaUf = string.Equals(um.Uf, outro.Uf, StringComparison.Ordinal),
                MesmoPartido = string.Equals(um.Partido, outro.Partido, StringComparison.Ordinal),
            });
        }

        densos.Sort((a, b) =>
            b.Fornecedores != a.Fornecedores
                ? b.Fornecedores.CompareTo(a.Fornecedores)
                : b.Valor.CompareTo(a.Valor));

        return (densos, soltos);
    }

    private sealed record CandidatoAFornecedor
    {
        public required CpfCnpj Documento { get; init; }
        public required string Nome { get; init; }
        public required int Parlamentares { get; init; }
        public required decimal Valor { get; init; }
        public required int Notas { get; init; }
    }
}
