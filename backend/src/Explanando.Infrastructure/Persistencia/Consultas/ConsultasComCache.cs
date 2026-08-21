using System.Text.Json;
using Explanando.Application.Consultas;
using Microsoft.Extensions.Caching.Hybrid;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// Envolve as consultas com cache.
///
/// <para><b>Só o ranking é cacheado</b>, e é decisão de projeto, não de preguiça. O ranking é
/// a página que um link viral atinge: milhares de requisições idênticas para a mesma primeira
/// página, cada uma agregando 513 linhas. Perfil e despesas são consultas dispersas — cada
/// leitor abre um parlamentar diferente —, e cachear tudo encheria a memória com entradas de
/// uso único.</para>
///
/// <para>O dado muda uma vez por varredura, não de hora em hora, então a janela é generosa. O
/// custo de servir um ranking com minutos de atraso é zero; o de derrubar o banco num pico é
/// a página inteira fora do ar.</para>
///
/// <para>Decorador, e não cache dentro da consulta: quem lê SQL não precisa saber que existe
/// cache, e um teste da consulta continua medindo a consulta.</para>
/// </summary>
public sealed class ConsultasComCache(IConsultasPublicas interna, HybridCache cache)
    : IConsultasPublicas
{
    /// <summary>
    /// Quanto tempo o ranking fica em cache.
    ///
    /// Cinco minutos: a varredura roda uma vez por dia no fluxo normal, então qualquer valor
    /// entre minutos e horas serviria. Cinco mantém o dado fresco o bastante para que uma
    /// varredura sob demanda apareça rápido, sem deixar o pico bater no banco.
    /// </summary>
    public static TimeSpan JanelaDoRanking => TimeSpan.FromMinutes(5);

    public async Task<Pagina<ItemDoRanking>> RanquearAsync(
        OrdenacaoDoRanking ordenacao,
        int pagina,
        int itensPorPagina,
        CancellationToken cancelamento = default)
    {
        // A chave carrega os tres parametros: ordenacoes diferentes sao respostas
        // diferentes, e servir a errada seria pior que nao cachear.
        var chave = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"ranking:{ordenacao}:{pagina}:{itensPorPagina}");

        return await cache.GetOrCreateAsync(
            chave,
            async token => await interna
                .RanquearAsync(ordenacao, pagina, itensPorPagina, token)
                .ConfigureAwait(false),
            new HybridCacheEntryOptions
            {
                Expiration = JanelaDoRanking,
                LocalCacheExpiration = JanelaDoRanking,
            },
            cancellationToken: cancelamento).ConfigureAwait(false);
    }

    public Task<PerfilDoParlamentar?> ObterPerfilAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        interna.ObterPerfilAsync(parlamentarId, cancelamento);

    public Task<Pagina<ItemDeDespesa>> ListarDespesasAsync(
        Guid parlamentarId,
        FiltroDeDespesas filtro,
        CancellationToken cancelamento = default) =>
        interna.ListarDespesasAsync(parlamentarId, filtro, cancelamento);

    public Task<DespesaDetalhada?> ObterDespesaAsync(
        Guid despesaId,
        CancellationToken cancelamento = default) =>
        interna.ObterDespesaAsync(despesaId, cancelamento);
}
