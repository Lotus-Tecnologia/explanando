namespace Explanando.Infrastructure.Integracoes;

/// <summary>
/// Espaça as requisições a uma fonte externa, garantindo um intervalo mínimo entre elas.
///
/// <para>Existe por causa de uma medição que se provou enganosa. Na Etapa 01, oito consultas
/// sequenciais à BrasilAPI não produziram nenhum <c>429</c>, e a conclusão registrada foi
/// "nenhum rate-limit observado". Na primeira coleta real, de apenas dois deputados, o log
/// acumulou 166 respostas <c>429</c>: oito requisições não revelam o limite de um serviço que
/// mede por janela de tempo.</para>
///
/// <para>Retry sozinho não resolve — ele reage depois de já ter incomodado o servidor, e sob
/// carga vira uma fila de tentativas que piora o congestionamento. O espaçamento evita o
/// <c>429</c> em vez de remediá-lo.</para>
///
/// <para>Serializa as chamadas de propósito: o gargalo aqui é cortesia com um serviço
/// comunitário gratuito, não vazão.</para>
/// </summary>
public sealed class LimitadorDeTaxaHandler(TimeSpan intervaloMinimo, TimeProvider? tempo = null)
    : DelegatingHandler
{
    private readonly TimeProvider _tempo = tempo ?? TimeProvider.System;
    private readonly SemaphoreSlim _porta = new(1, 1);
    private long _proximaLiberacao;

    public TimeSpan IntervaloMinimo { get; } = intervaloMinimo;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await _porta.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var agora = _tempo.GetTimestamp();
            var espera = _proximaLiberacao - agora;

            if (espera > 0)
            {
                var duracao = TimeSpan.FromSeconds((double)espera / _tempo.TimestampFrequency);
                await Task.Delay(duracao, _tempo, cancellationToken).ConfigureAwait(false);
            }

            _proximaLiberacao = _tempo.GetTimestamp()
                + (long)(IntervaloMinimo.TotalSeconds * _tempo.TimestampFrequency);
        }
        finally
        {
            _porta.Release();
        }

        var resposta = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A fonte pediu para esperar: obedecer é mais barato que insistir e ser bloqueado.
        if (resposta.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var pedido = resposta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
            await AdiarProximaAsync(pedido, cancellationToken).ConfigureAwait(false);
        }

        return resposta;
    }

    private async Task AdiarProximaAsync(TimeSpan atraso, CancellationToken cancelamento)
    {
        await _porta.WaitAsync(cancelamento).ConfigureAwait(false);

        try
        {
            _proximaLiberacao = _tempo.GetTimestamp()
                + (long)(atraso.TotalSeconds * _tempo.TimestampFrequency);
        }
        finally
        {
            _porta.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _porta.Dispose();
        }

        base.Dispose(disposing);
    }
}
