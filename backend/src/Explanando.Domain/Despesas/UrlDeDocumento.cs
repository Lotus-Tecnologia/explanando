using System.Globalization;
using System.Text.RegularExpressions;

namespace Explanando.Domain.Despesas;

/// <summary>O que a URL de origem carrega, extraído antes de ela ser descartada.</summary>
public sealed record AnaliseDeUrl
{
    /// <summary>
    /// Se a Câmara publicou o arquivo. Falso em 52% das notas de tipo 0 — não é falha de
    /// coleta, é ausência na origem.
    /// </summary>
    public bool TemDocumento { get; init; }

    /// <summary>
    /// Número da carteira parlamentar, constante por deputado.
    ///
    /// <para><b>Só existe dentro desta URL.</b> Nenhum endpoint da API da Câmara o expõe —
    /// verificado. Por isso ele é extraído na ingestão e guardado no parlamentar: sem esse
    /// passo, descartar a coluna perderia o dado para sempre.</para>
    /// </summary>
    public int? NumeroCarteira { get; init; }

    /// <summary>
    /// Ano da pasta no caminho do arquivo. Não é o ano do documento: nas 20.595 URLs reais,
    /// toda nota datada de novembro ou dezembro é arquivada no ano seguinte, e há 5 casos
    /// que não seguem regra alguma.
    /// </summary>
    public int? AnoDoArquivo { get; init; }
}

/// <summary>
/// Analisa e reconstrói a URL do documento na Câmara.
///
/// <para>A URL deixou de ser persistida: eram 1.735 kB em 28.540 despesas, ~44 MB
/// projetados para 513 deputados, e ela é inteiramente derivável de três valores que já
/// existem. O que se guarda no lugar é o número da carteira (no parlamentar), um booleano
/// e — só quando o ano da pasta diverge do ano da nota — um <c>smallint</c>.</para>
///
/// <para><b>Nada aqui é heurística.</b> Havia a tentação de derivar o ano por regra
/// ("novembro e dezembro vão para o ano seguinte"), que acertaria 99,95% dos casos. Numa
/// página pública com nome e foto, 0,05% de links quebrados não vale os dois bytes: o
/// leitor clicaria e concluiria que o documento sumiu.</para>
/// </summary>
public static partial class UrlDeDocumento
{
    private const string BasePdf = "https://www.camara.leg.br/cota-parlamentar/documentos/publ";

    // http, nao https: e' o que a origem devolve, e trocar quebraria o link.
    private const string BaseNotaEletronica =
        "http://www.camara.leg.br/cota-parlamentar/nota-fiscal-eletronica?ideDocumentoFiscal=";

    /// <summary>Tipo 4 e' Nota Fiscal Eletronica, o unico com padrao proprio de URL.</summary>
    private const int TipoNotaFiscalEletronica = 4;

    public static AnaliseDeUrl Analisar(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new AnaliseDeUrl { TemDocumento = false };
        }

        var partes = CaminhoDeArquivo().Match(url);

        // Formato desconhecido continua sendo documento publicado. Se a Camara mudar o
        // padrao, o produto nao pode passar a dizer que o arquivo nao existe — ele
        // existe; o que se perdeu foi a capacidade de reconstruir o endereco.
        if (!partes.Success)
        {
            return new AnaliseDeUrl { TemDocumento = true };
        }

        return new AnaliseDeUrl
        {
            TemDocumento = true,
            NumeroCarteira = int.Parse(partes.Groups[1].Value, CultureInfo.InvariantCulture),
            AnoDoArquivo = int.Parse(partes.Groups[2].Value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Reconstrói o endereço, ou <c>null</c> quando falta peça para isso.
    ///
    /// Devolver uma URL incompleta seria pior que devolver nenhuma — um link quebrado
    /// afirma que o documento sumiu.
    /// </summary>
    public static string? Derivar(
        int codigoTipoDocumento,
        string? codigoDocumento,
        int? numeroCarteira,
        int ano)
    {
        if (string.IsNullOrWhiteSpace(codigoDocumento))
        {
            return null;
        }

        var codigo = codigoDocumento.Trim();

        if (codigoTipoDocumento == TipoNotaFiscalEletronica)
        {
            return BaseNotaEletronica + codigo;
        }

        if (numeroCarteira is not { } carteira)
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{BasePdf}/{carteira}/{ano}/{codigo}.pdf");
    }

    [GeneratedRegex(@"/publ/(\d+)/(\d{4})/", RegexOptions.CultureInvariant)]
    private static partial Regex CaminhoDeArquivo();
}

/// <summary>
/// Compara hashes de despesa por conteúdo.
///
/// Existe porque <c>byte[]</c> em C# compara por referência: um <c>HashSet&lt;byte[]&gt;</c>
/// sem comparador aceitaria a mesma nota duas vezes e a idempotência da ingestão — que é a
/// única defesa contra duplicar um mês inteiro ao reprocessá-lo — simplesmente não
/// existiria.
/// </summary>
public sealed class ComparadorDeHash : IEqualityComparer<byte[]>
{
    public static ComparadorDeHash Instancia { get; } = new();

    private ComparadorDeHash()
    {
    }

    public bool Equals(byte[]? x, byte[]? y) =>
        x is null || y is null ? ReferenceEquals(x, y) : x.AsSpan().SequenceEqual(y);

    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        // O valor ja e' um digest criptografico: os primeiros bytes servem de hash de
        // tabela sem nenhum tratamento adicional.
        var conteudo = new HashCode();
        conteudo.AddBytes(obj);
        return conteudo.ToHashCode();
    }
}
