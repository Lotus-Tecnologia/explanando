using System.Net;
using Explanando.Infrastructure.Integracoes;
using Microsoft.Extensions.Time.Testing;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// O limitador nasceu de um defeito real: a medicao da Etapa 01 usou oito requisicoes e
/// concluiu "sem rate-limit", mas a primeira coleta real acumulou 166 respostas 429.
/// </summary>
public sealed class LimitadorDeTaxaTests
{
    private sealed class ContadorDeChamadas(HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public List<DateTimeOffset> Momentos { get; } = [];

        public TimeProvider? Tempo { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Momentos.Add(Tempo?.GetUtcNow() ?? DateTimeOffset.UtcNow);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    [Fact]
    public async Task Espaca_as_requisicoes_pelo_intervalo_configurado()
    {
        var tempo = new FakeTimeProvider();
        var interno = new ContadorDeChamadas { Tempo = tempo };
        var limitador = new LimitadorDeTaxaHandler(TimeSpan.FromMilliseconds(400), tempo)
        {
            InnerHandler = interno,
        };

        using var cliente = new HttpClient(limitador) { BaseAddress = new Uri("https://exemplo.test/") };

        var primeira = cliente.GetAsync("a");
        await primeira;

        var segunda = cliente.GetAsync("b");
        segunda.IsCompleted.ShouldBeFalse("a segunda tem de esperar o intervalo");

        tempo.Advance(TimeSpan.FromMilliseconds(400));
        await segunda;

        interno.Momentos.Count.ShouldBe(2);
        (interno.Momentos[1] - interno.Momentos[0]).ShouldBeGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(400));
    }

    [Fact]
    public async Task Primeira_requisicao_nao_espera()
    {
        var tempo = new FakeTimeProvider();
        var limitador = new LimitadorDeTaxaHandler(TimeSpan.FromSeconds(10), tempo)
        {
            InnerHandler = new ContadorDeChamadas { Tempo = tempo },
        };

        using var cliente = new HttpClient(limitador) { BaseAddress = new Uri("https://exemplo.test/") };

        var chamada = cliente.GetAsync("a");

        (await chamada).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resposta_429_adia_a_proxima_alem_do_intervalo_normal()
    {
        // Obedecer ao pedido do servidor e' mais barato que insistir e ser bloqueado.
        var tempo = new FakeTimeProvider();
        var interno = new ContadorDeChamadas(HttpStatusCode.TooManyRequests) { Tempo = tempo };
        var limitador = new LimitadorDeTaxaHandler(TimeSpan.FromMilliseconds(100), tempo)
        {
            InnerHandler = interno,
        };

        using var cliente = new HttpClient(limitador) { BaseAddress = new Uri("https://exemplo.test/") };

        await cliente.GetAsync("a");

        var segunda = cliente.GetAsync("b");

        tempo.Advance(TimeSpan.FromMilliseconds(100));
        segunda.IsCompleted.ShouldBeFalse("o intervalo normal nao basta depois de um 429");

        tempo.Advance(TimeSpan.FromSeconds(5));
        await segunda;

        interno.Momentos.Count.ShouldBe(2);
    }
}
