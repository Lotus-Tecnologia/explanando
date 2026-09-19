using System.Net.Http.Json;
using Explanando.Application.Consultas;

namespace Explanando.E2E.Tests;

/// <summary>
/// O mapa de fornecedores pelo caminho inteiro: HTTP, serialização, projeção e SQL.
///
/// <para>A semente ajuda de graça: os três parlamentares dela dividem o mesmo posto de
/// combustível. É <b>exatamente</b> a forma do caso que o mapa precisa tratar com cuidado
/// — um fornecedor em comum, três pares, e nenhum deles denso o bastante para virar
/// aresta no desenho.</para>
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public sealed class RedeNaApiTests(ApiDeTestes api)
{
    private async Task<MapaDaRede> MapaAsync()
    {
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDaRede>("/api/v1/rede", ApiDeTestes.Json);

        mapa.ShouldNotBeNull();
        return mapa;
    }

    [Fact]
    public async Task O_mapa_chega_pelo_HTTP_com_a_ligacao_e_quem_ela_liga()
    {
        var mapa = await MapaAsync();

        var ligacao = mapa.Ligacoes.ShouldHaveSingleItem();

        ligacao.Fornecedor.ShouldBe("AUTO POSTO BOMTEMPO LTDA");
        ligacao.Parlamentares.Count.ShouldBe(3);
        ligacao.CnaePrincipal.ShouldBe("4723700");
        ligacao.Municipio.ShouldBe("GOIANIA");
        ligacao.Uf.ShouldBe("GO");
    }

    [Fact]
    public async Task Deputado_e_senador_convivem_no_mesmo_mapa__com_a_casa_marcada()
    {
        // A chave da casa e' o que o desenho usa para escolher a forma do no'. Se ela
        // deixasse de vir, o grafo cairia numa forma so' sem nenhum teste ficar vermelho.
        var mapa = await MapaAsync();

        mapa.Parlamentares.Count.ShouldBe(3);
        mapa.Parlamentares.ShouldContain(pessoa => pessoa.ChaveDaCasa == "senado");
        mapa.Parlamentares.ShouldContain(pessoa => pessoa.ChaveDaCasa == "camara");
        mapa.Parlamentares.ShouldAllBe(pessoa => pessoa.Casa != string.Empty);
    }

    [Fact]
    public async Task Um_fornecedor_em_comum_nao_desenha_aresta__e_o_mapa_diz_quantos_ficaram_de_fora()
    {
        // Tres pessoas com UM fornecedor em comum produzem tres pares, e nenhum deles
        // entra no desenho. Sem o contador, o leitor veria um grafo vazio sem saber por que.
        var mapa = await MapaAsync();

        mapa.Pares.ShouldBeEmpty();
        mapa.ParesComUmFornecedorSo.ShouldBe(3);
    }

    [Fact]
    public async Task O_estorno_da_semente_nao_entra_na_conta_da_aresta()
    {
        var mapa = await MapaAsync();

        var ligacao = mapa.Ligacoes.ShouldHaveSingleItem();

        ligacao.Valor.ShouldBeGreaterThan(0m);
        ligacao.Notas.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task As_ressalvas_viajam_no_JSON__e_nao_so_na_tela()
    {
        // O ponto inteiro de po-las no contrato: quem consumir esta API por fora recebe o
        // aviso junto com os dados, e nao tem como desenhar o grafo sem te-lo visto.
        var mapa = await MapaAsync();

        mapa.Ressalva.ShouldContain("não é indício de irregularidade");
        mapa.PorQueEsteCorte.ShouldContain("companhias aéreas");
        mapa.OQueADensidadeMostra.ShouldContain("mesmo estado");
    }

    [Fact]
    public async Task A_escala_do_corte_vem_junto__quantos_existem_e_qual_o_maior()
    {
        var mapa = await MapaAsync();

        mapa.FornecedoresNaBase.ShouldBeGreaterThan(0);
        mapa.FornecedoresCompartilhados.ShouldBe(1);
        mapa.MaiorFornecedorCompartilhado.ShouldBe("AUTO POSTO BOMTEMPO LTDA");
        mapa.ParlamentaresAtendidosPeloMaior.ShouldBe(3);
    }

    [Fact]
    public async Task As_ligacoes_de_um_parlamentar_vem_pela_rota_dele()
    {
        var ligacoes = await api.CreateClient()
            .GetFromJsonAsync<IReadOnlyList<LigacaoDaRede>>(
                $"/api/v1/rede/{api.ParlamentarRanqueado}",
                ApiDeTestes.Json);

        ligacoes.ShouldNotBeNull();
        ligacoes.ShouldHaveSingleItem().Fornecedor.ShouldBe("AUTO POSTO BOMTEMPO LTDA");
    }

    [Fact]
    public async Task Quem_nao_esta_no_mapa_recebe_lista_vazia_e_200__nao_404()
    {
        // Nao estar no mapa e' o caso da maioria, e nao e' erro nem ausencia de dado.
        // Responder 404 faria a tela dizer "nao encontrado" sobre alguem que existe.
        var resposta = await api.CreateClient()
            .GetAsync(new Uri($"/api/v1/rede/{Guid.NewGuid()}", UriKind.Relative));

        resposta.EnsureSuccessStatusCode();

        var ligacoes = await resposta.Content
            .ReadFromJsonAsync<IReadOnlyList<LigacaoDaRede>>(ApiDeTestes.Json);

        ligacoes.ShouldBeEmpty();
    }
}
