using Explanando.Application.Consultas;
using Explanando.Domain.Auditoria;

namespace Explanando.Application.Tests.Consultas;

/// <summary>
/// A decomposição do índice por regra — a Fase 1 do plano de execução.
///
/// <para>A promessa é aritmética e por isso testável: as fatias somam 100% e as parcelas
/// somam o índice que a página publica. Se uma das duas falhar, a tela diz de onde vem um
/// número que não é o número.</para>
/// </summary>
public class ComposicaoDoIndiceTests
{
    // O deputado de maior volume na base local: 49 médias e 20 baixas em 2.878 despesas,
    // índice 4,10.
    private static readonly AchadosAgrupados[] Pompeo =
    [
        new("REG_KYB_001", Gravidade.Media, 29, 58m),
        new("REG_TX_003", Gravidade.Baixa, 19, 19m),
        new("REG_KYB_005", Gravidade.Media, 14, 28m),
        new("REG_TX_004", Gravidade.Media, 5, 10m),
        new("REG_TX_001", Gravidade.Media, 1, 2m),
        new("REG_GEO_002", Gravidade.Baixa, 1, 1m),
    ];

    [Fact]
    public void As_fatias_somam_cem_por_cento()
    {
        var composicao = ComposicaoDoIndice.Montar(Pompeo, 2_878);

        composicao.Sum(parcela => parcela.Fatia).ShouldBe(1m, 0.000_001m);
    }

    [Fact]
    public void As_parcelas_somam_o_indice_publicado()
    {
        var composicao = ComposicaoDoIndice.Montar(Pompeo, 2_878);

        // A mesma conta do ScoreRiscoParlamentar: peso total / despesas × 100.
        var indice = 118m / 2_878m * 100m;

        composicao.Sum(parcela => parcela.PontosNoIndice).ShouldBe(indice, 0.000_001m);
        Math.Round(indice, 2).ShouldBe(4.10m);
    }

    [Fact]
    public void Vem_da_regra_que_mais_pesa_para_a_que_menos_pesa()
    {
        var composicao = ComposicaoDoIndice.Montar(Pompeo, 2_878);

        composicao.Select(parcela => parcela.Codigo).ShouldBe(
            ["REG_KYB_001", "REG_KYB_005", "REG_TX_003", "REG_TX_004", "REG_TX_001", "REG_GEO_002"]);
    }

    [Fact]
    public void Ordena_pelo_peso_e_nao_pela_quantidade()
    {
        // Dezenove baixas pesam 19; catorze médias pesam 28. Contar achados colocaria a
        // regra de peso menor na frente.
        var composicao = ComposicaoDoIndice.Montar(Pompeo, 2_878);

        var baixas = composicao.Single(parcela => parcela.Codigo == "REG_TX_003");
        var medias = composicao.Single(parcela => parcela.Codigo == "REG_KYB_005");

        baixas.Quantidade.ShouldBeGreaterThan(medias.Quantidade);
        composicao.ToList().IndexOf(medias).ShouldBeLessThan(composicao.ToList().IndexOf(baixas));
    }

    [Fact]
    public void Junta_as_gravidades_de_uma_mesma_regra()
    {
        AchadosAgrupados[] achados =
        [
            new("REG_TX_004", Gravidade.Alta, 2, 6m),
            new("REG_TX_004", Gravidade.Media, 3, 6m),
        ];

        var parcela = ComposicaoDoIndice.Montar(achados, 100).ShouldHaveSingleItem();

        parcela.Quantidade.ShouldBe(5);
        parcela.Peso.ShouldBe(12m);
        parcela.Altas.ShouldBe(2);
        parcela.Medias.ShouldBe(3);
        parcela.Baixas.ShouldBe(0);
        parcela.Fatia.ShouldBe(1m);
    }

    [Fact]
    public void Traz_a_redacao_de_leitor_da_regra()
    {
        var composicao = ComposicaoDoIndice.Montar(Pompeo, 2_878);

        composicao.ShouldAllBe(parcela => !string.IsNullOrWhiteSpace(parcela.Descricao));
    }

    [Fact]
    public void Inclui_a_regra_de_rede()
    {
        // A regra que cruza parlamentares não tem linha na cobertura individual, e por isso
        // sumia da explicação. Aqui ela aparece como qualquer outra.
        AchadosAgrupados[] achados = [new("REG_NET_004", Gravidade.Media, 1, 1.4m)];

        var parcela = ComposicaoDoIndice.Montar(achados, 100).ShouldHaveSingleItem();

        parcela.Descricao.ShouldNotBeNull();
        parcela.PontosNoIndice.ShouldBe(1.4m);
    }

    [Fact]
    public void Sem_achado_nao_ha_parcela()
    {
        ComposicaoDoIndice.Montar([], 2_878).ShouldBeEmpty();
    }

    [Fact]
    public void Sem_despesa_nao_divide_por_zero()
    {
        var parcela = ComposicaoDoIndice
            .Montar([new("REG_TX_001", Gravidade.Media, 1, 2m)], 0)
            .ShouldHaveSingleItem();

        parcela.PontosNoIndice.ShouldBe(0m);
        parcela.Fatia.ShouldBe(1m);
    }
}
