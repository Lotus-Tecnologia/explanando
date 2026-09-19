using Explanando.Application.Estado;
using Explanando.Application.Ingestao;
using Explanando.Domain.Estado;

namespace Explanando.Application.Tests.Estado;

/// <summary>
/// A coleta do grafo do Estado.
///
/// <para>O serviço existe porque <b>o SIORG publica um retrato do agora e nada mais</b>. A
/// história — que é metade do valor do produto — precisa ser deduzida comparando coletas, e
/// é aqui que a comparação acontece.</para>
///
/// <para>Duas políticas dominam os testes abaixo, e as duas são a postura permissiva do
/// ADR-0007 aplicada a um grafo: <b>a primeira coleta é linha de base</b>, não vinte mil
/// notícias; e <b>coleta incompleta nunca faz órgão desaparecer</b>, porque a diferença
/// entre "foi extinto" e "a API falhou naquela subárvore" é invisível no resultado.</para>
/// </summary>
public sealed class ServicoDeColetaDoEstadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 19, 3, 0, 0, TimeSpan.Zero);

    private static NovaUnidadeDoEstado Unidade(
        int codigo,
        int? pai = null,
        string? nome = null,
        EsferaDeGoverno? esfera = EsferaDeGoverno.Federal,
        TipoDeUnidade tipo = TipoDeUnidade.UnidadeAdministrativa) => new()
        {
            Codigo = codigo,
            CodigoPai = pai,
            CodigoOrgao = 26,
            Nome = nome ?? $"Unidade {codigo}",
            Sigla = $"U{codigo}",
            Poder = Poder.Executivo,
            Esfera = esfera,
            Tipo = tipo,
            NaturezaJuridica = NaturezaJuridica.AdministracaoDireta,
            Normatizacao = NivelDeNormatizacao.AtoInterno,
        };

    // ------------------------------------------------------------------
    //  A primeira coleta e' linha de base
    // ------------------------------------------------------------------

    [Fact]
    public async Task Primeira_coleta_grava_o_grafo_e_nao_anuncia_mudanca_nenhuma()
    {
        // Sem isto, o dia um do produto teria ~20 mil linhas de "passou a constar na
        // estrutura oficial" — o que e' verdade e e' inutil. A tela de mudancas so' tem
        // valor se cada linha for algo que realmente aconteceu no Estado.
        var fonte = new FonteFalsa(
            orgaos: [Unidade(26, pai: 208613, tipo: TipoDeUnidade.Orgao)],
            subarvores: new() { [26] = [Unidade(26, pai: 208613, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)] },
            avulsas: new() { [208613] = Unidade(208613, pai: null, nome: "Poder Executivo", tipo: TipoDeUnidade.Ente) });

        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora));

        var resultado = await servico.ColetarAsync();

        resultado.EhLinhaDeBase.ShouldBeTrue();
        resultado.UnidadesNovas.ShouldBe(3);
        resultado.Mudancas.ShouldBe(0);
        repositorio.Mudancas.ShouldBeEmpty();
        repositorio.Unidades.Count.ShouldBe(3);
    }

    [Fact]
    public async Task O_ancestral_que_nao_e_orgao_e_buscado_para_o_grafo_nao_ficar_solto()
    {
        // Medido: o pai da Presidencia (26) e' o ente 208613 "Poder Executivo", que NAO
        // aparece em /orgao-entidade e para o qual /estrutura-organizacional responde
        // codigoErro 104. Sem resolver ancestrais, o grafo comeca no segundo nivel e as
        // arestas de topo apontam para o vazio.
        var fonte = new FonteFalsa(
            orgaos: [Unidade(26, pai: 208613, tipo: TipoDeUnidade.Orgao)],
            subarvores: new() { [26] = [Unidade(26, pai: 208613, tipo: TipoDeUnidade.Orgao)] },
            avulsas: new()
            {
                [208613] = Unidade(208613, pai: 200001, nome: "Poder Executivo", tipo: TipoDeUnidade.Ente),
                [200001] = Unidade(200001, pai: 200000, nome: "União", tipo: TipoDeUnidade.Ente),
                [200000] = Unidade(200000, pai: null, nome: "República Federativa do Brasil", tipo: TipoDeUnidade.Ente),
            });

        var repositorio = new RepositorioFalso();
        var servico = new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora));

        await servico.ColetarAsync();

        repositorio.Unidades.Keys.OrderBy(codigo => codigo)
            .ShouldBe([26, 200000, 200001, 208613]);

        // Nenhuma aresta aponta para fora do conjunto.
        foreach (var unidade in repositorio.Unidades.Values.Where(u => u.CodigoPai is not null))
        {
            repositorio.Unidades.ShouldContainKey(unidade.CodigoPai!.Value);
        }
    }

    // ------------------------------------------------------------------
    //  A segunda coleta e' a que produz noticia
    // ------------------------------------------------------------------

    [Fact]
    public async Task Segunda_coleta_reporta_renomeacao_e_troca_de_pai()
    {
        var repositorio = new RepositorioFalso();

        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)]);

        var resultado = await Coletar(repositorio,
            [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26, nome: "Unidade rebatizada")]);

        resultado.EhLinhaDeBase.ShouldBeFalse();
        resultado.Mudancas.ShouldBe(1);
        repositorio.Mudancas.ShouldHaveSingleItem();
        repositorio.Mudancas[0].Tipo.ShouldBe(TipoDeMudanca.Alterada);
        repositorio.Mudancas[0].Campo.ShouldBe("Nome");
        repositorio.Mudancas[0].NomeNaEpoca.ShouldBe("Unidade 100");
    }

    [Fact]
    public async Task Unidade_nova_na_segunda_coleta_vira_criacao()
    {
        var repositorio = new RepositorioFalso();

        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao)]);

        var resultado = await Coletar(repositorio,
            [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(101, pai: 26)]);

        resultado.UnidadesNovas.ShouldBe(1);
        repositorio.Mudancas.ShouldHaveSingleItem();
        repositorio.Mudancas[0].Tipo.ShouldBe(TipoDeMudanca.Criada);
        repositorio.Mudancas[0].CodigoUnidade.ShouldBe(101);
    }

    [Fact]
    public async Task Unidade_que_some_de_coleta_completa_e_marcada_e_nao_apagada()
    {
        var repositorio = new RepositorioFalso();

        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)]);

        var resultado = await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao)]);

        resultado.UnidadesDesaparecidas.ShouldBe(1);
        repositorio.Unidades.ShouldContainKey(100);
        repositorio.Unidades[100].Ativa.ShouldBeFalse();
        repositorio.Mudancas.ShouldHaveSingleItem();
        repositorio.Mudancas[0].Tipo.ShouldBe(TipoDeMudanca.Desaparecida);
    }

    [Fact]
    public async Task Unidade_que_volta_reaparece()
    {
        var repositorio = new RepositorioFalso();

        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)]);
        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao)]);

        var resultado = await Coletar(repositorio,
            [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)]);

        resultado.UnidadesReaparecidas.ShouldBe(1);
        repositorio.Unidades[100].Ativa.ShouldBeTrue();
        repositorio.Mudancas[^1].Tipo.ShouldBe(TipoDeMudanca.Reaparecida);
    }

    // ------------------------------------------------------------------
    //  ADR-0007: ausencia de dado nao e' fato
    // ------------------------------------------------------------------

    [Fact]
    public async Task Coleta_incompleta_nao_faz_orgao_desaparecer()
    {
        // O caso que este teste impede: a subarvore de um ministerio falha, as 300 unidades
        // dele somem da resposta, e o produto publica "300 orgaos deixaram de constar na
        // estrutura oficial". Seria uma manchete falsa gerada por um timeout.
        var repositorio = new RepositorioFalso();

        await Coletar(repositorio, [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(100, pai: 26)]);

        var fonte = new FonteFalsa(
            orgaos: [Unidade(26, tipo: TipoDeUnidade.Orgao)],
            subarvores: [],
            avulsas: [],
            falhaEm: codigo => codigo == 26);

        var resultado = await new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora))
            .ColetarAsync();

        resultado.Completa.ShouldBeFalse();
        resultado.OrgaosComFalha.ShouldBe(1);
        resultado.UnidadesDesaparecidas.ShouldBe(0);
        repositorio.Unidades[100].Ativa.ShouldBeTrue();
        repositorio.Mudancas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Esfera_fora_do_escopo_nao_entra_no_grafo_por_acidente()
    {
        // Medido: /orgao-entidade traz o Governo do Distrito Federal e mais quatro orgaos
        // estaduais. Eles nao sao erro da fonte — sao entes que tambem usam o SIORG. Mas o
        // produto e' sobre a Uniao, e absorver cinco unidades de outro ente sem ninguem ter
        // escolhido isso e' pior do que nao te-las.
        var fonte = new FonteFalsa(
            orgaos:
            [
                Unidade(26, tipo: TipoDeUnidade.Orgao),
                Unidade(200002, nome: "Governo do Distrito Federal", esfera: EsferaDeGoverno.EstadualOuDistrital, tipo: TipoDeUnidade.Orgao),
            ],
            subarvores: new()
            {
                [26] = [Unidade(26, tipo: TipoDeUnidade.Orgao)],
                [200002] = [Unidade(200002, esfera: EsferaDeGoverno.EstadualOuDistrital, tipo: TipoDeUnidade.Orgao)],
            },
            avulsas: []);

        var repositorio = new RepositorioFalso();

        var resultado = await new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora))
            .ColetarAsync();

        resultado.OrgaosForaDoEscopo.ShouldBe(1);
        repositorio.Unidades.ShouldNotContainKey(200002);
        fonte.SubarvoresPedidas.ShouldBe([26]);
    }

    [Fact]
    public async Task Unidade_que_a_fonte_descreve_mal_e_recusada_sem_derrubar_a_coleta()
    {
        // Cadastro publico erra. Uma unidade que se declara pai de si mesma travaria
        // qualquer travessia em profundidade — o dominio a recusa, e a coleta segue com as
        // outras dezenove mil em vez de morrer na primeira.
        var fonte = new FonteFalsa(
            orgaos: [Unidade(26, tipo: TipoDeUnidade.Orgao)],
            subarvores: new()
            {
                [26] =
                [
                    Unidade(26, tipo: TipoDeUnidade.Orgao),
                    Unidade(100, pai: 100),
                    Unidade(101, pai: 26),
                ],
            },
            avulsas: []);

        var repositorio = new RepositorioFalso();

        var resultado = await new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora))
            .ColetarAsync();

        resultado.UnidadesRecusadas.ShouldBe(1);
        repositorio.Unidades.Keys.OrderBy(c => c).ShouldBe([26, 101]);
    }

    [Fact]
    public async Task A_mesma_unidade_vinda_por_duas_subarvores_entra_uma_vez_so()
    {
        // Medido: a arvore do Ministerio da Defesa e a dos comandos militares se
        // sobrepoem, e /orgao-entidade repete unidades entre subarvores. Sem deduplicar por
        // codigo, a mesma unidade viraria duas linhas e o total ficaria inflado.
        var fonte = new FonteFalsa(
            orgaos: [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(41066, pai: 26, tipo: TipoDeUnidade.Orgao)],
            subarvores: new()
            {
                [26] = [Unidade(26, tipo: TipoDeUnidade.Orgao), Unidade(41066, pai: 26, tipo: TipoDeUnidade.Orgao)],
                [41066] = [Unidade(41066, pai: 26, tipo: TipoDeUnidade.Orgao), Unidade(94, pai: 41066)],
            },
            avulsas: []);

        var repositorio = new RepositorioFalso();

        var resultado = await new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora))
            .ColetarAsync();

        resultado.UnidadesVistas.ShouldBe(3);
        repositorio.Unidades.Count.ShouldBe(3);
    }

    // ------------------------------------------------------------------
    //  Dubles
    // ------------------------------------------------------------------

    private static async Task<ResultadoDaColetaDoEstado> Coletar(
        RepositorioFalso repositorio,
        IReadOnlyList<NovaUnidadeDoEstado> unidades)
    {
        var raiz = unidades[0];

        var fonte = new FonteFalsa(
            orgaos: [raiz],
            subarvores: new() { [raiz.Codigo] = unidades },
            avulsas: []);

        return await new ServicoDeColetaDoEstado(fonte, repositorio, new RelogioFixo(Agora))
            .ColetarAsync();
    }

    private sealed class FonteFalsa(
        IReadOnlyList<NovaUnidadeDoEstado> orgaos,
        Dictionary<int, IReadOnlyList<NovaUnidadeDoEstado>> subarvores,
        Dictionary<int, NovaUnidadeDoEstado> avulsas,
        Func<int, bool>? falhaEm = null) : IFonteDaEstruturaDoEstado
    {
        public List<int> SubarvoresPedidas { get; } = [];

        public Task<IReadOnlyList<NovaUnidadeDoEstado>> ListarOrgaosAsync(
            CancellationToken cancelamento = default) => Task.FromResult(orgaos);

        public Task<SubarvoreDoEstado> ObterSubarvoreAsync(
            int codigoDoOrgao,
            CancellationToken cancelamento = default)
        {
            SubarvoresPedidas.Add(codigoDoOrgao);

            return Task.FromResult(falhaEm?.Invoke(codigoDoOrgao) == true
                ? new SubarvoreDoEstado { CodigoDoOrgao = codigoDoOrgao, Falha = "fonte indisponivel" }
                : new SubarvoreDoEstado
                {
                    CodigoDoOrgao = codigoDoOrgao,
                    Unidades = subarvores.GetValueOrDefault(codigoDoOrgao, []),
                });
        }

        public Task<NovaUnidadeDoEstado?> ObterUnidadeAsync(
            int codigo,
            CancellationToken cancelamento = default) =>
            Task.FromResult(avulsas.GetValueOrDefault(codigo));
    }

    private sealed class RepositorioFalso : IRepositorioDoEstado
    {
        public Dictionary<int, UnidadeDoEstado> Unidades { get; } = [];

        public List<MudancaEstrutural> Mudancas { get; } = [];

        public Task<IReadOnlyList<UnidadeDoEstado>> CarregarUnidadesAsync(
            CancellationToken cancelamento = default) =>
            Task.FromResult<IReadOnlyList<UnidadeDoEstado>>([.. Unidades.Values]);

        public void Adicionar(UnidadeDoEstado unidade) => Unidades[unidade.Codigo] = unidade;

        public void Registrar(MudancaEstrutural mudanca) => Mudancas.Add(mudanca);

        public Task SalvarAsync(CancellationToken cancelamento = default) => Task.CompletedTask;
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora => agora;
    }
}
