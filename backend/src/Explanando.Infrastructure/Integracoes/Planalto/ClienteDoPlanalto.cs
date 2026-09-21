using System.Text.RegularExpressions;
using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Planalto;

/// <summary>
/// As cadeiras do Executivo federal, lidas do portal da Presidência.
///
/// <para><b>Não há API.</b> O SIORG, que é o cadastro oficial da estrutura, não publica
/// titular em nenhum endpoint. Quem publica quem ocupa cada cargo é o próprio Planalto, em
/// duas páginas HTML: a de ministros e a de "Quem é Quem", esta última obrigatória pela
/// Lei de Acesso à Informação. São páginas servidas pelo governo sobre o próprio governo —
/// a fonte mais primária que existe para este dado.</para>
///
/// <para><b>O detalhe que custou três tentativas:</b> o gov.br responde <c>404</c> a um
/// cliente HTTP comum e <c>200</c> ao mesmo pedido com o conjunto completo de cabeçalhos
/// de navegador. Não é o <c>User-Agent</c> sozinho — faltando <c>Sec-Fetch-Mode</c> ou
/// <c>Accept-Language</c> o 404 volta. Os cabeçalhos estão em
/// <see cref="ServicosDeIntegracao"/>, e se alguém "limpar" aquela lista por parecer
/// supérflua, esta coleta morre devolvendo <b>404, e não erro de rede</b> — que é o modo
/// de falhar mais difícil de diagnosticar.</para>
///
/// <para>Ler HTML é frágil por natureza e isso não se disfarça: o extrator devolve
/// <see cref="ColetaDeCadeiras.Falha"/> quando a página muda de forma, em vez de devolver
/// meia dúzia de linhas e deixar o mapa mostrar um governo pela metade.</para>
/// </summary>
public sealed partial class ClienteDoPlanalto(HttpClient http) : IFonteDeCadeiras
{
    /// <summary>A página de ministros. O caminho canônico redireciona de <c>/ministros</c>.</summary>
    private const string CaminhoDosMinistros =
        "https://www.gov.br/planalto/pt-br/conheca-a-presidencia/ministros-e-ministras";

    /// <summary>O "Quem é Quem" da LAI, onde está o Presidente da República.</summary>
    private const string CaminhoDoQuemEQuem =
        "https://www.gov.br/planalto/pt-br/acesso-a-informacao/institucional/"
        + "principais-cargos-e-respectivos-ocupantes-201cquem-e-quem201d";

    /// <summary>
    /// O piso de sanidade da página de ministros.
    ///
    /// <para>Hoje são 38. Se a extração devolver menos que isto, alguma coisa mudou na
    /// página e o certo é falhar: um mapa do Executivo com seis ministérios é mais
    /// enganoso que um mapa vazio, porque parece completo.</para>
    /// </summary>
    private const int MinimoDeMinistros = 20;

    public string Nome => "planalto";

