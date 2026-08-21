using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// Serve as respostas reais capturadas em <c>docs/contratos/</c> no lugar da rede.
///
/// Testar contra a fixture, e nao contra a API ao vivo, mantem a suite deterministica e
/// executavel offline. E quando a fixture parar de refletir a realidade, o teste que bate
/// na API de verdade — separado e opcional — e' quem avisa.
/// </summary>
internal sealed class ManipuladorDeFixtures : HttpMessageHandler
{
    private const string PaginaVazia = """{"dados":[],"links":[]}""";

    private readonly List<(Func<Uri, bool> Casa, Func<Uri, RespostaFalsa> Responde)> _rotas = [];

    public List<Uri> UrlsChamadas { get; } = [];

    public ManipuladorDeFixtures Quando(Func<Uri, bool> casa, Func<Uri, RespostaFalsa> responde)
    {
        _rotas.Add((casa, responde));
        return this;
    }

    public ManipuladorDeFixtures QuandoContiver(string trecho, string arquivoDeFixture) =>
        Quando(
            uri => uri.PathAndQuery.Contains(trecho, StringComparison.OrdinalIgnoreCase),
            _ => RespostaFalsa.DeFixture(arquivoDeFixture));

    /// <summary>
    /// Serve a fixture na primeira chamada e uma pagina vazia depois.
    ///
    /// Necessario porque as fixtures reais trazem link `rel=next`: um fake que devolvesse
    /// sempre o mesmo corpo faria o cliente paginar para sempre. Foi exatamente assim que
    /// o processo de teste caiu antes desta correcao.
    /// </summary>
    public ManipuladorDeFixtures QuandoContiverUmaVez(string trecho, string arquivoDeFixture)
    {
        var servida = false;

        return Quando(
            uri => uri.PathAndQuery.Contains(trecho, StringComparison.OrdinalIgnoreCase),
            _ =>
            {
                if (servida)
                {
                    return new RespostaFalsa(HttpStatusCode.OK, PaginaVazia);
                }

                servida = true;
                return RespostaFalsa.DeFixture(arquivoDeFixture);
            });
    }

    /// <summary>
    /// Serve a fixture como <b>pagina unica</b>, trocando os links por so' <c>self</c>.
    ///
    /// <para>Necessario porque as fixtures reais foram capturadas com <c>itens=3</c> e
    /// trazem <c>last=397</c>. Servir uma pagina so' com esse link e' simular exatamente o
    /// truncamento que o cliente agora recusa — e o teste de mapeamento de campos nao fala
    /// de paginacao, fala de campos.</para>
    /// </summary>
    public ManipuladorDeFixtures QuandoContiverPaginaUnica(string trecho, string arquivoDeFixture) =>
        Quando(
            uri => uri.PathAndQuery.Contains(trecho, StringComparison.OrdinalIgnoreCase),
            _ =>
            {
                var corpo = Fixtures.Ler(arquivoDeFixture);
                var abre = corpo.IndexOf("\"links\"", StringComparison.Ordinal);

                var soDados = abre < 0
                    ? corpo
                    : corpo[..abre] + "\"links\":[]}";

                return new RespostaFalsa(HttpStatusCode.OK, soDados);
            });

    public ManipuladorDeFixtures QuandoContiver(string trecho, HttpStatusCode status, string corpo = "{}") =>
        Quando(
            uri => uri.PathAndQuery.Contains(trecho, StringComparison.OrdinalIgnoreCase),
            _ => new RespostaFalsa(status, corpo));

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        UrlsChamadas.Add(uri);

        foreach (var (casa, responde) in _rotas)
        {
            if (casa(uri))
            {
                var resposta = responde(uri);

                return Task.FromResult(new HttpResponseMessage(resposta.Status)
                {
                    Content = new StringContent(resposta.Corpo, Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                });
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"Nenhuma rota de teste casa com '{uri}'."),
            RequestMessage = request,
        });
    }

    internal sealed record RespostaFalsa(HttpStatusCode Status, string Corpo)
    {
        public static RespostaFalsa DeFixture(string arquivo) =>
            new(HttpStatusCode.OK, Fixtures.Ler(arquivo));
    }
}

/// <summary>Acesso aos contratos reais versionados em <c>docs/contratos/</c>.</summary>
internal static class Fixtures
{
    public static string Ler(string arquivo) =>
        File.ReadAllText(Path.Combine(Pasta(), arquivo));

    /// <summary>
    /// Ancorado no caminho do codigo-fonte: a saida de build vive fora do repositorio
    /// (ver Directory.Build.props), entao partir do diretorio de saida nao acharia nada.
    /// </summary>
    private static string Pasta([CallerFilePath] string arquivoDesteCodigo = "")
    {
        var diretorio = new DirectoryInfo(Path.GetDirectoryName(arquivoDesteCodigo)!);

        while (diretorio is not null && !Directory.Exists(Path.Combine(diretorio.FullName, "docs")))
        {
            diretorio = diretorio.Parent;
        }

        return Path.Combine(
            diretorio?.FullName ?? throw new InvalidOperationException("Raiz do repositorio nao encontrada."),
            "docs",
            "contratos");
    }
}
