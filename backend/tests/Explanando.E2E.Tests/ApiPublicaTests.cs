using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Explanando.Application.Consultas;

namespace Explanando.E2E.Tests;

/// <summary>
/// A API pública ponta a ponta: HTTP, serialização, projeção e SQL.
///
/// <para>É o nível onde erro de contrato aparece. Um teste de unidade sobre o serviço de
/// consulta passaria com o JSON errado, com o campo omitido pelo serializador ou com a
/// projeção que o EF não consegue traduzir.</para>
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public sealed class ApiPublicaTests(ApiDeTestes api)
{
    private HttpClient Cliente => api.CreateClient();

    private static async Task<T> LerAsync<T>(HttpResponseMessage resposta)
    {
        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);

        var corpo = await resposta.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(corpo, ApiDeTestes.Json)!;
    }

    // ------------------------------------------------------------------
    //  Sonda
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_api_responde()
    {
        var resposta = await Cliente.GetAsync("/saude");

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------------
    //  Ranking: três ordenações, e o piso amostral separando-as
    // ------------------------------------------------------------------

    [Fact]
    public async Task Ranking_por_volume_inclui_quem_nao_tem_amostra()
    {
        // Gasto não depende de amostra: um deputado com 3 despesas gastou o que gastou.
        var pagina = await LerAsync<Pagina<ItemDoRanking>>(
            await Cliente.GetAsync("/api/v1/parlamentares?ordenar=volume"));

        pagina.Itens.Select(i => i.Id).ShouldContain(api.ParlamentarSemAmostra);
        pagina.Total.ShouldBe(2);
    }

    [Theory]
    [InlineData("indice")]
    [InlineData("gravidade")]
    public async Task Ordenacoes_por_densidade_excluem_quem_nao_tem_amostra(string ordenacao)
    {
        // Abaixo do piso do ADR-0009 a densidade é ruído. Publicá-la com nome e foto seria
        // ranquear alguém por prova fina.
        var pagina = await LerAsync<Pagina<ItemDoRanking>>(
            await Cliente.GetAsync($"/api/v1/parlamentares?ordenar={ordenacao}"));

        pagina.Itens.Select(i => i.Id).ShouldNotContain(api.ParlamentarSemAmostra);
        pagina.Itens.ShouldAllBe(i => i.AmostraSuficiente);
    }

    [Fact]
    public async Task Quem_nao_tem_amostra_traz_indice_NULO_e_nao_zero()
    {
        // Zero diria que a densidade foi medida e deu nada. A interface precisa distinguir.
        var pagina = await LerAsync<Pagina<ItemDoRanking>>(
            await Cliente.GetAsync("/api/v1/parlamentares?ordenar=volume"));

        var semAmostra = pagina.Itens.Single(i => i.Id == api.ParlamentarSemAmostra);

        semAmostra.AmostraSuficiente.ShouldBeFalse();
        semAmostra.IndiceDeRisco.ShouldBeNull();
        semAmostra.IndiceDeAltas.ShouldBeNull();
        semAmostra.VolumeTotalGasto.ShouldBeGreaterThan(0m);
    }

    [Fact]
    public async Task Ordenacao_desconhecida_devolve_400_com_as_opcoes()
    {
        // Aceitar em silêncio devolveria o ranking por volume, e o consumidor acharia que
        // estava ordenando por risco.
        var resposta = await Cliente.GetAsync("/api/v1/parlamentares?ordenar=inventada");

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).ShouldContain("gravidade");
    }

    [Fact]
    public async Task A_pagina_tem_teto_de_itens()
    {
        // A paginação é a superfície mais fácil de abusar numa API pública sem autenticação.
        var pagina = await LerAsync<Pagina<ItemDoRanking>>(
            await Cliente.GetAsync("/api/v1/parlamentares?itens=100000"));

        pagina.ItensPorPagina.ShouldBeLessThanOrEqualTo(200);
    }

    // ------------------------------------------------------------------
    //  Perfil: a cobertura ao lado dos achados
    // ------------------------------------------------------------------

    [Fact]
    public async Task Perfil_traz_a_cobertura_de_todas_as_regras()
    {
        var perfil = await LerAsync<PerfilDoParlamentar>(
            await Cliente.GetAsync($"/api/v1/parlamentares/{api.ParlamentarRanqueado}"));

        perfil.Cobertura.Count.ShouldBe(11);
        perfil.RegrasConclusivas.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Regra_que_se_absteve_diz_por_que_em_portugues()
    {
        // O ADR-0013 §2 proíbe a interface de renderizar regra abstida como aprovada — e
        // sem o motivo cada consumidor redigiria a própria explicação, que é como
        // "nada irregular encontrado" acabaria aparecendo em algum lugar.
        var perfil = await LerAsync<PerfilDoParlamentar>(
            await Cliente.GetAsync($"/api/v1/parlamentares/{api.ParlamentarRanqueado}"));

        var mei = perfil.Cobertura.Single(c => c.Codigo == "REG_KYB_004");

        mei.SilencioEhConclusivo.ShouldBeFalse();
        mei.FlagsEmitidas.ShouldBe(0);
        mei.MotivoDaAbstencao.ShouldNotBeNullOrWhiteSpace();
        mei.MotivoDaAbstencao.ShouldContain("MEI");
    }

    [Fact]
    public async Task Zero_flags_por_abstencao_e_zero_por_nada_achado_sao_distinguiveis()
    {
        // As duas produzem zero. É a distinção inteira do produto.
        var perfil = await LerAsync<PerfilDoParlamentar>(
            await Cliente.GetAsync($"/api/v1/parlamentares/{api.ParlamentarRanqueado}"));

        var abstidas = perfil.Cobertura.Where(c => !c.SilencioEhConclusivo).ToList();
        var limpas = perfil.Cobertura
            .Where(c => c.SilencioEhConclusivo && c.FlagsEmitidas == 0)
            .ToList();

        abstidas.ShouldNotBeEmpty("alguma regra tem de estar sem dado nesta semente");
        limpas.ShouldNotBeEmpty("e alguma tem de ter avaliado e não achado nada");
        perfil.RegrasSemDado.ShouldBe(abstidas.Count);
    }

    [Fact]
    public async Task Perfil_traz_a_serie_mensal_para_o_grafico()
    {
        var perfil = await LerAsync<PerfilDoParlamentar>(
            await Cliente.GetAsync($"/api/v1/parlamentares/{api.ParlamentarRanqueado}"));

        perfil.SerieMensal.ShouldNotBeEmpty();
        perfil.SerieMensal.ShouldAllBe(mes => mes.DetalheDisponivel);
        perfil.SerieMensal.Sum(mes => mes.QuantidadeDeDespesas).ShouldBe(121);
    }

    [Fact]
    public async Task Parlamentar_inexistente_devolve_404()
    {
        var resposta = await Cliente.GetAsync($"/api/v1/parlamentares/{Guid.NewGuid()}");

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------
    //  Despesas: comprovante, glosa e estorno
    // ------------------------------------------------------------------

    [Fact]
    public async Task Despesa_com_comprovante_traz_a_url_reconstruida()
    {
        var despesa = await LerAsync<ItemDeDespesa>(
            await Cliente.GetAsync($"/api/v1/despesas/{api.DespesaComComprovante}"));

        despesa.TemDocumentoPublicado.ShouldBeTrue();
        despesa.UrlDocumento.ShouldNotBeNullOrWhiteSpace();
        despesa.UrlDocumento.ShouldContain("/publ/1472/");
    }

    [Fact]
    public async Task Despesa_sem_comprovante_traz_url_nula_e_nao_link_quebrado()
    {
        // Um link que dá 404 afirma que o documento sumiu. A ausência é da origem, e a
        // interface exibe a tag do ADR-0012 §1 no lugar do botão.
        var despesa = await LerAsync<ItemDeDespesa>(
            await Cliente.GetAsync($"/api/v1/despesas/{api.DespesaSemComprovante}"));

        despesa.TemDocumentoPublicado.ShouldBeFalse();
        despesa.UrlDocumento.ShouldBeNull();
    }

    [Fact]
    public async Task O_cnpj_sai_formatado_e_nunca_cru()
    {
        var despesa = await LerAsync<ItemDeDespesa>(
            await Cliente.GetAsync($"/api/v1/despesas/{api.DespesaComComprovante}"));

        despesa.DocumentoFornecedor.ShouldContain(".");
        despesa.DocumentoFornecedor.ShouldContain("/");
    }

    [Fact]
    public async Task A_listagem_pagina_e_conta_o_total()
    {
        var pagina = await LerAsync<Pagina<ItemDeDespesa>>(
            await Cliente.GetAsync(
                $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?itens=10"));

        pagina.Itens.Count.ShouldBe(10);
        pagina.Total.ShouldBe(121);
        pagina.TotalDePaginas.ShouldBe(13);
    }

    [Fact]
    public async Task Filtro_por_flag_separa_com_e_sem_achado()
    {
        var com = await LerAsync<Pagina<ItemDeDespesa>>(
            await Cliente.GetAsync(
                $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?comFlag=true&itens=200"));

        var sem = await LerAsync<Pagina<ItemDeDespesa>>(
            await Cliente.GetAsync(
                $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?comFlag=false&itens=200"));

        com.Itens.ShouldAllBe(d => d.Flags.Count > 0);
        sem.Itens.ShouldAllBe(d => d.Flags.Count == 0);
        (com.Total + sem.Total).ShouldBe(121);
    }

    [Fact]
    public async Task Mes_sem_ano_devolve_400_em_vez_de_ignorar_o_filtro()
    {
        // Aceitar em silêncio devolveria a lista inteira e o leitor acharia que filtrou.
        var resposta = await Cliente.GetAsync(
            $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?mes=7");

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mes_fora_da_faixa_devolve_400()
    {
        var resposta = await Cliente.GetAsync(
            $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?ano=2026&mes=13");

        resposta.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task O_estorno_aparece_com_valor_negativo_e_marcado()
    {
        // Esconder o estorno inflaria o gasto exibido: passagem comprada e cancelada
        // apareceria como gasta.
        var pagina = await LerAsync<Pagina<ItemDeDespesa>>(
            await Cliente.GetAsync(
                $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?itens=200"));

        var estorno = pagina.Itens.Single(d => d.EhEstorno);

        estorno.ValorLiquido.ShouldBeLessThan(0m);
    }

    [Fact]
    public async Task A_flag_carrega_gravidade_confianca_e_versao_do_motor()
    {
        // A gravidade decide o peso visual (ADR-0013); a versão distingue achado novo por
        // despesa nova de achado novo por regra alterada.
        var pagina = await LerAsync<Pagina<ItemDeDespesa>>(
            await Cliente.GetAsync(
                $"/api/v1/parlamentares/{api.ParlamentarRanqueado}/despesas?comFlag=true&itens=5"));

        var flag = pagina.Itens.SelectMany(d => d.Flags).First();

        flag.CodigoRegra.ShouldStartWith("REG_");
        flag.Descricao.ShouldNotBeNullOrWhiteSpace();
        flag.GrauDeConfianca.ShouldBeGreaterThan(0m);
        flag.VersaoDoMotor.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Despesa_inexistente_devolve_404()
    {
        var resposta = await Cliente.GetAsync($"/api/v1/despesas/{Guid.NewGuid()}");

        resposta.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
