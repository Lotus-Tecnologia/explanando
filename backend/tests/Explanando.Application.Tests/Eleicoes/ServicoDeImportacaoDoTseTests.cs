using System.Runtime.CompilerServices;
using Explanando.Application.Eleicoes;
using Explanando.Application.Ingestao;
using Explanando.Domain.Eleicoes;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Tests.Eleicoes;

/// <summary>
/// A importação do TSE — a Fase 7 do plano de execução.
///
/// <para>O que se protege aqui: a ligação parlamentar ↔ candidatura só acontece por igualdade
/// e nunca no palpite; só entra doação que alimenta alguma coisa; e o CPF completo não passa da
/// importação. CPFs daqui são inventados.</para>
/// </summary>
public class ServicoDeImportacaoDoTseTests
{
    private static Parlamentar Deputado(string nomeParlamentar, string nomeCivil, string uf, int id = 1) =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = id,
            NomeCivil = nomeCivil,
            NomeParlamentar = nomeParlamentar,
            SiglaPartido = "XYZ",
            Uf = uf,
            Tipo = TipoParlamentar.DeputadoFederal,
        });

    private static CandidatoNaVotacao Candidato(long sequencial, string nomeCivil, string urna, string uf, long votos = 100) =>
        new()
        {
            Sequencial = sequencial,
            NomeCivil = nomeCivil,
            NomeDeUrna = urna,
            Uf = uf,
            CodigoDoCargo = 6,
            Cargo = "Deputado Federal",
            SiglaPartido = "XYZ",
            NumeroNaUrna = 1234,
            Situacao = "ELEITO POR QP",
            PorMunicipio = [new VotosDaFonte(1, "MACAPÁ", votos), new VotosDaFonte(2, "SANTANA", 0)],
        };

    private static ReceitaDaFonte Receita(long sequencial, long? candidato, string doador, string cpf, decimal valor = 1_000m) =>
        new()
        {
            Sequencial = sequencial,
            Destino = candidato is null ? DestinoDaDoacao.Partido : DestinoDaDoacao.Candidato,
            SequencialDoCandidato = candidato,
            NomeDoDestinatario = "DESTINATARIO",
            SiglaPartidoDoDestinatario = "XYZ",
            UfDoDestinatario = "AP",
            DocumentoDoDoador = cpf,
            NomeDoDoador = doador,
            Data = new DateOnly(2022, 9, 1),
            Valor = valor,
        };

    [Fact]
    public async Task Apagar_o_ano_e_gravar_de_novo_acontecem_numa_transacao_so()
    {
        // Auditoria de 2026-09-25: o ano era apagado, as candidaturas gravadas e as doações
        // entravam em lotes — sem transação. Uma falha no meio (o CDN do TSE recusa cliente
        // automatizado) deixava campanha com doações parciais, e a cobertura a declarava
        // disponível: REG_NET_001 concluía "nada encontrado" com dado faltando (ADR-0007).
        var gleisi = Deputado("Gleisi Hoffmann", "GLEISI HELENA HOFFMANN", "PR");
        var cenario = new Cenario(
            [gleisi],
            [Candidato(10, "GLEISI HELENA HOFFMANN", "GLEISI", "PR")],
            [Receita(1, 10, "FULANO", "52998224725")]);

        await cenario.Importar();

        cenario.Repositorio.Transacoes.ShouldBe(1);
        cenario.Repositorio.GravacoesForaDaTransacao.ShouldBeEmpty();
    }

    [Fact]
    public async Task Liga_pelo_nome_civil_quando_o_de_urna_e_outro()
    {
        // "GLEISI" na urna, nome parlamentar diferente: só o civil liga.
        var gleisi = Deputado("Gleisi Hoffmann", "GLEISI HELENA HOFFMANN", "PR");
        var cenario = new Cenario([gleisi], [Candidato(10, "GLEISI HELENA HOFFMANN", "GLEISI", "PR")], []);

        var resultado = await cenario.Importar();

        resultado.CandidaturasLigadas.ShouldBe(1);
        cenario.Repositorio.Candidaturas.ShouldHaveSingleItem().ParlamentarId.ShouldBe(gleisi.Id);
    }

    [Fact]
    public async Task O_nome_civil_vem_do_detalhe_da_camara_quando_falta()
    {
        // A lista da Câmara grava o nome parlamentar nos dois campos.
        var fraga = Deputado("Alberto Fraga", "Alberto Fraga", "DF", id: 77);
        var cenario = new Cenario([fraga], [Candidato(11, "ALBERTO FRAGA DE SOUZA", "FRAGA", "DF")], [])
        {
            NomesCivisDaCamara = { [77] = "Alberto Fraga de Souza" },
        };

        var resultado = await cenario.Importar();

        resultado.NomesCivisCompletados.ShouldBe(1);
        fraga.NomeCivil.ShouldBe("Alberto Fraga de Souza");
        resultado.CandidaturasLigadas.ShouldBe(1);
    }

    [Fact]
    public async Task Acento_e_caixa_nao_impedem_a_ligacao()
    {
        var acacio = Deputado("Acácio Favacho", "Acácio da Silva Favacho Neto", "AP");
        var cenario = new Cenario([acacio], [Candidato(12, "ACACIO DA SILVA FAVACHO NETO", "ACÁCIO FAVACHO", "AP")], []);

        (await cenario.Importar()).CandidaturasLigadas.ShouldBe(1);
    }

    [Fact]
    public async Task Homonimo_na_mesma_uf_nao_liga_ninguem()
    {
        var joao = Deputado("João", "JOAO DA SILVA", "SP");
        var cenario = new Cenario(
            [joao],
            [Candidato(20, "JOAO DA SILVA", "JOAO", "SP"), Candidato(21, "JOAO DA SILVA", "JOAO DO POVO", "SP")],
            []);

        var resultado = await cenario.Importar();

        resultado.CandidaturasLigadas.ShouldBe(0);
        resultado.Ambiguos.ShouldBe(1);
        resultado.SemCandidatura.ShouldContain("João (SP)");
    }

    [Fact]
    public async Task Mesmo_nome_em_outra_uf_nao_liga()
    {
        var cenario = new Cenario([Deputado("Fulano", "FULANO DE TAL", "RJ")], [Candidato(30, "FULANO DE TAL", "FULANO", "SP")], []);

        (await cenario.Importar()).CandidaturasLigadas.ShouldBe(0);
    }

    [Fact]
    public async Task Grava_os_votos_por_municipio_sem_os_municipios_zerados()
    {
        var cenario = new Cenario([Deputado("Acácio Favacho", "ACACIO FAVACHO NETO", "AP")], [Candidato(12, "ACACIO FAVACHO NETO", "ACACIO", "AP", votos: 5_000)], []);

        await cenario.Importar();

        var votos = cenario.Repositorio.Votos.ShouldHaveSingleItem();
        votos.NomeDoMunicipio.ShouldBe("MACAPÁ");
        votos.Votos.ShouldBe(5_000);
        cenario.Repositorio.Candidaturas.Single().TotalDeVotos.ShouldBe(5_000);
    }

    [Fact]
    public async Task So_entra_doacao_ao_parlamentar_ou_de_quem_e_socio()
    {
        var acacio = Deputado("Acácio Favacho", "ACACIO FAVACHO NETO", "AP");

        var cenario = new Cenario(
            [acacio],
            [Candidato(12, "ACACIO FAVACHO NETO", "ACACIO", "AP")],
            [
                Receita(1, candidato: 12, "MARIA DOADORA", "11122233344"),       // ao parlamentar
                Receita(2, candidato: 99, "OUTRA PESSOA", "55566677788"),        // a outro, não é sócio
                Receita(3, candidato: 99, "JOSÉ DA SILVA", "12345678901"),        // a outro, mas é sócio
                Receita(4, candidato: null, "JOSE DA SILVA", "12345678901"),      // a partido, é sócio
            ])
        {
            Socios = { new ChaveDeSocio("JOSE DA SILVA", "456789") },
        };

        var resultado = await cenario.Importar();

        resultado.DoacoesParaParlamentares.ShouldBe(1);
        resultado.DoacoesDeSocios.ShouldBe(2);
        cenario.Repositorio.Doacoes.Select(d => d.SequencialDaReceita).ShouldBe([1L, 3L, 4L], ignoreOrder: true);
        cenario.Repositorio.Doacoes.Single(d => d.SequencialDaReceita == 1).ParlamentarId.ShouldBe(acacio.Id);
    }

    [Fact]
    public async Task O_cpf_completo_nao_chega_ao_repositorio()
    {
        var cenario = new Cenario(
            [Deputado("Acácio Favacho", "ACACIO FAVACHO NETO", "AP")],
            [Candidato(12, "ACACIO FAVACHO NETO", "ACACIO", "AP")],
            [Receita(1, candidato: 12, "MARIA DOADORA", "11122233344")]);

        await cenario.Importar();

        var doacao = cenario.Repositorio.Doacoes.ShouldHaveSingleItem();
        doacao.CpfDoDoador.Mascarado.ShouldBe("***222333**");
        System.Text.Json.JsonSerializer.Serialize(doacao).ShouldNotContain("11122233344");
    }

    [Fact]
    public async Task A_carga_limpa_o_ano_antes_de_gravar()
    {
        var cenario = new Cenario([], [], []);

        await cenario.Importar();

        cenario.Repositorio.AnosLimpos.ShouldBe([2022]);
    }

    private sealed class Cenario(
        IReadOnlyList<Parlamentar> parlamentares,
        IReadOnlyList<CandidatoNaVotacao> candidatos,
        IReadOnlyList<ReceitaDaFonte> receitas)
    {
        public RepositorioFalso Repositorio { get; } = new(parlamentares);

        public Dictionary<int, string> NomesCivisDaCamara { get; } = [];

        public HashSet<ChaveDeSocio> Socios { get; } = [];

        public Task<ResultadoDaImportacaoDoTse> Importar()
        {
            foreach (var socio in Socios)
            {
                Repositorio.Socios.Add(socio);
            }

            return new ServicoDeImportacaoDoTse(
                new FonteFalsa(candidatos, receitas), Repositorio, new CamaraFalsa(NomesCivisDaCamara))
                .ImportarAsync(2022);
        }
    }

    private sealed class FonteFalsa(
        IReadOnlyList<CandidatoNaVotacao> candidatos,
        IReadOnlyList<ReceitaDaFonte> receitas) : IFonteDoTse
    {
        public async IAsyncEnumerable<CandidatoNaVotacao> LerVotacaoAsync(
            int ano, [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            foreach (var candidato in candidatos)
            {
                await Task.Yield();
                yield return candidato;
            }
        }

        public async IAsyncEnumerable<ReceitaDaFonte> LerReceitasAsync(
            int ano, [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            foreach (var receita in receitas)
            {
                await Task.Yield();
                yield return receita;
            }
        }
    }

    private sealed class CamaraFalsa(Dictionary<int, string> nomesCivis) : IFonteDeParlamentares
    {
        public async IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<DeputadoDaFonte?> ObterDetalheAsync(int idExterno, CancellationToken cancelamento = default) =>
            Task.FromResult(nomesCivis.TryGetValue(idExterno, out var nome)
                ? new DeputadoDaFonte { IdExterno = idExterno, NomeCivil = nome, NomeParlamentar = "x", SiglaPartido = "XYZ", Uf = "DF" }
                : null);

        public async IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
            int idExterno, int legislatura, [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class RepositorioFalso(IReadOnlyList<Parlamentar> parlamentares) : IRepositorioDoTse
    {
        public List<CandidaturaDoParlamentar> Candidaturas { get; } = [];
        public List<VotosNoMunicipio> Votos { get; } = [];
        public List<DoacaoEleitoral> Doacoes { get; } = [];
        public HashSet<ChaveDeSocio> Socios { get; } = [];
        public List<int> AnosLimpos { get; } = [];

        public Task<IReadOnlyList<Parlamentar>> ObterParlamentaresAsync(CancellationToken cancelamento = default) =>
            Task.FromResult(parlamentares);

        public Task<IReadOnlySet<ChaveDeSocio>> ObterChavesDeSociosAsync(CancellationToken cancelamento = default) =>
            Task.FromResult<IReadOnlySet<ChaveDeSocio>>(Socios);

        public int Transacoes { get; private set; }
        public List<string> GravacoesForaDaTransacao { get; } = [];
        private bool _dentro;

        public async Task EmUmaTransacaoAsync(
            Func<CancellationToken, Task> trabalho,
            CancellationToken cancelamento = default)
        {
            Transacoes++;
            _dentro = true;
            try
            {
                await trabalho(cancelamento);
            }
            finally
            {
                _dentro = false;
            }
        }

        private void Anotar(string operacao)
        {
            if (!_dentro)
            {
                GravacoesForaDaTransacao.Add(operacao);
            }
        }

        public Task LimparAnoAsync(int ano, CancellationToken cancelamento = default)
        {
            Anotar("limpar");
            AnosLimpos.Add(ano);
            return Task.CompletedTask;
        }

        public void Adicionar(CandidaturaDoParlamentar candidatura)
        {
            Anotar("candidatura");
            Candidaturas.Add(candidatura);
        }

        public void Adicionar(VotosNoMunicipio votos)
        {
            Anotar("votos");
            Votos.Add(votos);
        }

        public void Adicionar(DoacaoEleitoral doacao)
        {
            Anotar("doacao");
            Doacoes.Add(doacao);
        }

        // O salvar do nome civil (antes da carga) é legítimo fora: não apaga nada do ano.
        public Task SalvarAsync(CancellationToken cancelamento = default) => Task.CompletedTask;
    }
}
