using System.Net.Http.Json;
using Explanando.Application.Consultas;

namespace Explanando.E2E.Tests;

/// <summary>
/// O mapa de poder pelo caminho inteiro.
///
/// <para>A semente não tem cadeira nenhuma, e é exatamente o caso que mais importa provar:
/// <b>o mapa vazio precisa dizer por que está vazio</b>. Um endpoint que devolvesse uma
/// lista vazia faria a tela desenhar um Estado sem comando; o que ele devolve são os
/// quatro poderes com <c>porQueFalta</c> preenchido.</para>
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public sealed class CadeirasNaApiTests(ApiDeTestes api)
{
    private async Task<MapaDePoderCompleto> MapaAsync()
    {
        var mapa = await api.CreateClient()
            .GetFromJsonAsync<MapaDePoderCompleto>("/api/v1/cadeiras", ApiDeTestes.Json);

        mapa.ShouldNotBeNull();
        return mapa;
    }

    [Fact]
    public async Task Os_quatro_poderes_vem_SEMPRE__inclusive_os_vazios()
    {
        // Omitir um poder sem ocupantes faria parecer que ele nao tem comando. O que
        // existe e' uma fonte que nao publica, e isso e' outra afirmacao.
        var mapa = await MapaAsync();

        mapa.Poderes.Count.ShouldBe(4);

        mapa.Poderes.Select(p => p.ChaveDoPoder).ShouldBe(
            ["executivo", "judiciario", "essenciais", "legislativo"]);
    }

    [Fact]
    public async Task Poder_sem_cadeira_EXPLICA_a_ausencia__e_nao_so_a_declara()
    {
        var mapa = await MapaAsync();

        foreach (var poder in mapa.Poderes.Where(p => p.TotalDeCadeiras == 0))
        {
            poder.PorQueFalta.ShouldNotBeNullOrWhiteSpace();
            poder.PorQueFalta!.ShouldContain("SIORG");
            poder.Camadas.ShouldBeEmpty();

            // Sem cadeira nao ha' data: nulo, e nao a data de hoje, que seria uma
            // verificacao que nunca aconteceu.
            poder.VerificadoDesde.ShouldBeNull();
        }
    }

    [Fact]
    public async Task A_ressalva_da_curadoria_viaja_no_JSON()
    {
        var mapa = await MapaAsync();

        mapa.RessalvaDaCuradoria.ShouldContain("data de verificação");
    }

    [Fact]
    public async Task Cada_poder_leva_a_chave_estavel_E_o_rotulo_de_tela()
    {
        // A chave e' o que o desenho usa para casar o setor do grafo com a aba do mapa de
        // poder; o rotulo e' o que o leitor le. Colapsa-los faria melhorar a redacao
        // quebrar a troca de vista.
        var mapa = await MapaAsync();

        mapa.Poderes.ShouldAllBe(p => p.ChaveDoPoder != p.Poder);
        mapa.Poderes.ShouldAllBe(p => p.Poder.Length > 0);
    }
}
