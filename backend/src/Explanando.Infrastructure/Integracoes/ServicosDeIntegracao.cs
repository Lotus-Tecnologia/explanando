using Explanando.Application.Ingestao;
using Explanando.Infrastructure.Integracoes.BrasilApi;
using Explanando.Infrastructure.Integracoes.Camara;
using Microsoft.Extensions.DependencyInjection;

namespace Explanando.Infrastructure.Integracoes;

/// <summary>
/// Registra os clientes das fontes externas com politica de resiliencia.
///
/// Os numeros aqui vieram de medicao na Etapa 01, nao de suposicao:
///
/// - <b>Pool de conexoes importa mais que retry.</b> 20 requisicoes sequenciais abrindo
///   conexao nova custaram ~2,6 s cada; 12 requisicoes concorrentes reaproveitando conexao
///   custaram 1,95 s no total. O ganho de 16x veio do <c>IHttpClientFactory</c>, nao de
///   politica nenhuma.
/// - <b>Nenhum 429 foi observado</b> em nenhuma das duas fontes. A concorrencia contida
///   abaixo e' cortesia com servico publico e comunitario, nao necessidade tecnica.
/// </summary>
public static class ServicosDeIntegracao
{
    /// <summary>
    /// Teto de chamadas simultaneas por fonte. Baixo de proposito: a Camara e a BrasilAPI
    /// sao servicos publicos e gratuitos, e o gargalo da ingestao nao esta aqui.
    /// </summary>
    public const int ConcorrenciaMaxima = 6;

    /// <summary>
    /// Intervalo minimo entre consultas a BrasilAPI.
    ///
    /// Calibrado pela primeira coleta real: dois deputados produziram 166 respostas 429.
    /// A medicao da Etapa 01, que nao viu nenhum, usou oito requisicoes — poucas demais
    /// para revelar um limite medido por janela de tempo.
    /// </summary>
    public static TimeSpan IntervaloEntreConsultasDeCnpj { get; } = TimeSpan.FromMilliseconds(400);

    public static IServiceCollection AdicionarIntegracoes(this IServiceCollection servicos)
    {
        servicos
            .AddHttpClient<IFonteDeParlamentares, ClienteCamara>(http =>
            {
                http.BaseAddress = new Uri("https://dadosabertos.camara.leg.br/api/v2/");
                http.Timeout = TimeSpan.FromSeconds(60);
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                http.DefaultRequestHeaders.Add("User-Agent", "Explanando/1.0 (+transparencia)");
            })
            .AddStandardResilienceHandler(opcoes =>
            {
                // A carga inicial e' de ~6.000 requisicoes; instabilidade pontual nao pode
                // derrubar a coleta inteira, mas insistir demais tambem nao ajuda.
                opcoes.Retry.MaxRetryAttempts = 4;
                opcoes.Retry.Delay = TimeSpan.FromSeconds(2);
                opcoes.Retry.UseJitter = true;

                opcoes.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                opcoes.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);

                // Fonte fora do ar: parar de bater evita esgotar o pool de threads
                // esperando por algo que nao vai responder.
                opcoes.CircuitBreaker.FailureRatio = 0.5;
                opcoes.CircuitBreaker.MinimumThroughput = 10;
                opcoes.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                opcoes.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });

        servicos
            .AddHttpClient<IFonteDeCadastroEmpresarial, ClienteBrasilApi>(http =>
            {
                http.BaseAddress = new Uri("https://brasilapi.com.br/api/");
                http.Timeout = TimeSpan.FromSeconds(45);
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                http.DefaultRequestHeaders.Add("User-Agent", "Explanando/1.0 (+transparencia)");
            })
            // Espaca as chamadas ANTES de sair: evitar o 429 e' melhor que remedia-lo com
            // retry, que so' reage depois de ja ter incomodado o servidor.
            .AddHttpMessageHandler(() => new LimitadorDeTaxaHandler(IntervaloEntreConsultasDeCnpj))
            .AddStandardResilienceHandler(opcoes =>
            {
                // Servico comunitario: menos insistencia e espera maior entre tentativas.
                opcoes.Retry.MaxRetryAttempts = 3;
                opcoes.Retry.Delay = TimeSpan.FromSeconds(3);
                opcoes.Retry.UseJitter = true;

                opcoes.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
                opcoes.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);

                opcoes.CircuitBreaker.FailureRatio = 0.5;
                opcoes.CircuitBreaker.MinimumThroughput = 10;
                opcoes.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                opcoes.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
            });

        return servicos;
    }
}
