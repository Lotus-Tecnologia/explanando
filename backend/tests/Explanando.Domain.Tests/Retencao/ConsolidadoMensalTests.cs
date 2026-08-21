using System.Globalization;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Retencao;

namespace Explanando.Domain.Tests.Retencao;

public sealed class JanelaDeRetencaoTests
{
    private static readonly DateOnly Hoje = new(2026, 8, 20);

    [Fact]
    public void A_janela_do_v1_guarda_tudo()
    {
        JanelaDeRetencao.Ilimitada.GuardaTudo.ShouldBeTrue();
        JanelaDeRetencao.Ilimitada.PrimeiraCompetenciaPreservada(Hoje).ShouldBeNull();
    }

    [Fact]
    public void Janela_de_vinte_e_quatro_meses_preserva_do_mes_corrente_para_tras()
    {
        // 24 meses contando o corrente: agosto/2026 volta a setembro/2024, não a
        // agosto/2024. Errar por um mês aqui descartaria um mês de gasto público.
        JanelaDeRetencao.De(24)
            .PrimeiraCompetenciaPreservada(Hoje)
            .ShouldBe(Competencia.Criar(2024, 9));
    }

    [Fact]
    public void Janela_de_um_mes_preserva_so_o_mes_corrente()
    {
        JanelaDeRetencao.De(1)
            .PrimeiraCompetenciaPreservada(Hoje)
            .ShouldBe(Competencia.Criar(2026, 8));
    }

    [Fact]
    public void Janela_atravessa_a_virada_do_ano()
    {
        JanelaDeRetencao.De(3)
            .PrimeiraCompetenciaPreservada(new DateOnly(2026, 1, 15))
            .ShouldBe(Competencia.Criar(2025, 11));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recusa_janela_nao_positiva(int meses)
    {
        // Zero meses guardaria nada e apagaria a base inteira na primeira poda.
        Should.Throw<ArgumentOutOfRangeException>(() => JanelaDeRetencao.De(meses));
    }
}

/// <summary>
/// O total que sobrevive à poda. Consolidação mensal, e não um total único, porque uma
/// linha por mês custa quase nada e preserva a curva de gasto.
/// </summary>
public sealed class ConsolidadoMensalTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Competencia Julho = Competencia.Criar(2026, 7);
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static int _sequencia;

    private static Despesa Nota(decimal documento, decimal liquido, decimal glosa = 0m) =>
        Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = ParlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
            NomeFornecedor = "FORNECEDOR",
            Competencia = Julho,
            TipoDespesa = "TELEFONIA",
            CodigoDocumento = $"c-{Interlocked.Increment(ref _sequencia)}",
            NumeroDocumento = _sequencia.ToString(CultureInfo.InvariantCulture),
            DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = documento,
            ValorLiquido = liquido,
            ValorGlosa = glosa,
        });

    private static ConsolidadoMensal Consolidado(params Despesa[] despesas) =>
        ConsolidadoMensal.Consolidar(ParlamentarId, Julho, despesas, Agora);

    [Fact]
    public void Soma_os_tres_valores_e_conta_as_notas()
    {
        var consolidado = Consolidado(
            Nota(1_000m, 1_000m),
            Nota(500m, 400m, glosa: 100m));

        consolidado.TotalDocumento.ShouldBe(1_500m);
        consolidado.TotalLiquido.ShouldBe(1_400m);
        consolidado.TotalGlosa.ShouldBe(100m);
        consolidado.QuantidadeDeDespesas.ShouldBe(2);
        consolidado.Competencia.ShouldBe(Julho);
    }

    [Fact]
    public void Estorno_abate_o_total_em_vez_de_inflar()
    {
        // O defeito que a primeira coleta real revelou: um bilhete de R$ 1.846 comprado e
        // cancelado não pode aparecer como R$ 1.846 gastos.
        var consolidado = Consolidado(
            Nota(1_846m, 1_846m),
            Nota(-1_846m, -1_846m));

        consolidado.TotalLiquido.ShouldBe(0m);
        consolidado.QuantidadeDeDespesas.ShouldBe(2);
        consolidado.QuantidadeDeEstornos.ShouldBe(1);
    }

    [Fact]
    public void Nasce_com_o_detalhe_disponivel()
    {
        Consolidado(Nota(100m, 100m)).DetalheDisponivel.ShouldBeTrue();
    }

    [Fact]
    public void Mes_sem_despesa_consolida_em_zero_sem_quebrar()
    {
        var vazio = Consolidado();

        vazio.TotalLiquido.ShouldBe(0m);
        vazio.QuantidadeDeDespesas.ShouldBe(0);
    }

    [Fact]
    public void Recalcular_refaz_do_zero_em_vez_de_somar()
    {
        // A consolidação roda de novo a cada varredura sobre os mesmos meses. Acumular
        // inflaria o volume sem que gasto novo algum tivesse existido.
        var consolidado = Consolidado(Nota(1_000m, 1_000m));

        consolidado.Recalcular([Nota(300m, 300m)], Agora.AddDays(1));

        consolidado.TotalLiquido.ShouldBe(300m);
        consolidado.QuantidadeDeDespesas.ShouldBe(1);
        consolidado.AtualizadoEm.ShouldBe(Agora.AddDays(1));
    }

    [Fact]
    public void Descartar_o_detalhe_preserva_os_totais()
    {
        // Os totais viram a única coisa que existe daquele mês. Se sumissem junto, a poda
        // apagaria gasto público em vez de apagar detalhe.
        var consolidado = Consolidado(Nota(1_000m, 900m, glosa: 100m));

        consolidado.MarcarDetalheDescartado(Agora.AddDays(2));

        consolidado.DetalheDisponivel.ShouldBeFalse();
        consolidado.TotalLiquido.ShouldBe(900m);
        consolidado.TotalGlosa.ShouldBe(100m);
        consolidado.QuantidadeDeDespesas.ShouldBe(1);
        consolidado.AtualizadoEm.ShouldBe(Agora.AddDays(2));
    }
}
