using Explanando.Domain.Cotas;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Semente;

/// <summary>
/// Valores mensais da Cota para o Exercicio da Atividade Parlamentar (CEAP), por UF.
///
/// <para><b>Procedencia.</b> Extraidos em 2026-08-19 do Guia para Jornalistas do Portal da
/// Camara dos Deputados (<c>www2.camara.leg.br/comunicacao/assessoria-de-imprensa/
/// guia-para-jornalistas/cota-parlamentar</c>). Nenhuma API publica esta tabela — ela nao
/// esta nos dados abertos e o mapa do portal de transparencia a renderiza por JavaScript,
/// sem expo-la no HTML.</para>
///
/// <para><b>Conferencia.</b> Os 27 valores foram extraidos duas vezes de forma independente,
/// com resultados identicos, e os extremos (DF R$ 41.612,55 e RR R$ 58.474,70) batem com o
/// intervalo publicado por fonte jornalistica separada. A ordem tambem faz sentido fisico:
/// o DF tem o menor valor por dispensar deslocamento aereo, e RR o maior por ser o estado
/// mais distante de Brasilia.</para>
///
/// <para><b>Limitacao conhecida — precisa de confirmacao.</b> A fonte publica os valores
/// vigentes, mas nao a data em que passaram a valer, e nao traz o historico. Assumimos
/// <see cref="VigenciaAssumida"/> como inicio.</para>
///
/// <para>A consequencia dessa suposicao e' assimetrica, e por isso ela e' aceitavel: se o
/// reajuste tiver ocorrido depois de janeiro de 2026, as despesas dos meses anteriores serao
/// comparadas a um teto <b>mais alto</b> que o real, e REG_TX_002 vai deixar de sinalizar
/// alguns casos. Erra para menos, nunca para mais — que e' o lado certo de errar quando o
/// resultado aparece com nome e foto numa pagina publica.</para>
///
/// <para>Despesas anteriores a esta vigencia ficam <b>sem teto configurado</b>, e REG_TX_002
/// se abstem sobre elas. Os valores de 2023 a 2025 precisam ser levantados nos Atos da Mesa
/// correspondentes para que a regra alcance o restante da legislatura 57.</para>
/// </summary>
public static class SementeDeLimitesCota
{
    /// <summary>
    /// Inicio de vigencia assumido. <b>Nao confirmado na fonte</b> — ver a nota da classe.
    /// </summary>
    public static DateOnly VigenciaAssumida { get; } = new(2026, 1, 1);

    /// <summary>Os 27 valores, em ordem alfabetica de UF.</summary>
    public static IReadOnlyList<(string Uf, decimal ValorMensal)> Valores { get; } =
    [
        ("AC", 57_359.87m),
        ("AL", 53_164.36m),
        ("AM", 56_151.46m),
        ("AP", 55_929.26m),
        ("BA", 50_965.29m),
        ("CE", 54_879.34m),
        ("DF", 41_612.55m),
        ("ES", 49_160.15m),
        ("GO", 46_979.73m),
        ("MA", 54_537.99m),
        ("MG", 47_645.91m),
        ("MS", 52_707.93m),
        ("MT", 51_439.83m),
        ("PA", 54_624.17m),
        ("PB", 54_402.48m),
        ("PE", 53_997.81m),
        ("PI", 53_195.84m),
        ("PR", 50_807.19m),
        ("RJ", 47_267.41m),
        ("RN", 55_198.09m),
        ("RO", 56_267.90m),
        ("RR", 58_474.70m),
        ("RS", 53_086.78m),
        ("SC", 51_951.42m),
        ("SE", 52_248.86m),
        ("SP", 48_727.46m),
        ("TO", 51_525.80m),
    ];

    /// <summary>
    /// Grava os limites que ainda nao existem para <see cref="VigenciaAssumida"/>.
    /// Idempotente: rodar de novo nao duplica nem sobrescreve.
    /// </summary>
    public static async Task<int> AplicarAsync(
        ExplanandoDbContext contexto,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var jaGravados = await contexto.LimitesCota
            .Where(limite => limite.VigenciaInicio == VigenciaAssumida)
            .Select(limite => limite.Uf)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var existentes = jaGravados.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inseridos = 0;

        foreach (var (uf, valorMensal) in Valores)
        {
            if (existentes.Contains(uf))
            {
                continue;
            }

            contexto.LimitesCota.Add(LimiteCota.Criar(uf, valorMensal, VigenciaAssumida));
            inseridos++;
        }

        if (inseridos > 0)
        {
            await contexto.SaveChangesAsync(cancelamento).ConfigureAwait(false);
        }

        return inseridos;
    }
}