    public async Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default)
    {
        var cadeiras = new List<NovaCadeira>();

        var quemEQuem = await BaixarAsync(CaminhoDoQuemEQuem, cancelamento).ConfigureAwait(false);

        if (quemEQuem is null)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha = $"O portal da Presidência não respondeu em {CaminhoDoQuemEQuem}.",
            };
        }

        if (LerPresidente(quemEQuem) is { } presidente)
        {
            cadeiras.Add(presidente);
        }

        var pagina = await BaixarAsync(CaminhoDosMinistros, cancelamento).ConfigureAwait(false);

        if (pagina is null)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha = $"O portal da Presidência não respondeu em {CaminhoDosMinistros}.",
            };
        }

        var ministros = LerMinistros(pagina).ToList();

        if (ministros.Count < MinimoDeMinistros)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha =
                    $"A página de ministros rendeu {ministros.Count} cargos, abaixo do piso "
                    + $"de {MinimoDeMinistros}. A estrutura da página provavelmente mudou; "
                    + "publicar isso mostraria um Executivo pela metade.",
            };
        }

        cadeiras.AddRange(ministros);

        return new ColetaDeCadeiras { Fonte = Nome, Cadeiras = cadeiras };
    }

    private async Task<string?> BaixarAsync(string url, CancellationToken cancelamento)
    {
        using var resposta = await http
            .GetAsync(new Uri(url), cancelamento)
            .ConfigureAwait(false);

        // 404 aqui costuma ser o filtro do gov.br recusando o cliente, e nao pagina
        // inexistente. Ver a nota da classe.
        return resposta.IsSuccessStatusCode
            ? await resposta.Content.ReadAsStringAsync(cancelamento).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// O Presidente da República, do "Quem é Quem".
    ///
    /// <para>A página traz o cargo em caixa alta e o nome na linha seguinte, junto com o
    /// começo da biografia. O nome é o trecho antes do primeiro parêntese — "Luiz Inácio
    /// Lula da Silva (nascido Luiz Inácio da Silva; Garanhuns/PE…".</para>
    /// </summary>
    private static NovaCadeira? LerPresidente(string html)
    {
        var linhas = EmTexto(html);

        for (var i = 0; i < linhas.Count - 1; i++)
        {
            if (!linhas[i].StartsWith("PRESIDENTE DA REP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nome = AteOParentese(linhas[i + 1]);

            if (nome.Length < 4)
            {
                return null;
            }

            return new NovaCadeira
            {
                Poder = Poder.Executivo,
                Camada = CamadaDeComando.Chefia,
                Cargo = "Presidente da República",
                Ocupante = nome,
                Orgao = "Presidência da República",
                Fonte = CaminhoDoQuemEQuem,
                Origem = OrigemDaCadeira.Automatica,
            };
        }

        return null;
    }

    /// <summary>
    /// Os ministros de Estado.
    ///
    /// <para>A página alterna cargo e nome em linhas consecutivas. O que identifica a
    /// linha de cargo é o próprio texto — "Ministro de Estado…", "Advocacia-Geral da
    /// União" —, e não a posição, porque a posição muda a cada troca de layout.</para>
    /// </summary>
    private static IEnumerable<NovaCadeira> LerMinistros(string html)
    {
        var linhas = EmTexto(html);
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < linhas.Count - 1; i++)
        {
            var cargo = linhas[i];

            if (!CargoDeMinistro().IsMatch(cargo) || cargo.Length > 160)
            {
                continue;
            }

            var nome = AteOParentese(linhas[i + 1]);

            // O nome de uma pessoa nao tem 120 caracteres nem comeca com minuscula: quando
            // a proxima linha nao parece nome, o par esta' quebrado e a linha e' descartada
            // em vez de virar um "ministro" com um paragrafo de biografia no lugar do nome.
            if (!PareceNome(nome) || !vistos.Add(cargo))
            {
                continue;
            }

            yield return new NovaCadeira
            {
                Poder = Poder.Executivo,
                Camada = CamadaDeComando.Cupula,
                Cargo = cargo,
                Ocupante = nome,
                Orgao = OrgaoDoCargo(cargo),
                Fonte = CaminhoDosMinistros,
                Origem = OrigemDaCadeira.Automatica,
            };
        }
    }

    /// <summary>
    /// O nome do órgão, deduzido do cargo.
    ///
    /// <para>"Ministro de Estado da Fazenda" vira "Ministério da Fazenda". É dedução, e
    /// por isso só acontece quando o cargo tem a forma exata — nos casos que fogem dela
    /// (Advocacia-Geral da União, Banco Central) o próprio cargo já é o nome do órgão.</para>
    /// </summary>
    private static string OrgaoDoCargo(string cargo)
    {
        var m = PrefixoDeMinistro().Match(cargo);

        return m.Success ? $"Ministério {m.Groups[1].Value}".Trim() : cargo;
    }

    private static string AteOParentese(string linha)
    {
        var corte = linha.IndexOf('(', StringComparison.Ordinal);
        var nome = corte > 0 ? linha[..corte] : linha;

        return nome.Trim().TrimEnd(',', ';', '.');
    }

    private static bool PareceNome(string valor) =>
        valor.Length is >= 4 and <= 120
        && char.IsUpper(valor[0])
        && !valor.Contains(". ", StringComparison.Ordinal);

    /// <summary>O texto visível da página, linha a linha, sem script nem estilo.</summary>
    private static List<string> EmTexto(string html)
    {
        var limpo = ScriptOuEstilo().Replace(html, " ");
        var semTag = Tag().Replace(limpo, "\n");
        var texto = System.Net.WebUtility.HtmlDecode(semTag);

        return
        [
            .. texto
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(linha => EspacoRepetido().Replace(linha, " ").Trim())
                .Where(linha => linha.Length > 0),
        ];
    }

    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOuEstilo();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[\s ]+")]
    private static partial Regex EspacoRepetido();

    [GeneratedRegex(
        @"^(Ministr[oa] de Estado\b|Advocacia-Geral da União$|Controladoria-Geral da União$|Banco Central do Brasil$)",
        RegexOptions.IgnoreCase)]
    private static partial Regex CargoDeMinistro();

    [GeneratedRegex(@"^Ministr[oa] de Estado (?:Chefe )?(?:d[aeo]s?\s+)?(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixoDeMinistro();

    /// <summary>Os cabeçalhos sem os quais o gov.br devolve 404. Ver a nota da classe.</summary>
    public static void ConfigurarCabecalhos(HttpClient cliente)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        var cabecalhos = cliente.DefaultRequestHeaders;

        cabecalhos.Add(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
            + "Chrome/131.0.0.0 Safari/537.36");
        cabecalhos.Add(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        cabecalhos.Add("Accept-Language", "pt-BR,pt;q=0.9");
        cabecalhos.Add("Upgrade-Insecure-Requests", "1");
        cabecalhos.Add("Sec-Fetch-Dest", "document");
        cabecalhos.Add("Sec-Fetch-Mode", "navigate");
        cabecalhos.Add("Sec-Fetch-Site", "none");
    }

    /// <summary>
    /// Quantos cargos de ministro a página rendeu.
    ///
    /// <para>Público porque o teste de contrato vive noutro assembly e precisa medir o
    /// extrator sobre a página real, sem passar por HTTP. É a única forma de o
    /// <c>git diff</c> da fixture apontar uma mudança de layout no dia em que ela
    /// acontecer.</para>
    /// </summary>
    public static int ContarMinistros(string html) =>
        LerMinistros(html).Count();

    /// <inheritdoc cref="ContarMinistros"/>
    public static string? NomeDoPresidente(string html) =>
        LerPresidente(html)?.Ocupante;

}
