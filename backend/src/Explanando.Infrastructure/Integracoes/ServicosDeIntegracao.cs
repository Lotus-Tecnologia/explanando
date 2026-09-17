using Explanando.Application.Ingestao;
using Explanando.Infrastructure.Integracoes.BrasilApi;
using Explanando.Infrastructure.Integracoes.Camara;
using Explanando.Infrastructure.Integracoes.Senado;
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
    /// Calibrado em duas rodadas reais. Sem limitador, dois deputados produziram 166
    /// respostas 429 — a medicao da Etapa 01, que nao viu nenhuma, usou oito requisicoes,
    /// poucas demais para revelar um limite medido por janela de tempo. Com 400ms, tres
    /// deputados ainda geraram 8 retries. Em 600ms o custo e' tempo, e o tempo aqui e'
    /// barato: o enriquecimento e' cacheado no banco e so' paga uma vez por CNPJ.
    /// </summary>
    public static TimeSpan IntervaloEntreConsultasDeCnpj { get; } = TimeSpan.FromMilliseconds(600);

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

        // Registrado pelo TIPO CONCRETO, e nao pela porta.
        //
        // `AddHttpClient<IFonteDeParlamentares, T>` liga a porta a um tipo so', e o ultimo
        // registro venceria — o Senado silenciosamente sequestraria a coleta da Camara. Quem
        // monta a cadeia do Senado e' o comando `coletar-senado`, explicitamente.
        servicos
            .AddHttpClient<ClienteDoSenado>(http =>
            {
                // Sem BaseAddress: sao dois servicos em hosts diferentes — o administrativo
                // publica a CEAPS, o legislativo publica o cadastro. Ver senado.md §1.
                //
                // O timeout e' maior que o da Camara porque a resposta e' maior: um ano
                // inteiro num payload so', 10 MB em 2025. Medido em 0,7 s, mas o custo de
                // errar para menos aqui e' a coleta morrer no meio do download.
                http.Timeout = TimeSpan.FromSeconds(120);
                http.DefaultRequestHeaders.Add("Accept", "application/json");
                http.DefaultRequestHeaders.Add("User-Agent", "Explanando/1.0 (+transparencia)");
            })
            .AddStandardResilienceHandler(opcoes =>
            {
                // Sao poucas requisicoes — quatro anos mais dois cadastros —, entao insistir
                // vale mais aqui do que na Camara: cada falha custa um ano inteiro de dado.
                opcoes.Retry.MaxRetryAttempts = 4;
                opcoes.Retry.Delay = TimeSpan.FromSeconds(3);
                opcoes.Retry.UseJitter = true;

                // 45s por tentativa: o maior payload medido sao 10 MB em 0,7 s, entao isto e'
                // sessenta vezes a folga. Nao adianta ser mais generoso — a janela de
                // amostragem do disjuntor precisa ser o DOBRO deste valor, e o proprio
                // framework recusa a configuracao na subida se nao for. Descoberto assim: com
                // 90s aqui e 120s abaixo, os 42 testes de ponta a ponta falharam em bloco com
                // OptionsValidationException, porque a API valida isso ao iniciar.
                opcoes.AttemptTimeout.Timeout = TimeSpan.FromSeconds(45);
                opcoes.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(6);

                // Limiar alto de propósito: com meia duzia de requisicoes na passada inteira,
                // o padrao de 10 nunca fecharia o circuito e duas falhas o abririam.
                opcoes.CircuitBreaker.FailureRatio = 0.9;
                opcoes.CircuitBreaker.MinimumThroughput = 5;
                opcoes.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
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
