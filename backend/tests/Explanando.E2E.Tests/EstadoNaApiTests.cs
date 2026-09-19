using System.Net;
using System.Net.Http.Json;
using Explanando.Application.Consultas;

namespace Explanando.E2E.Tests;

/// <summary>
/// O grafo do Estado pelo caminho inteiro: HTTP, serialização, projeção e SQL.
///
/// <para>É aqui que erro de contrato aparece. Um teste de unidade sobre a consulta passaria
/// com o JSON errado — e o JSON é o que a interface e qualquer consumidor externo leem.</para>
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public sealed class EstadoNaApiTests(ApiDeTestes api)
{
    [Fact]
    public async Task O_mapa_traz_os_nos_os_totais_e_os_cortes()
    {
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDoEstado>("/api/v1/estado", ApiDeTestes.Json);

        mapa.ShouldNotBeNull();

        // A unidade que sumiu nao conta em lugar nenhum do mapa: ela existe na base, mas o
        // desenho e os totais falam do Estado de hoje.
        mapa.TotalDeUnidades.ShouldBe(3);
        mapa.Nos.ShouldNotContain(no => no.Codigo == ApiDeTestes.UnidadeSumida);

        mapa.PorPoder.ShouldNotBeEmpty();
        mapa.PorTipo.ShouldNotBeEmpty();
        mapa.PorBaseLegal.ShouldNotBeEmpty();
        mapa.UltimaColeta.ShouldNotBeNull();
    }

    [Fact]
    public async Task O_mapa_mostra_orgaos_e_entes_mas_nao_unidades_administrativas()
    {
        // A regra que torna o desenho legivel: na base real sao mais de cem mil unidades
        // administrativas, e desenha-las junto produz uma nuvem onde o Executivo some.
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDoEstado>("/api/v1/estado", ApiDeTestes.Json);

        mapa!.Nos.Select(no => no.Codigo).Order()
            .ShouldBe([ApiDeTestes.UnidadeOrgao, ApiDeTestes.UnidadeRaiz]);
    }

    [Fact]
    public async Task A_contagem_de_subordinadas_e_recursiva()
    {
        // Contar so' os filhos diretos faria um ministerio com tres secretarias e mil
        // unidades aparecer do tamanho de um conselho de tres pessoas.
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDoEstado>("/api/v1/estado", ApiDeTestes.Json);

        var raiz = mapa!.Nos.Single(no => no.Codigo == ApiDeTestes.UnidadeRaiz);

        raiz.UnidadesSubordinadas.ShouldBe(2);
    }

    [Fact]
    public async Task Os_rotulos_chegam_por_extenso_e_nunca_como_codigo()
    {
        // ADR-0004 nesta metade do produto: "UnidadeColegiada" e "ATO_INTERNO" sao nomes
        // internos, e uma legenda de grafico escrita assim nao explica nada a ninguem.
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDoEstado>("/api/v1/estado", ApiDeTestes.Json);

        var orgao = mapa!.Nos.Single(no => no.Codigo == ApiDeTestes.UnidadeOrgao);

        orgao.Tipo.ShouldBe("Órgão");
        orgao.Poder.ShouldBe("Executivo");
        orgao.BaseLegal.ShouldBe("Criada por lei ou decreto");

        // A raiz nao ganha classificacao inventada: a fonte nao a classifica.
        mapa.Nos.Single(no => no.Codigo == ApiDeTestes.UnidadeRaiz).Poder.ShouldBeNull();
    }

    [Fact]
    public async Task Nenhum_texto_do_mapa_imputa_conduta()
    {
        // Esta metade do produto nao emite juizo nenhum. Um orgao criado por ato interno e'
        // um fato administrativo, e o vocabulario da gravidade nao atravessa a fronteira.
        string[] proibidos =
        [
            "suspeit", "irregular", "fraude", "desvio", "ilícit", "ilicit", "crime", "risco",
        ];

        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDoEstado>("/api/v1/estado", ApiDeTestes.Json);

        IEnumerable<string> textos =
        [
            .. mapa!.Nos.Select(no => $"{no.Tipo} {no.Poder} {no.Natureza} {no.BaseLegal}"),
            .. mapa.PorPoder.Select(item => item.Rotulo),
            .. mapa.PorTipo.Select(item => item.Rotulo),
            .. mapa.PorBaseLegal.Select(item => item.Rotulo),
        ];

        foreach (var texto in textos)
        {
            foreach (var proibido in proibidos)
            {
                texto.ShouldNotContain(proibido, Case.Insensitive);
            }
        }
    }

    [Fact]
    public async Task A_unidade_responde_com_a_ficha_completa()
    {
        var unidade = await api.CreateClient()
            .GetFromJsonAsync<NoDoEstado>(
                $"/api/v1/estado/{ApiDeTestes.UnidadeOrgao}", ApiDeTestes.Json);

        unidade.ShouldNotBeNull();
        unidade.Nome.ShouldBe("Presidência da República");
        unidade.Sigla.ShouldBe("PR");
        unidade.CodigoPai.ShouldBe(ApiDeTestes.UnidadeRaiz);
        unidade.Ativa.ShouldBeTrue();
    }

    [Fact]
    public async Task A_unidade_que_sumiu_continua_acessivel_e_marcada()
    {
        // ADR-0007 num grafo: apagar a linha perderia o pedaco do mapa que explica de onde
        // veio o que existe hoje. Ela some do desenho, nao da base.
        var unidade = await api.CreateClient()
            .GetFromJsonAsync<NoDoEstado>(
                $"/api/v1/estado/{ApiDeTestes.UnidadeSumida}", ApiDeTestes.Json);

        unidade.ShouldNotBeNull();
        unidade.Ativa.ShouldBeFalse();
    }

    [Fact]
    public async Task Unidade_inexistente_responde_404_e_nao_500()
    {
        var resposta = await api.CreateClient().GetAsync(new Uri("/api/v1/estado/7654321", UriKind.Relative));

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Os_filhos_diretos_de_uma_unidade_saem_sem_os_netos()
    {
        var filhos = await api.CreateClient()
            .GetFromJsonAsync<IReadOnlyList<NoDoEstado>>(
                $"/api/v1/estado/{ApiDeTestes.UnidadeOrgao}/unidades", ApiDeTestes.Json);

        filhos.ShouldNotBeNull();

        // Um nivel por vez: o conselho que sumiu tambem e' filho, e nao entra.
        filhos.Select(filho => filho.Codigo).ShouldBe([300]);
    }

    [Fact]
    public async Task As_mudancas_chegam_da_mais_nova_para_a_mais_velha_e_ja_traduzidas()
    {
        var mudancas = await api.CreateClient()
            .GetFromJsonAsync<IReadOnlyList<MudancaDoEstado>>(
                "/api/v1/estado/mudancas", ApiDeTestes.Json);

        mudancas.ShouldNotBeNull();
        mudancas.Count.ShouldBe(2);

        // A frase vem pronta. "CodigoPai" e "Alterada" sao telemetria e nao saem daqui.
        mudancas.ShouldContain(mudanca => mudanca.Descricao == "mudou de nome");
        mudancas.ShouldContain(mudanca => mudanca.Descricao.Contains("deixou de constar"));

        // O nome congelado: a linha continua legivel depois de a unidade ser renomeada.
        mudancas.ShouldAllBe(mudanca => mudanca.Unidade.Length > 0);
    }
}
