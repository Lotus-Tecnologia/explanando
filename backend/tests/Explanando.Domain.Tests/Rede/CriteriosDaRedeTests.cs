using Explanando.Domain.Rede;

namespace Explanando.Domain.Tests.Rede;

/// <summary>
/// O corte do mapa de fornecedores.
///
/// <para>Estes testes protegem <b>um número contra a tentação de afrouxá-lo</b>. O teto de
/// cinco parlamentares é o que impede o mapa de virar um emaranhado em que a CASCOL
/// Combustíveis liga 417 deputados entre si, e é exatamente o tipo de limite que alguém
/// sobe para "mostrar mais dados" sem perceber que está inventando relação.</para>
/// </summary>
public class CriteriosDaRedeTests
{
    [Fact]
    public void Um_fornecedor_de_um_gabinete_so_nao_liga_ninguem()
    {
        Assert.False(CriteriosDaRede.Liga(parlamentaresAtendidos: 1, valorLiquidoTotal: 900_000m));
    }

    [Fact]
    public void Dois_gabinetes_e_valor_alto_e_uma_ligacao()
    {
        Assert.True(CriteriosDaRede.Liga(parlamentaresAtendidos: 2, valorLiquidoTotal: 50_000m));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void A_faixa_inteira_de_dois_a_cinco_entra(int quantos)
    {
        Assert.True(CriteriosDaRede.Liga(quantos, 100_000m));
    }

    [Fact]
    public void O_posto_de_gasolina_que_atende_quatrocentos_deputados_fica_de_fora()
    {
        // O caso real: CASCOL Combustiveis, 417 parlamentares. Dinheiro nao falta; o que
        // falta e' significado. Abastecer no mesmo posto de Brasilia nao liga dois
        // gabinetes a coisa nenhuma, e desenhar essa aresta afirmaria que liga.
        Assert.False(CriteriosDaRede.Liga(parlamentaresAtendidos: 417, valorLiquidoTotal: 40_000_000m));
    }

    [Fact]
    public void Seis_parlamentares_ja_e_de_fora__o_teto_e_fechado()
    {
        Assert.False(CriteriosDaRede.Liga(parlamentaresAtendidos: 6, valorLiquidoTotal: 5_000_000m));
    }

    [Fact]
    public void Duas_notas_pequenas_no_mesmo_estacionamento_nao_viram_aresta()
    {
        // Sem o piso de valor, esta ligacao teria o mesmo peso visual de uma agencia de
        // comunicacao de R$ 1,5 milhao dividida por quatro gabinetes.
        Assert.False(CriteriosDaRede.Liga(parlamentaresAtendidos: 2, valorLiquidoTotal: 160m));
    }

    [Fact]
    public void O_piso_de_valor_e_inclusivo()
    {
        Assert.True(CriteriosDaRede.Liga(2, CriteriosDaRede.ValorMinimoDaLigacao));
        Assert.False(CriteriosDaRede.Liga(2, CriteriosDaRede.ValorMinimoDaLigacao - 0.01m));
    }

    [Fact]
    public void A_ressalva_nega_irregularidade_em_vez_de_so_omitir()
    {
        // ADR-0004. Um mapa de pessoas ligadas por dinheiro e' lido como acusacao por
        // padrao; nao basta nao acusar, e' preciso dizer que nao se esta' acusando.
        Assert.Contains("não é indício de irregularidade", CriteriosDaRede.Ressalva, StringComparison.Ordinal);
        Assert.Contains("lícito", CriteriosDaRede.Ressalva, StringComparison.Ordinal);
    }

    [Fact]
    public void A_explicacao_do_corte_cita_os_numeros_que_o_codigo_usa()
    {
        // Se alguem mudar o teto e esquecer o texto, a tela passa a mentir sobre o proprio
        // criterio. Este teste faz a mentira ficar vermelha.
        Assert.Contains(
            $"de {CriteriosDaRede.ParlamentaresMinimos} a {CriteriosDaRede.ParlamentaresMaximos}",
            CriteriosDaRede.PorQueEsteCorte,
            StringComparison.Ordinal);

        Assert.Contains("R$ 50 mil", CriteriosDaRede.PorQueEsteCorte, StringComparison.Ordinal);
    }

    [Fact]
    public void A_explicacao_nomeia_o_que_ficou_de_fora()
    {
        // O leitor precisa saber o que NAO esta' no desenho. Um mapa sem companhias aereas
        // que nao diz isso parece completo.
        Assert.Contains("companhias aéreas", CriteriosDaRede.PorQueEsteCorte, StringComparison.Ordinal);
    }
}
