using Explanando.Domain.Regras;
using Shouldly;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// Caracterizacao das bordas exatas dos limiares (levantamento de 08/11/2026). Nao muda
/// comportamento: fixa o que o motor ja faz, para que uma troca de &lt; por &lt;= apareca.
/// </summary>
public sealed class BordasDasRegrasTests
{
    [Theory]
    [InlineData(7, 17, false)]
    [InlineData(7, 18, true)]
    [InlineData(12, 22, false)]
    [InlineData(12, 23, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    public void Recesso_comeca_e_termina_no_dia_certo(int mes, int dia, bool emRecesso)
    {
        CalendarioLegislativo.EmRecesso(new DateOnly(2026, mes, dia)).ShouldBe(emRecesso);
    }

    [Theory]
    [InlineData(89, 1)]
    [InlineData(90, 0)]
    public void Empresa_recem_nascida_vai_ate_89_dias(int dias, int flags)
    {
        var nota = new DateOnly(2026, 7, 6);

        new CenarioDeAvaliacao()
            .ComFornecedor(dataAbertura: nota.AddDays(-dias))
            .ComDespesa(5_000m, ano: 2026, mes: 7, dia: 6)
            .Rodar(new RegraEmpresaRecemNascida())
            .Count.ShouldBe(flags);
    }

    [Theory]
    [InlineData(81_000.00, 0)]
    [InlineData(81_000.01, 1)]
    public void Mei_so_passa_do_teto_acima_de_81_mil(decimal total, int flags)
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: true, opcaoPeloMei: new DateOnly(2020, 1, 1))
            .ComDespesa(total, ano: 2026, mes: 3)
            .Rodar(new RegraMeiAcimaDoTeto())
            .Count.ShouldBe(flags);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(3, 0)]
    public void Fracionamento_pede_quatro_notas_e_cinco_mil(int notas, int flags)
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();
        for (var i = 1; i <= notas; i++)
        {
            cenario.ComDespesa(5_000m / 4, mes: 7, dia: i, codigoDocumento: $"f-{i}");
        }

        cenario.Rodar(new RegraFracionamento()).Count.ShouldBe(flags);
    }

    [Fact]
    public void Pico_de_exatamente_tres_vezes_a_media_sinaliza()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();
        cenario.ComDespesa(1_000m, mes: 1).ComDespesa(1_000m, mes: 2).ComDespesa(1_000m, mes: 3);
        cenario.ComDespesa(3_000m, mes: 4);

        cenario.Rodar(new RegraPicoDeConsumo()).Count.ShouldBe(1);
    }
}
