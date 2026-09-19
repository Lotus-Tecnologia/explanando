using System.Globalization;
using Explanando.Application.Consultas;
using Microsoft.Extensions.Caching.Hybrid;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Cache por cima das leituras do grafo.
///
/// <para><b>Aqui o cache não é otimização, é requisito.</b> Montar o mapa custa carregar
/// mais de cem mil arestas e contar descendentes de cada nó; servir isso a cada visita
/// tornaria a página inviável no plano gratuito. E o dado é o oposto de volátil: a
/// estrutura do Estado muda em escala de semanas, e a coleta roda uma vez por mês.</para>
///
/// <para>Por isso a validade é <b>longa</b> — seis horas —, e não os minutos do ranking de
/// despesas. O pior caso é alguém ver uma reforma administrativa seis horas depois de nós.</para>
/// </summary>
public sealed class ConsultasDoEstadoComCache(IConsultasDoEstado interno, HybridCache cache)
    : IConsultasDoEstado
{
    private static readonly HybridCacheEntryOptions Validade = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6),
    };

    public ValueTask<MapaDoEstado> ObterMapaValueAsync(CancellationToken cancelamento) =>
        cache.GetOrCreateAsync(
            "estado:mapa",
            interno,
            static (consultas, ct) => new ValueTask<MapaDoEstado>(consultas.ObterMapaAsync(ct)),
            Validade,
            cancellationToken: cancelamento);

    public async Task<MapaDoEstado> ObterMapaAsync(CancellationToken cancelamento = default) =>
        await ObterMapaValueAsync(cancelamento).ConfigureAwait(false);

    public async Task<IReadOnlyList<NoDoEstado>> ObterSubarvoreAsync(
        int codigo,
        CancellationToken cancelamento = default) =>
        await cache.GetOrCreateAsync(
            string.Create(CultureInfo.InvariantCulture, $"estado:filhos:{codigo}"),
            (Interno: interno, Codigo: codigo),
            static (estado, ct) =>
                new ValueTask<IReadOnlyList<NoDoEstado>>(
                    estado.Interno.ObterSubarvoreAsync(estado.Codigo, ct)),
            Validade,
            cancellationToken: cancelamento)
            .ConfigureAwait(false);

    public async Task<NoDoEstado?> ObterUnidadeAsync(
        int codigo,
        CancellationToken cancelamento = default) =>
        await cache.GetOrCreateAsync(
            string.Create(CultureInfo.InvariantCulture, $"estado:unidade:{codigo}"),
            (Interno: interno, Codigo: codigo),
            static (estado, ct) =>
                new ValueTask<NoDoEstado?>(estado.Interno.ObterUnidadeAsync(estado.Codigo, ct)),
            Validade,
            cancellationToken: cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MudancaDoEstado>> ListarMudancasAsync(
        int quantidade,
        CancellationToken cancelamento = default) =>
        await cache.GetOrCreateAsync(
            string.Create(CultureInfo.InvariantCulture, $"estado:mudancas:{quantidade}"),
            (Interno: interno, Quantidade: quantidade),
            static (estado, ct) =>
                new ValueTask<IReadOnlyList<MudancaDoEstado>>(
                    estado.Interno.ListarMudancasAsync(estado.Quantidade, ct)),
            Validade,
            cancellationToken: cancelamento)
            .ConfigureAwait(false);
}
