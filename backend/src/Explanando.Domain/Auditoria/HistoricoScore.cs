using Explanando.Domain.Comum;

namespace Explanando.Domain.Auditoria;

/// <summary>
/// Fotografia periódica dos números de um parlamentar, para a curva de tendência da página
/// de perfil (documento de Negócios, §5).
///
/// <para>Guarda os <b>dois</b> números do <c>ADR-0009</c>, porque as duas curvas contam
/// coisas diferentes: a de volume mostra ritmo de gasto, a de índice mostra se a proporção
/// de indicadores está piorando. Uma pode subir enquanto a outra cai.</para>
///
/// <para>Guarda também a <see cref="VersaoDoMotor"/>. Sem ela, um degrau no gráfico seria
/// indistinguível entre "o parlamentar mudou" e "as regras mudaram" — e a recalibragem de
/// 2026-08-20 levou uma regra de 1.477 para 297 flags de uma vez.</para>
/// </summary>
public sealed class HistoricoScore : Entidade
{
    private HistoricoScore()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>Data de referência da fotografia, sem hora: o snapshot é diário no máximo.</summary>
    public DateOnly Referencia { get; private set; }

    public decimal VolumeTotalGasto { get; private set; }

    public decimal IndiceDeRisco { get; private set; }

    public int DespesasAvaliadas { get; private set; }

    public int TotalFlagsVermelhas { get; private set; }

    /// <summary>Versão do motor que produziu estes números. Ver a nota da classe.</summary>
    public int VersaoDoMotor { get; private set; }

    public DateTimeOffset CapturadoEm { get; private set; }

    public static HistoricoScore Capturar(
        ScoreRiscoParlamentar score,
        int versaoDoMotor,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(score);

        return new HistoricoScore
        {
            ParlamentarId = score.ParlamentarId,
            Referencia = DateOnly.FromDateTime(momento.UtcDateTime),
            VolumeTotalGasto = score.VolumeTotalGasto,
            IndiceDeRisco = score.IndiceDeRisco,
            DespesasAvaliadas = score.DespesasAvaliadas,
            TotalFlagsVermelhas = score.TotalFlagsVermelhas,
            VersaoDoMotor = versaoDoMotor,
            CapturadoEm = momento,
        };
    }

    /// <summary>
    /// Regrava o ponto do dia com números novos.
    ///
    /// Existe porque a varredura roda mais de uma vez no mesmo dia — cada solicitação paga
    /// dispara uma — e o índice único é <c>(parlamentar_id, referência)</c>. Sem isto, a
    /// segunda varredura do dia falharia por chave duplicada, ou empilharia pontos sobre a
    /// mesma data e a curva teria degraus dentro de um dia só.
    /// </summary>
    public void Regravar(ScoreRiscoParlamentar score, int versaoDoMotor, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versaoDoMotor);

        if (score.ParlamentarId != ParlamentarId)
        {
            throw new ArgumentException(
                $"Este ponto e' do parlamentar {ParlamentarId}; o score e' de "
                + $"{score.ParlamentarId}.",
                nameof(score));
        }

        VolumeTotalGasto = score.VolumeTotalGasto;
        IndiceDeRisco = score.IndiceDeRisco;
        DespesasAvaliadas = score.DespesasAvaliadas;
        TotalFlagsVermelhas = score.TotalFlagsVermelhas;
        VersaoDoMotor = versaoDoMotor;
        CapturadoEm = momento;
    }

    /// <summary>
    /// Se este ponto é comparável com outro — ou seja, se a diferença entre eles diz algo
    /// sobre o parlamentar em vez de sobre o software.
    /// </summary>
    public bool EhComparavelCom(HistoricoScore outro)
    {
        ArgumentNullException.ThrowIfNull(outro);

        return VersaoDoMotor == outro.VersaoDoMotor;
    }
}
