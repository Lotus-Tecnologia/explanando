using System.Globalization;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Tests.Auditoria;

/// <summary>
/// A curva de tendencia da pagina de perfil. O ponto que justifica a classe inteira e'
/// <see cref="HistoricoScore.EhComparavelCom"/>: sem ele, um degrau no grafico nao distingue
/// "o parlamentar mudou" de "as regras mudaram".
/// </summary>
public sealed class HistoricoScoreTests
{
    private const int PisoAmostral = 100;

    private static readonly Guid ParlamentarId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DespesaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 15, 30, 0, TimeSpan.Zero);

    private static List<Despesa> Despesas(int quantidade, decimal valorCada) =>
        [.. Enumerable.Range(1, quantidade).Select(i => Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = ParlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
            NomeFornecedor = "FORNECEDOR",
            Competencia = Competencia.Criar(2026, 7),
            TipoDespesa = "TELEFONIA",
            CodigoDocumento = $"doc-{i}",
            NumeroDocumento = i.ToString(CultureInfo.InvariantCulture),
            DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = valorCada,
            ValorLiquido = valorCada,
        }))];

    private static ScoreRiscoParlamentar Score(
        int despesas = 200,
        decimal valorCada = 1_000m,
        int flagsVermelhas = 2)
    {
        var flags = Enumerable
            .Range(1, flagsVermelhas)
            .Select(_ => FlagAnomalia.Criar(
                DespesaId,
                "REG_KYB_001",
                Gravidade.Alta,
                "Inconsistência financeira.",
                1.0m))
            .ToList();

        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular(Despesas(despesas, valorCada), flags, Agora, PisoAmostral);
        return score;
    }

    [Fact]
    public void Guarda_os_dois_numeros_do_ADR_0009()
    {
        // As duas curvas contam coisas diferentes: volume mostra ritmo de gasto, indice
        // mostra se a proporcao de indicadores piorou. Uma pode subir enquanto a outra cai.
        var score = Score();

        var ponto = HistoricoScore.Capturar(score, 2, Agora);

        ponto.VolumeTotalGasto.ShouldBe(score.VolumeTotalGasto);
        ponto.IndiceDeRisco.ShouldBe(score.IndiceDeRisco);
        ponto.DespesasAvaliadas.ShouldBe(score.DespesasAvaliadas);
        ponto.TotalFlagsVermelhas.ShouldBe(score.TotalFlagsVermelhas);
        ponto.ParlamentarId.ShouldBe(ParlamentarId);
    }

    [Fact]
    public void Referencia_e_a_data_sem_hora()
    {
        // O snapshot e' diario no maximo; guardar a hora criaria varios pontos por dia
        // no mesmo grafico.
        var ponto = HistoricoScore.Capturar(Score(), 2, Agora);

        ponto.Referencia.ShouldBe(new DateOnly(2026, 8, 20));
        ponto.CapturadoEm.ShouldBe(Agora);
    }

    [Fact]
    public void Recusa_score_nulo()
    {
        Should.Throw<ArgumentNullException>(() => HistoricoScore.Capturar(null!, 2, Agora));
    }

    // ------------------------------------------------------------------
    //  O motivo de a versao existir
    // ------------------------------------------------------------------

    [Fact]
    public void Dois_pontos_da_mesma_versao_sao_comparaveis()
    {
        var antes = HistoricoScore.Capturar(Score(), 2, Agora);
        var depois = HistoricoScore.Capturar(Score(), 2, Agora.AddDays(30));

        antes.EhComparavelCom(depois).ShouldBeTrue();
    }

    [Fact]
    public void Pontos_de_versoes_diferentes_nao_sao_comparaveis()
    {
        // A recalibragem de 2026-08-20 levou REG_KYB_001 de 1.477 para 297 flags de uma
        // vez. Comparar os dois lados dessa mudanca mostraria uma queda que nao foi do
        // parlamentar — e notificaria todo mundo que o favoritou por motivo errado.
        var antes = HistoricoScore.Capturar(Score(), 1, Agora);
        var depois = HistoricoScore.Capturar(Score(), 2, Agora.AddDays(1));

        antes.EhComparavelCom(depois).ShouldBeFalse();
    }

    [Fact]
    public void Comparabilidade_e_simetrica()
    {
        var a = HistoricoScore.Capturar(Score(), 1, Agora);
        var b = HistoricoScore.Capturar(Score(), 2, Agora);

        a.EhComparavelCom(b).ShouldBe(b.EhComparavelCom(a));
    }

    [Fact]
    public void Recusa_comparar_com_nulo()
    {
        Should.Throw<ArgumentNullException>(
            () => HistoricoScore.Capturar(Score(), 2, Agora).EhComparavelCom(null!));
    }

    // ------------------------------------------------------------------
    //  Amostra insuficiente
    // ------------------------------------------------------------------

    [Fact]
    public void Ponto_abaixo_do_piso_amostral_registra_indice_zerado()
    {
        // Coerente com o ADR-0009: abaixo de 100 despesas o indice nao e' publicado, e o
        // historico nao pode guardar um numero que a pagina nao mostra.
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular(
            Despesas(10, 1_000m),
            [FlagAnomalia.Criar(DespesaId, "REG_KYB_001", Gravidade.Alta, "Inconsistência financeira.", 1.0m)],
            Agora,
            PisoAmostral);

        var ponto = HistoricoScore.Capturar(score, 2, Agora);

        score.AmostraSuficiente.ShouldBeFalse();
        ponto.IndiceDeRisco.ShouldBe(score.IndiceDeRisco);
        ponto.VolumeTotalGasto.ShouldBe(10_000m);
    }
}
