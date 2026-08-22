using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Explanando.Infrastructure;

/// <summary>
/// Rastros e métricas, que o documento de Arquitetura §1.3 pede.
///
/// <para>O que se quer responder aqui é uma pergunta operacional concreta: <b>quando a página
/// demorar, o que segurou?</b> Foi o Postgres, foi a BrasilAPI, foi o nosso código. Sem o
/// rastro, a resposta vem de palpite — e a história deste projeto é feita de palpites que a
/// medição desmentiu.</para>
/// </summary>
public static class Observabilidade
{
    /// <summary>O caminho da sonda, que fica fora do rastro.</summary>
    private const string CaminhoDaSonda = "/saude";

    public static IServiceCollection AdicionarObservabilidade(
        this IServiceCollection servicos,
        IConfiguration configuracao,
        string nomeDoServico)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(configuracao);

        var destino = configuracao["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var paraConsole = configuracao.GetValue("Observabilidade:Console", false);
        var amostragem = configuracao.GetValue("Observabilidade:Amostragem", 1.0);

        servicos.AddOpenTelemetry()
            .ConfigureResource(recurso => recurso.AddService(
                serviceName: nomeDoServico,
                serviceVersion: typeof(Observabilidade).Assembly.GetName().Version?.ToString()))

            .WithTracing(rastro =>
            {
                /*
                  Amostragem configurável, com 100% de padrão.

                  Cem por cento é o certo no começo: o tráfego é baixo e cada rastro conta.
                  Num pico viral deixa de ser — só que aí não dá para reagir a tempo. O
                  número fica em configuração, e não em constante, exatamente para poder
                  baixar sem redeploy quando a hora chegar.
                */
                rastro.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(amostragem)));

                rastro.AddAspNetCoreInstrumentation(opcoes =>
                {
                    // A sonda dispara sem parar e não diz nada sobre ninguém. Rastreá-la
                    // afogaria a requisição de verdade no meio do ruído — e, num backend
                    // cobrado por span, seria pagar para guardar batimento cardíaco.
                    opcoes.Filter = contexto =>
                        !contexto.Request.Path.StartsWithSegments(CaminhoDaSonda);
                });

                /*
                  As chamadas que saem: Câmara, Senado e BrasilAPI.

                  Vale registrar o que foi conferido antes de ligar isto: nenhuma dessas URLs
                  carrega dado pessoal. A da Câmara leva o id público do deputado, a da
                  BrasilAPI leva CNPJ — que é dado de pessoa jurídica. O CPF do parlamentar
                  nunca entrou no sistema (Etapa 01), e o do sócio chega mascarado da fonte.
                  Se algum dia uma URL passar a levar dado pessoal, esta instrumentação
                  precisa de filtro antes.
                */
                rastro.AddHttpClientInstrumentation();

                // O rastro do Npgsql é o que responde "qual consulta segurou a requisição".
                // Sem ele, "o banco demorou" é o fim da investigação em vez do começo.
                // Qualificado: `AddNpgsql` sem namespace resolve para a extensao do EF
                // Core sobre IServiceCollection, que pede string de conexao. Nomes iguais em
                // assemblies diferentes, e o compilador escolhe o errado em silencio.
                Npgsql.TracerProviderBuilderExtensions.AddNpgsql(rastro);

                Exportar(rastro, destino, paraConsole);
            })

            .WithMetrics(metricas =>
            {
                metricas.AddAspNetCoreInstrumentation();
                metricas.AddHttpClientInstrumentation();

                // Coletor de lixo, memória e thread pool: no Cloud Run, onde se paga por
                // tempo de contêiner, é o que separa "está lento" de "está sem recurso".
                metricas.AddRuntimeInstrumentation();

                Exportar(metricas, destino, paraConsole);
            });

        return servicos;
    }

    /// <summary>
    /// Para onde o rastro vai.
    ///
    /// <para>Sem destino configurado, <b>nenhum exportador é registrado</b>. Registrar um
    /// OTLP apontando para o vazio faria a aplicação tentar entregar, falhar e repetir, em
    /// segundo plano e para sempre — gastando CPU num contêiner cobrado por tempo, sem
    /// entregar nada a ninguém.</para>
    /// </summary>
    private static void Exportar(TracerProviderBuilder rastro, string? destino, bool console)
    {
        if (!string.IsNullOrWhiteSpace(destino))
        {
            rastro.AddOtlpExporter();
        }

        if (console)
        {
            rastro.AddConsoleExporter();
        }
    }

    private static void Exportar(MeterProviderBuilder metricas, string? destino, bool console)
    {
        if (!string.IsNullOrWhiteSpace(destino))
        {
            metricas.AddOtlpExporter();
        }

        if (console)
        {
            metricas.AddConsoleExporter();
        }
    }
}
