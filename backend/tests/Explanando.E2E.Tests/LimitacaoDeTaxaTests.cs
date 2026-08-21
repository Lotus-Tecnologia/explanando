using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Explanando.E2E.Tests;

/// <summary>
/// A API pública não tem autenticação, e até aqui só o teto de 200 itens por página a
/// protegia. Fora do <c>localhost</c> isso deixa de ser suficiente: no Cloud Run com
/// cobrança por tempo de contêiner, um raspador de terceiros vira fatura — e "custo fixo
/// zero" é premissa do projeto, não meta.
///
/// <para>Sobe a API com um teto que cabe num teste, apontando para o mesmo Postgres da suíte
/// principal.</para>
/// </summary>
public sealed class ApiComTetoBaixo(string conexao, bool atrasDeProxy = false)
    : WebApplicationFactory<Program>
{
    public const int Teto = 3;
    public const string ChaveInterna = "chave-de-teste";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Explanando", conexao);
        builder.UseEnvironment("Development");

        builder.UseSetting("RateLimit:Capacidade", Teto.ToString(Cultura));
        builder.UseSetting("RateLimit:PorMinuto", Teto.ToString(Cultura));
        builder.UseSetting("RateLimit:ChaveInterna", ChaveInterna);
        builder.UseSetting("RateLimit:AtrasDeProxy", atrasDeProxy ? "true" : "false");
    }

    private static readonly System.Globalization.CultureInfo Cultura =
        System.Globalization.CultureInfo.InvariantCulture;
}

[Collection(ColecaoDaApi.Nome)]
public sealed class LimitacaoDeTaxaTests(ApiDeTestes api)
{
    private const string Leitura = "/api/v1/parlamentares?itens=1";

    private static async Task<HttpResponseMessage> BaterAsync(
        HttpClient cliente,
        int vezes,
        Action<HttpRequestMessage>? preparar = null)
    {
        HttpResponseMessage? ultima = null;

        for (var i = 0; i < vezes; i++)
        {
            ultima?.Dispose();

            using var pedido = new HttpRequestMessage(HttpMethod.Get, Leitura);
            preparar?.Invoke(pedido);

            ultima = await cliente.SendAsync(pedido);
        }

        return ultima!;
    }

    [Fact]
    public async Task A_rajada_acima_do_teto_recebe_429()
    {
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var dentro = await BaterAsync(cliente, ApiComTetoBaixo.Teto);
        dentro.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var alem = await BaterAsync(cliente, 1);
        alem.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_recusa_diz_quando_tentar_de_novo()
    {
        // Sem Retry-After, o cliente educado não tem como saber que basta esperar — e o
        // comportamento natural dele passa a ser tentar de novo na hora, em laço.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var recusa = await BaterAsync(cliente, ApiComTetoBaixo.Teto + 1);

        recusa.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        recusa.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_recusa_nunca_e_cacheada()
    {
        // A armadilha desta etapa. O limitador recusa ANTES de o endpoint rodar, então o
        // filtro que carimba Cache-Control nas leituras nunca chega a ver este 429. Sem
        // cabeçalho, a CDN pode guardá-lo — e um 429 guardado no edge tranca todo mundo que
        // sai por aquele nó, por minutos, por causa de um raspador só.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var recusa = await BaterAsync(cliente, ApiComTetoBaixo.Teto + 1);

        recusa.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task A_recusa_sai_no_mesmo_formato_de_erro_do_resto_da_API()
    {
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var recusa = await BaterAsync(cliente, ApiComTetoBaixo.Teto + 1);

        recusa.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var corpo = await recusa.Content.ReadAsStringAsync();
        corpo.ShouldContain("429");
    }

    [Fact]
    public async Task A_sonda_de_saude_nao_e_limitada()
    {
        // Limitar a sonda faz a plataforma matar o contêiner exatamente no momento de carga
        // em que ele precisa continuar de pé.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        for (var i = 0; i < ApiComTetoBaixo.Teto * 3; i++)
        {
            using var resposta = await cliente.GetAsync("/saude");
            resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task A_chave_interna_passa_pelo_teto_publico()
    {
        // Nosso próprio frontend é o cliente mais pesado que existe por IP: o build busca as
        // três páginas de ranking e os 513 perfis de um endereço só, e a revalidação do ISR
        // continua batendo dali. Sem escape, o primeiro deploy derruba o próprio limite.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var resposta = await BaterAsync(
            cliente,
            ApiComTetoBaixo.Teto * 3,
            pedido => pedido.Headers.Add("X-Explanando-Interno", ApiComTetoBaixo.ChaveInterna));

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Chave_errada_nao_passa_pelo_teto()
    {
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste);
        var cliente = host.CreateClient();

        using var resposta = await BaterAsync(
            cliente,
            ApiComTetoBaixo.Teto + 1,
            pedido => pedido.Headers.Add("X-Explanando-Interno", "chute"));

        resposta.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Atras_de_proxy_o_teto_e_por_leitor_e_nao_por_proxy()
    {
        // Em produção há Cloudflare e Cloud Run na frente: o IP da conexão é o do proxy, e
        // TODO MUNDO cairia na mesma partição. O teto viraria um teto global — a centésima
        // pessoa a abrir a home levaria 429 por causa das noventa e nove anteriores.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste, atrasDeProxy: true);
        var cliente = host.CreateClient();

        using var esgotou = await BaterAsync(
            cliente,
            ApiComTetoBaixo.Teto + 1,
            pedido => pedido.Headers.Add("X-Forwarded-For", "198.51.100.7"));

        esgotou.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using var outroLeitor = await BaterAsync(
            cliente,
            1,
            pedido => pedido.Headers.Add("X-Forwarded-For", "203.0.113.42"));

        outroLeitor.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sem_proxy_declarado_o_cabecalho_do_cliente_e_ignorado()
    {
        // O outro lado da mesma moeda: confiar num cabeçalho que o cliente escreve dá a
        // qualquer um uma partição nova por requisição, e o limitador vira decoração. Só
        // vale quando o desdobramento garante que o proxy o sobrescreve.
        using var host = new ApiComTetoBaixo(api.ConexaoDeTeste, atrasDeProxy: false);
        var cliente = host.CreateClient();

        using var recusa = await BaterAsync(
            cliente,
            ApiComTetoBaixo.Teto + 1,
            pedido => pedido.Headers.Add("X-Forwarded-For", $"198.51.100.{Random.Shared.Next(1, 250)}"));

        recusa.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
