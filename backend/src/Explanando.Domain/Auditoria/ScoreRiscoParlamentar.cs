using Explanando.Domain.Comum;

namespace Explanando.Domain.Auditoria;

/// <summary>
/// Score agregado de risco de um parlamentar. Relacao 1:1 com <c>Parlamentar</c>.
///
/// O valor e' sempre <b>recalculado do zero</b> a partir das flags vigentes, nunca
/// incrementado: a varredura roda repetidas vezes sobre o mesmo parlamentar, e acumular
/// inflaria o ranking sem que nenhum gasto novo tivesse existido.
/// </summary>
public sealed class ScoreRiscoParlamentar : Entidade
{
    private ScoreRiscoParlamentar()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>Soma dos pesos das flags: gravidade ponderada pelo grau de confianca.</summary>
    public decimal ScoreCalculado { get; private set; }

    public int TotalFlagsVermelhas { get; private set; }

    /// <summary>Exibida em destaque na pagina publica — mostra se o dado esta fresco.</summary>
    public DateTimeOffset DataUltimaVarredura { get; private set; }

    public static ScoreRiscoParlamentar Criar(Guid parlamentarId) => new()
    {
        ParlamentarId = parlamentarId,
        ScoreCalculado = 0m,
        TotalFlagsVermelhas = 0,
        DataUltimaVarredura = DateTimeOffset.MinValue,
    };

    public void Recalcular(IReadOnlyCollection<FlagAnomalia> flags, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(flags);

        var total = 0m;
        var vermelhas = 0;

        foreach (var flag in flags)
        {
            total += flag.Peso;

            if (flag.EhVermelha)
            {
                vermelhas++;
            }
        }

        ScoreCalculado = total;
        TotalFlagsVermelhas = vermelhas;
        DataUltimaVarredura = momento;
    }
}
