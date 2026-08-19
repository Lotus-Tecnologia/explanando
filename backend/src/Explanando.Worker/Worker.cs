namespace Explanando.Worker;

/// <summary>
/// Esqueleto do worker de varredura. A logica real de ingestao entra na Etapa 3;
/// por enquanto ele so' sobe, registra que esta vivo e encerra de forma limpa.
/// </summary>
public sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan IntervaloDeBatimento = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WorkerIniciado(logger);

        using var temporizador = new PeriodicTimer(IntervaloDeBatimento);

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                WorkerOcioso(logger);
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento solicitado pelo host: caminho normal, nao e' falha.
        }

        WorkerEncerrado(logger);
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Worker de varredura iniciado.")]
    private static partial void WorkerIniciado(ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "Worker ocioso: nenhuma fila conectada ainda (previsto para a Etapa 3).")]
    private static partial void WorkerOcioso(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Worker de varredura encerrado.")]
    private static partial void WorkerEncerrado(ILogger logger);
}
