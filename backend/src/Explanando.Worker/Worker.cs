using Explanando.Application.Ciclo;
using Explanando.Domain.Regras;

namespace Explanando.Worker;

/// <summary>
/// A passada mensal do <c>ADR-0010</c> §4, quando o worker roda como serviço.
///
/// <para><b>Este não é o formato preferido de produção.</b> O documento de infraestrutura
/// aposta em Cloud Run com <i>scale to zero</i> — contêiner desligado quando não há tráfego,
/// custo zero —, e um serviço que fica de pé esperando um mês passar contradiz isso
/// diretamente. Em produção, quem dispara é um agendador externo chamando
/// <c>dotnet run -- ciclo-mensal</c>, que sobe, trabalha e morre.</para>
///
/// <para>Este caminho existe para uma máquina que já está ligada de qualquer jeito. Ele
/// acorda de tempos em tempos e <b>pergunta ao banco</b> se o ciclo venceu, em vez de contar
/// o tempo por conta própria: contar internamente perderia a conta a cada reinício do
/// contêiner, e no Cloud Run reiniciar é rotina, não exceção.</para>
/// </summary>
public sealed partial class Worker(
    IServiceScopeFactory fabricaDeEscopos,
    ILogger<Worker> logger) : BackgroundService
{
    /// <summary>
    /// De quanto em quanto tempo se pergunta ao banco. Não é o intervalo do ciclo — é a
    /// frequência da pergunta, e ela é barata: uma consulta de agregação.
    /// </summary>
    private static readonly TimeSpan IntervaloDaPergunta = TimeSpan.FromHours(6);

    private const int LegislaturaCorrente = 57;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServicoIniciado(logger, IntervaloDaPergunta.TotalHours);

        using var temporizador = new PeriodicTimer(IntervaloDaPergunta);

        try
        {
            do
            {
                await TentarCicloAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Encerramento solicitado pelo host: caminho normal, nao e' falha.
        }

        ServicoEncerrado(logger);
    }

    private async Task TentarCicloAsync(CancellationToken cancelamento)
    {
        // Escopo por passada: o DbContext é scoped, e segurar um pelo tempo de vida do
        // serviço acumularia rastreamento de tudo que já passou por ele.
        using var escopo = fabricaDeEscopos.CreateScope();

        var ciclo = escopo.ServiceProvider.GetRequiredService<ServicoDeCicloMensal>();

        try
        {
            var resultado = await ciclo
                .ExecutarSeVencidoAsync(
                    LegislaturaCorrente,
                    CicloDeAtualizacao.IntervaloPadrao,
                    cancelamento)
                .ConfigureAwait(false);

            if (!resultado.Executou)
            {
                BaseFresca(logger);
                return;
            }

            CicloConcluido(
                logger,
                resultado.ParlamentaresVarridos,
                resultado.FlagsEmitidas,
                resultado.Coleta?.DespesasNovas ?? 0);

            // Coleta incompleta não pode passar em silêncio. A análise é válida sobre o que
            // está no banco, mas o dado ficou mais velho do que o resultado sugere — e foi
            // exatamente esse tipo de sucesso enganoso que produziu o incidente de
            // 2026-08-21, quando a fonte respondeu 200 com lista vazia 497 vezes.
            if (resultado.ColetaIncompleta)
            {
                ColetaIncompleta(logger, resultado.Coleta?.Falhas.Count ?? 0);
            }
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Uma passada que falha não pode derrubar o serviço: a próxima tenta de novo, e
            // o checkpoint garante que ela não recomeça do zero.
            CicloFalhou(logger, erro);
        }
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information,
        Message = "Servico de atualizacao iniciado. Consulta o vencimento a cada {Horas}h.")]
    private static partial void ServicoIniciado(ILogger logger, double horas);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Debug,
        Message = "Base dentro do intervalo: nada a atualizar nesta passada.")]
    private static partial void BaseFresca(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Ciclo mensal concluido: {Parlamentares} parlamentares varridos, "
            + "{Flags} indicadores, {DespesasNovas} despesas novas.")]
    private static partial void CicloConcluido(
        ILogger logger, int parlamentares, int flags, int despesasNovas);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "A coleta do ciclo ficou INCOMPLETA ({Falhas} falhas). A varredura rodou "
            + "sobre o que existe, mas o dado esta mais velho do que o resultado sugere.")]
    private static partial void ColetaIncompleta(ILogger logger, int falhas);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error,
        Message = "O ciclo mensal falhou. A proxima passada tenta de novo.")]
    private static partial void CicloFalhou(ILogger logger, Exception erro);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information,
        Message = "Servico de atualizacao encerrado.")]
    private static partial void ServicoEncerrado(ILogger logger);
}
