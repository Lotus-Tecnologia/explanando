using System.Runtime.CompilerServices;
using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Tests.Ingestao;

public sealed class ServicoDeIngestaoTests
{
    private const int Legislatura = 57;

    private static readonly DeputadoDaFonte Deputado = new()
    {
        IdExterno = 204379,
        NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
        NomeParlamentar = "Acácio Favacho",
        SiglaPartido = "MDB",
        Uf = "AP",
        EmExercicio = true,
    };

    private static DespesaDaFonte Despesa(string codigo, string? cnpj = "36826246000164") => new()
    {
        Ano = 2026,
        Mes = 7,
        TipoDespesa = "COMBUSTÍVEIS E LUBRIFICANTES.",
        CodigoDocumento = codigo,
        NumeroDocumento = "8887",
        DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
        ValorDocumento = 385.92m,
        ValorLiquido = 385.92m,
        NomeFornecedor = "Auto Posto Bom Tempo Ltda",
        CnpjCpfFornecedor = cnpj,
        CodigoLote = 2253045,
    };

    // ------------------------------------------------------------------
    //  Caminho feliz
    // ------------------------------------------------------------------

    [Fact]
    public async Task Grava_as_despesas_e_cria_o_parlamentar()
    {
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("2")]),
            new CadastroFalso(),
            repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(2);
        resultado.TemAlerta.ShouldBeFalse();
        repositorio.Parlamentares.Count.ShouldBe(1);
        repositorio.Despesas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Cria_o_fornecedor_uma_vez_so_para_varias_despesas()
    {
        // Um deputado repete os mesmos fornecedores dezenas de vezes. Consultar a fonte
        // a cada nota castigaria um servico gratuito sem necessidade.
        var cadastro = new CadastroFalso();
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("2"), Despesa("3")]),
            cadastro,
            repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.FornecedoresNovos.ShouldBe(1);
        cadastro.Consultas.Count.ShouldBe(1, "as outras duas vieram do cache da execucao");
        repositorio.Fornecedores.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Grava_o_quadro_societario_do_fornecedor()
    {
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1")]), new CadastroFalso(), repositorio);

        await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        repositorio.Sociedades.Count.ShouldBe(1);
        repositorio.Sociedades[0].Cpf.DigitosVisiveis.ShouldBe("456789");
    }

    // ------------------------------------------------------------------
    //  Idempotencia — a coleta roda de novo sobre os mesmos meses
    // ------------------------------------------------------------------

    [Fact]
    public async Task Nao_regrava_despesa_ja_conhecida()
    {
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("2")]), new CadastroFalso(), repositorio);

        await servico.IngerirDeputadoAsync(Deputado, Legislatura);
        var segunda = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        segunda.DespesasNovas.ShouldBe(0);
        segunda.DespesasJaConhecidas.ShouldBe(2);
        repositorio.Despesas.Count.ShouldBe(2, "nada foi duplicado");
    }

    [Fact]
    public async Task Despesa_repetida_dentro_do_mesmo_lote_conta_uma_vez_so()
    {
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("1")]), new CadastroFalso(), repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(1);
        resultado.DespesasJaConhecidas.ShouldBe(1);
    }

    // ------------------------------------------------------------------
    //  Dado sujo — 2 em 100 despesas reais
    // ------------------------------------------------------------------

    [Fact]
    public async Task Despesa_com_cnpj_sentinela_e_gravada_sem_fornecedor()
    {
        // Descartar essas despesas esconderia gasto publico, que e' o oposto do
        // proposito do produto.
        var cadastro = new CadastroFalso();
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1", cnpj: "00000000000001")]), cadastro, repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(1);
        repositorio.Despesas[0].FornecedorId.ShouldBeNull();
        cadastro.Consultas.ShouldBeEmpty("nem tentou consultar o sentinela");
    }

    [Fact]
    public async Task Cnpj_desconhecido_pela_fonte_nao_impede_a_despesa()
    {
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1")]),
            new CadastroFalso { Responde = false },
            repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(1);
        resultado.FornecedoresNovos.ShouldBe(0);
        repositorio.Despesas[0].FornecedorId.ShouldBeNull();
    }

    [Fact]
    public async Task Nota_malformada_vira_rejeicao_contada_e_nao_derruba_o_lote()
    {
        // Valores inconsistentes fazem o dominio recusar a despesa. Uma nota ruim nao
        // pode custar as outras 1.199 do mesmo deputado.
        var ruim = Despesa("ruim") with { ValorDocumento = 100m, ValorLiquido = 90m, ValorGlosa = 50m };
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), ruim, Despesa("2")]), new CadastroFalso(), repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(2);
        resultado.Rejeitadas.Count.ShouldBe(1);
        resultado.Rejeitadas[0].CodigoDocumento.ShouldBe("ruim");
        resultado.Rejeitadas[0].Motivo.ShouldContain("glosa");
    }

    [Fact]
    public async Task Falha_na_fonte_de_cadastro_nao_derruba_a_coleta_do_deputado()
    {
        // Aconteceu na primeira coleta real: uma consulta de CNPJ que esgotou as tentativas
        // por rate-limit abortava a ingestao inteira do parlamentar. Mil despesas ja
        // gravadas ficavam sem checkpoint e a proxima passada recomecava do zero.
        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("2")]),
            new CadastroFalso { Explode = true },
            repositorio);

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(2, "as despesas continuam sendo gravadas");
        resultado.FornecedoresNaoEnriquecidos.ShouldBe(2);
        repositorio.Despesas.ShouldAllBe(d => d.FornecedorId == null);
    }

    [Fact]
    public async Task Falha_de_enriquecimento_nao_e_cacheada()
    {
        // "Nao deu para consultar" e' diferente de "consultei e nao existe". Cachear a
        // falha faria a proxima passada herdar o buraco em vez de tentar de novo.
        var cadastro = new CadastroFalso { Explode = true };
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1"), Despesa("2"), Despesa("3")]),
            cadastro,
            new RepositorioFalso());

        await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        cadastro.Consultas.Count.ShouldBe(3, "tentou de novo a cada despesa, sem cachear a falha");
    }

    [Fact]
    public async Task Cnpj_ausente_do_cadastro_nao_conta_como_falha_de_enriquecimento()
    {
        // Um numero alto de "nao enriquecidos" e' sinal de problema de infraestrutura.
        // Misturar com "a fonte nao conhece o CNPJ", que e' rotina, apagaria esse sinal.
        var servico = new ServicoDeIngestao(
            new FonteFalsa([Despesa("1")]),
            new CadastroFalso { Responde = false },
            new RepositorioFalso());

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.DespesasNovas.ShouldBe(1);
        resultado.FornecedoresNaoEnriquecidos.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    //  O alarme de sanidade da Etapa 01
    // ------------------------------------------------------------------

    [Fact]
    public async Task Coleta_vazia_para_deputado_em_exercicio_gera_alarme()
    {
        // Este e' o teste que impede a falha mais cara possivel de voltar: sem
        // idLegislatura a API responde 200 com lista vazia, e a coleta inteira
        // "termina com sucesso" tendo gravado nada.
        var servico = new ServicoDeIngestao(
            new FonteFalsa([]), new CadastroFalso(), new RepositorioFalso());

        var resultado = await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        resultado.TemAlerta.ShouldBeTrue();
        resultado.AlertasDeSanidade[0].ShouldContain("idLegislatura");
    }

    [Fact]
    public async Task Coleta_vazia_para_quem_nao_esta_em_exercicio_nao_gera_alarme()
    {
        // Suplente que nunca assumiu realmente nao tem despesa: alarmar aqui seria
        // ruido que ensina a ignorar o alarme.
        var servico = new ServicoDeIngestao(
            new FonteFalsa([]), new CadastroFalso(), new RepositorioFalso());

        var resultado = await servico.IngerirDeputadoAsync(
            Deputado with { EmExercicio = false }, Legislatura);

        resultado.TemAlerta.ShouldBeFalse();
    }

    [Fact]
    public async Task Repassa_a_legislatura_para_a_fonte()
    {
        var fonte = new FonteFalsa([Despesa("1")]);
        var servico = new ServicoDeIngestao(fonte, new CadastroFalso(), new RepositorioFalso());

        await servico.IngerirDeputadoAsync(Deputado, Legislatura);

        fonte.LegislaturaPedida.ShouldBe(Legislatura);
    }

    // ==================================================================
    //  Dubles
    // ==================================================================

    private sealed class FonteFalsa(IReadOnlyList<DespesaDaFonte> despesas) : IFonteDeParlamentares
    {
        public int? LegislaturaPedida { get; private set; }

        public async IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            yield return Deputado;
            await Task.CompletedTask;
        }

        public Task<DeputadoDaFonte?> ObterDetalheAsync(int id, CancellationToken cancelamento = default) =>
            Task.FromResult<DeputadoDaFonte?>(Deputado);

        public async IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
            int idExterno,
            int legislatura,
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            LegislaturaPedida = legislatura;

            foreach (var despesa in despesas)
            {
                yield return despesa;
            }

            await Task.CompletedTask;
        }
    }

    private sealed class CadastroFalso : IFonteDeCadastroEmpresarial
    {
        public bool Responde { get; init; } = true;

        /// <summary>Simula falha transitoria da fonte, como o 429 da coleta real.</summary>
        public bool Explode { get; init; }

        public List<string> Consultas { get; } = [];

        public Task<EmpresaDaFonte?> ConsultarAsync(string cnpj, CancellationToken cancelamento = default)
        {
            Consultas.Add(cnpj);

            if (Explode)
            {
                throw new HttpRequestException("429 simulado.");
            }

            if (!Responde)
            {
                return Task.FromResult<EmpresaDaFonte?>(null);
            }

            return Task.FromResult<EmpresaDaFonte?>(new EmpresaDaFonte
            {
                Cnpj = cnpj,
                RazaoSocial = "AUTO POSTO BOMTEMPO LTDA",
                CnaePrincipal = "4731800",
                CnaesSecundarios = ["4732600"],
                DescricaoSituacaoCadastral = "ATIVA",
                DataInicioAtividade = new DateOnly(1991, 7, 23),
                Uf = "GO",
                Municipio = "GOIANIA",
                QuadroSocietario =
                [
                    new SocioDaFonte
                    {
                        NomeSocio = "FULANO DE TAL PRIMEIRO SOCIO",
                        CpfMascarado = "***456789**",
                        Qualificacao = "Sócio-Administrador",
                    },
                ],
            });
        }
    }

    private sealed class RepositorioFalso : IRepositorioDeIngestao
    {
        public List<Parlamentar> Parlamentares { get; } = [];
        public List<Fornecedor> Fornecedores { get; } = [];
        public List<Sociedade> Sociedades { get; } = [];
        public List<Despesa> Despesas { get; } = [];

        public Task<Parlamentar?> ObterParlamentarAsync(int idExterno, CancellationToken c = default) =>
            Task.FromResult(Parlamentares.Find(p => p.IdExterno == idExterno));

        public Task<Fornecedor?> ObterFornecedorAsync(CpfCnpj documento, CancellationToken c = default) =>
            Task.FromResult(Fornecedores.Find(f => f.Documento == documento));

        public Task<HashSet<string>> ObterHashesDeDespesasAsync(Guid parlamentarId, CancellationToken c = default) =>
            Task.FromResult(Despesas
                .Where(d => d.ParlamentarId == parlamentarId)
                .Select(d => d.HashUnico)
                .ToHashSet(StringComparer.Ordinal));

        public void Adicionar(Parlamentar parlamentar) => Parlamentares.Add(parlamentar);

        public void Adicionar(Fornecedor fornecedor) => Fornecedores.Add(fornecedor);

        public void Adicionar(Sociedade sociedade) => Sociedades.Add(sociedade);

        public void Adicionar(Despesa despesa) => Despesas.Add(despesa);

        public Task SalvarAsync(CancellationToken c = default) => Task.CompletedTask;
    }
}
