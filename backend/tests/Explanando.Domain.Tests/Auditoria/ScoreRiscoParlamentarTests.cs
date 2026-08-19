using Explanando.Domain.Auditoria;

namespace Explanando.Domain.Tests.Auditoria;

public sealed class ScoreRiscoParlamentarTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DespesaId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static FlagAnomalia Flag(Gravidade gravidade, decimal confianca = 1.0m) =>
        FlagAnomalia.Criar(DespesaId, "REG_KYB_001", gravidade, "Inconsistência financeira.", confianca);

    [Fact]
    public void Nasce_zerado()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.ScoreCalculado.ShouldBe(0m);
        score.TotalFlagsVermelhas.ShouldBe(0);
    }

    [Fact]
    public void Soma_o_peso_das_flags()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.Recalcular([Flag(Gravidade.Alta), Flag(Gravidade.Baixa)], DateTimeOffset.UtcNow);

        score.ScoreCalculado.ShouldBe(4m, "3 (alta) + 1 (baixa), ambas com confiança total");
    }

    [Fact]
    public void Conta_apenas_as_vermelhas_no_total_de_vermelhas()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.Recalcular(
            [Flag(Gravidade.Alta), Flag(Gravidade.Alta), Flag(Gravidade.Media)],
            DateTimeOffset.UtcNow);

        score.TotalFlagsVermelhas.ShouldBe(2);
    }

    [Fact]
    public void Achado_incerto_pesa_menos_que_achado_certo()
    {
        var certo = ScoreRiscoParlamentar.Criar(ParlamentarId);
        var incerto = ScoreRiscoParlamentar.Criar(ParlamentarId);

        certo.Recalcular([Flag(Gravidade.Alta, 1.0m)], DateTimeOffset.UtcNow);
        incerto.Recalcular([Flag(Gravidade.Alta, 0.5m)], DateTimeOffset.UtcNow);

        incerto.ScoreCalculado.ShouldBeLessThan(certo.ScoreCalculado);
    }

    [Fact]
    public void Recalcular_substitui_o_valor_anterior_em_vez_de_acumular()
    {
        // A varredura roda de novo sobre o mesmo parlamentar. Acumular inflaria
        // o ranking a cada execucao, sem nenhum gasto novo ter existido.
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        var agora = DateTimeOffset.UtcNow;

        score.Recalcular([Flag(Gravidade.Alta)], agora);
        score.Recalcular([Flag(Gravidade.Alta)], agora);

        score.ScoreCalculado.ShouldBe(3m);
    }

    [Fact]
    public void Registra_quando_a_ultima_varredura_rodou()
    {
        // A UI destaca essa data: e' o que mostra ao cidadao se o dado esta fresco.
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        var momento = new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

        score.Recalcular([], momento);

        score.DataUltimaVarredura.ShouldBe(momento);
    }

    [Fact]
    public void Parlamentar_sem_flags_fica_com_score_zero()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.Recalcular([Flag(Gravidade.Alta)], DateTimeOffset.UtcNow);
        score.Recalcular([], DateTimeOffset.UtcNow);

        score.ScoreCalculado.ShouldBe(0m);
        score.TotalFlagsVermelhas.ShouldBe(0);
    }
}
