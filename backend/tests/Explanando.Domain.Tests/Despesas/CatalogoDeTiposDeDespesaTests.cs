using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Tests.Despesas;

/// <summary>
/// O catálogo troca a frase repetida por um <c>smallint</c>. O que mais precisa de teste não
/// é o caminho feliz: é o que acontece com um tipo que a Câmara criar depois.
/// </summary>
public sealed class CatalogoDeTiposDeDespesaTests
{
    [Fact]
    public void Conhece_os_dezesseis_tipos_vistos_na_base_real()
    {
        CatalogoDeTiposDeDespesa.Quantidade.ShouldBe(16);
    }

    [Theory]
    [InlineData("COMBUSTÍVEIS E LUBRIFICANTES.")]
    [InlineData("PASSAGEM AÉREA - SIGEPA")]
    [InlineData("HOSPEDAGEM ,EXCETO DO PARLAMENTAR NO DISTRITO FEDERAL.")]
    [InlineData("SERVIÇOS POSTAIS")]
    public void Ida_e_volta_devolve_a_grafia_exata_da_origem(string nome)
    {
        // Inclusive o ponto final de uns e a vírgula deslocada de outro. A tela mostra o
        // que a Câmara escreveu, não uma versão arrumada por nós.
        var codigo = CatalogoDeTiposDeDespesa.CodigoDe(nome);

        codigo.ShouldNotBe(CatalogoDeTiposDeDespesa.NaoCatalogado);
        CatalogoDeTiposDeDespesa.NomeDe(codigo).ShouldBe(nome);
    }

    [Fact]
    public void Os_codigos_sao_distintos()
    {
        // Dois tipos com o mesmo código embaralhariam despesas já persistidas.
        CatalogoDeTiposDeDespesa.Nomes
            .Select(CatalogoDeTiposDeDespesa.CodigoDe)
            .Distinct()
            .Count()
            .ShouldBe(CatalogoDeTiposDeDespesa.Quantidade);
    }

    [Fact]
    public void O_primeiro_codigo_valido_e_um_e_o_zero_fica_reservado()
    {
        // Zero significa "fora do catálogo"; se um tipo real recebesse zero, ele passaria
        // a exigir a coluna de texto sem necessidade.
        CatalogoDeTiposDeDespesa.NomeDe(CatalogoDeTiposDeDespesa.NaoCatalogado).ShouldBeNull();
        CatalogoDeTiposDeDespesa.NomeDe(1).ShouldNotBeNull();
    }

    // ------------------------------------------------------------------
    //  O que acontece com o desconhecido
    // ------------------------------------------------------------------

    [Fact]
    public void Tipo_fora_do_catalogo_nao_lanca_excecao()
    {
        // A diferença entre isto e um enum. A lista da CEAP é definida por Ato da Mesa e
        // muda; recusar o desconhecido rejeitaria despesas no meio de uma coleta de doze
        // horas, e cada rejeição é gasto público que a página deixa de mostrar.
        CatalogoDeTiposDeDespesa
            .CodigoDe("CATEGORIA CRIADA POR ATO DA MESA EM 2027")
            .ShouldBe(CatalogoDeTiposDeDespesa.NaoCatalogado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("combustíveis e lubrificantes.")]
    [InlineData("COMBUSTÍVEIS E LUBRIFICANTES")]
    public void Nao_tenta_adivinhar_variacoes(string? nome)
    {
        // Casar por aproximação faria a despesa exibir uma grafia que a origem não usou.
        // Sem correspondência exata, o texto original é preservado inteiro.
        CatalogoDeTiposDeDespesa.CodigoDe(nome).ShouldBe(CatalogoDeTiposDeDespesa.NaoCatalogado);
    }

    [Fact]
    public void Codigo_fora_da_faixa_devolve_nulo_em_vez_de_estourar()
    {
        CatalogoDeTiposDeDespesa.NomeDe(9_999).ShouldBeNull();
        CatalogoDeTiposDeDespesa.NomeDe(-1).ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  Coerência com a tabela curada de CNAE
    // ------------------------------------------------------------------

    [Fact]
    public void Todo_tipo_catalogado_e_reconhecido_pela_tabela_de_cnae()
    {
        // Se um tipo entrasse no catálogo com grafia que a tabela curada não reconhece,
        // REG_KYB_001 se absteria em silêncio sobre todas as despesas dele.
        var desconhecidos = CatalogoDeTiposDeDespesa.Nomes
            .Where(nome => !CompatibilidadeCnae.ConheceOTipo(nome))
            .ToList();

        desconhecidos.ShouldBeEmpty();
    }
}

/// <summary>Como a despesa usa o catálogo, do lado de fora.</summary>
public sealed class DespesaComTipoCatalogadoTests
{
    private static Despesa Com(string tipo) => Despesa.Criar(new NovaDespesa
    {
        ParlamentarId = Guid.NewGuid(),
        DocumentoFornecedor = Domain.Comum.CpfCnpj.Criar("36826246000164"),
        NomeFornecedor = "FORNECEDOR",
        Competencia = Domain.Comum.Competencia.Criar(2026, 7),
        TipoDespesa = tipo,
        CodigoDocumento = "1",
        NumeroDocumento = "1",
        DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
        ValorDocumento = 100m,
        ValorLiquido = 100m,
    });

    [Fact]
    public void Tipo_conhecido_vira_codigo_e_nao_ocupa_a_coluna_de_texto()
    {
        var despesa = Com("TELEFONIA");

        despesa.CodigoTipoDespesa.ShouldNotBe(CatalogoDeTiposDeDespesa.NaoCatalogado);
        despesa.TipoDespesaNaoCatalogado.ShouldBeNull();
        despesa.TipoDespesa.ShouldBe("TELEFONIA");
    }

    [Fact]
    public void Tipo_desconhecido_e_preservado_por_extenso()
    {
        var despesa = Com("CATEGORIA NOVA QUE NINGUEM PREVIU");

        despesa.CodigoTipoDespesa.ShouldBe(CatalogoDeTiposDeDespesa.NaoCatalogado);
        despesa.TipoDespesaNaoCatalogado.ShouldBe("CATEGORIA NOVA QUE NINGUEM PREVIU");
        despesa.TipoDespesa.ShouldBe(
            "CATEGORIA NOVA QUE NINGUEM PREVIU",
            "as regras e a tela continuam lendo o nome, sem saber da diferença");
    }

    [Fact]
    public void Espaco_em_volta_e_removido_antes_de_procurar_no_catalogo()
    {
        Com("  TELEFONIA  ").TipoDespesaNaoCatalogado.ShouldBeNull();
    }
}
