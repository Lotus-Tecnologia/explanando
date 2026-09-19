using Explanando.Application.Consultas;
using Microsoft.Extensions.Caching.Hybrid;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Cache por cima do mapa de fornecedores.
///
/// <para><b>Requisito, e não otimização — pelo mesmo motivo do grafo do Estado, e ainda
/// mais forte aqui.</b> Montar o mapa agrupa as 711 mil despesas por CNPJ. É uma varredura
/// da tabela inteira, e servi-la a cada visita derrubaria o banco no plano gratuito.</para>
///
/// <para>A validade é de seis horas, alinhada com a do grafo. O dado só muda quando uma
/// ingestão traz notas novas, o que acontece em escala de dias — o pior caso é um
/// fornecedor novo aparecer no mapa seis horas depois de entrar na base.</para>
///
/// <para><b>As duas leituras compartilham a mesma entrada de cache</b>, porque
/// <c>ObterLigacoesAsync</c> é uma filtragem do mapa inteiro. Dar chave própria a cada
/// parlamentar guardaria seiscentas cópias do mesmo cálculo.</para>
/// </summary>
public sealed class ConsultasDaRedeComCache(IConsultasDaRede interno, HybridCache cache)
    : IConsultasDaRede
{
    private static readonly HybridCacheEntryOptions Validade = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6),
    };

    public async Task<MapaDaRede> ObterMapaAsync(CancellationToken cancelamento = default) =>
        await cache.GetOrCreateAsync(
            "rede:mapa",
            interno,
            static (consultas, ct) => new ValueTask<MapaDaRede>(consultas.ObterMapaAsync(ct)),
            Validade,
            cancellationToken: cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<LigacaoDaRede>> ObterLigacoesAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        var mapa = await ObterMapaAsync(cancelamento).ConfigureAwait(false);

        return [.. mapa.Ligacoes.Where(ligacao => ligacao.Parlamentares.Contains(parlamentarId))];
    }
}
