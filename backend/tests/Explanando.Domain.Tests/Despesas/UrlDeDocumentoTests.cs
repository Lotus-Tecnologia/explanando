using Explanando.Domain.Despesas;

namespace Explanando.Domain.Tests.Despesas;

/// <summary>
/// A URL do documento deixou de ser guardada e passou a ser derivada. Estes testes existem
/// porque a decisão é <b>irreversível</b>: descartar a coluna sem extrair o número da
/// carteira perde o dado para sempre — nenhum endpoint da Câmara o expõe.
///
/// <para>Os dois padrões e as três datas de referência abaixo vieram de 20.595 URLs reais.</para>
/// </summary>
public sealed class UrlDeDocumentoTests
{
    private const string Pdf =
        "https://www.camara.leg.br/cota-parlamentar/documentos/publ/1472/2023/7502460.pdf";

    private const string NotaEletronica =
        "http://www.camara.leg.br/cota-parlamentar/nota-fiscal-eletronica?ideDocumentoFiscal=7494257";

    // ------------------------------------------------------------------
    //  Análise: o que se extrai antes de descartar
    // ------------------------------------------------------------------

    [Fact]
    public void Extrai_a_carteira_e_o_ano_da_url_de_pdf()
    {
        var analise = UrlDeDocumento.Analisar(Pdf);

        analise.TemDocumento.ShouldBeTrue();
        analise.NumeroCarteira.ShouldBe(1472);
        analise.AnoDoArquivo.ShouldBe(2023);
    }

    [Fact]
    public void Url_de_nota_eletronica_nao_tem_carteira_nem_ano()
    {
        // O padrão da NF-e não carrega nenhum dos dois — e não precisa: ela se reconstrói
        // só com o código do documento.
        var analise = UrlDeDocumento.Analisar(NotaEletronica);

        analise.TemDocumento.ShouldBeTrue();
        analise.NumeroCarteira.ShouldBeNull();
        analise.AnoDoArquivo.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ausencia_de_url_significa_documento_nao_publicado(string? url)
    {
        // 52% das notas de tipo 0 não têm documento nenhum na origem. Não é falha de
        // coleta: a Câmara simplesmente não publicou o arquivo.
        var analise = UrlDeDocumento.Analisar(url);

        analise.TemDocumento.ShouldBeFalse();
        analise.NumeroCarteira.ShouldBeNull();
        analise.AnoDoArquivo.ShouldBeNull();
    }

    [Fact]
    public void Url_de_formato_desconhecido_conta_como_documento_sem_partes_legiveis()
    {
        // Se a Câmara mudar o padrão, o produto não pode passar a dizer que o documento
        // não existe. Ele existe; o que se perdeu foi a capacidade de reconstruir o link.
        var analise = UrlDeDocumento.Analisar("https://www.camara.leg.br/algo/novo/aqui");

        analise.TemDocumento.ShouldBeTrue();
        analise.NumeroCarteira.ShouldBeNull();
        analise.AnoDoArquivo.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  Derivação: reconstruir o que foi descartado
    // ------------------------------------------------------------------

    [Fact]
    public void Reconstroi_a_url_de_pdf_exatamente()
    {
        UrlDeDocumento
            .Derivar(codigoTipoDocumento: 0, codigoDocumento: "7502460", numeroCarteira: 1472, ano: 2023)
            .ShouldBe(Pdf);
    }

    [Fact]
    public void Reconstroi_a_url_de_nota_eletronica_exatamente()
    {
        // Repare no http, não https: é o que a origem devolve, e mudar quebraria o link.
        UrlDeDocumento
            .Derivar(codigoTipoDocumento: 4, codigoDocumento: "7494257", numeroCarteira: null, ano: 2026)
            .ShouldBe(NotaEletronica);
    }

    [Fact]
    public void Nota_eletronica_dispensa_a_carteira()
    {
        UrlDeDocumento
            .Derivar(4, "7494257", numeroCarteira: 1472, ano: 2023)
            .ShouldBe(NotaEletronica);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Todos_os_tipos_de_pdf_usam_o_mesmo_padrao(int tipo)
    {
        UrlDeDocumento
            .Derivar(tipo, "7502460", 1472, 2023)
            .ShouldBe(Pdf);
    }

    [Fact]
    public void Sem_carteira_nao_ha_como_reconstruir_o_pdf()
    {
        // Devolver uma URL com a carteira em branco daria um link quebrado, que é pior
        // que nenhum link: o leitor clicaria e concluiria que o documento sumiu.
        UrlDeDocumento.Derivar(0, "7502460", numeroCarteira: null, ano: 2023).ShouldBeNull();
    }

    [Fact]
    public void Sem_codigo_de_documento_nao_ha_url()
    {
        UrlDeDocumento.Derivar(4, "  ", 1472, 2023).ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  Ida e volta
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(Pdf, 0, "7502460")]
    [InlineData(NotaEletronica, 4, "7494257")]
    public void Analisar_e_derivar_devolvem_a_url_original(
        string original,
        int tipo,
        string codigoDocumento)
    {
        var analise = UrlDeDocumento.Analisar(original);

        UrlDeDocumento
            .Derivar(tipo, codigoDocumento, analise.NumeroCarteira ?? 1472, analise.AnoDoArquivo ?? 2026)
            .ShouldBe(original);
    }
}
