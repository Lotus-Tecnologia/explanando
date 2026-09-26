using Explanando.Domain.Comum;

namespace Explanando.Domain.Tests.Comum;

/// <summary>
/// O documento do fornecedor vem do campo `cnpjCpfFornecedor` da API da Camara.
/// A Etapa 01 mostrou que ele nao e' sempre um CNPJ: aparece CPF de pessoa fisica
/// e aparece o valor sentinela "00000000000001" (2 em 100 despesas na amostra).
///
/// Contratos reais em docs/contratos/camara-despesas-pagina-100.json.
/// </summary>
public sealed class CpfCnpjTests
{
    // CNPJ real, extraido de uma despesa de verdade (Auto Posto Bom Tempo Ltda).
    private const string CnpjReal = "36826246000164";

    [Fact]
    public void Reconhece_cnpj_valido_de_14_digitos()
    {
        var documento = CpfCnpj.Criar(CnpjReal);

        documento.EhValido.ShouldBeTrue();
        documento.Tipo.ShouldBe(TipoDocumento.Cnpj);
        documento.Numero.ShouldBe(CnpjReal);
    }

    [Fact]
    public void Reconhece_cpf_valido_de_11_digitos()
    {
        // CPF valido pelo algoritmo de digito verificador.
        var documento = CpfCnpj.Criar("52998224725");

        documento.EhValido.ShouldBeTrue();
        documento.Tipo.ShouldBe(TipoDocumento.Cpf);
    }

    [Theory]
    [InlineData("36.826.246/0001-64")]
    [InlineData("36826246/000164")]
    [InlineData("  36826246000164  ")]
    public void Normaliza_removendo_pontuacao_e_espacos(string comPontuacao)
    {
        var documento = CpfCnpj.Criar(comPontuacao);

        documento.Numero.ShouldBe(CnpjReal);
        documento.EhValido.ShouldBeTrue();
    }

    [Fact]
    public void Reconhece_o_valor_sentinela_da_camara()
    {
        var documento = CpfCnpj.Criar("00000000000001");

        documento.EhSentinela.ShouldBeTrue(
            "a Camara usa 00000000000001 quando nao ha fornecedor identificado");
        documento.EhValido.ShouldBeFalse();
        documento.Tipo.ShouldBe(TipoDocumento.Sentinela);
    }

    [Fact]
    public void Sentinela_nao_deve_ser_consultado_em_api_externa()
    {
        // A BrasilAPI responde 400 para ele (docs/contratos/brasilapi-cnpj-invalido.json).
        // Consultar seria queimar requisicao e poluir o log de erro.
        var documento = CpfCnpj.Criar("00000000000001");

        documento.PodeSerEnriquecido.ShouldBeFalse();
        CpfCnpj.Criar(CnpjReal).PodeSerEnriquecido.ShouldBeTrue();
    }

    [Theory]
    [InlineData("36826246000165", "digito verificador errado")]
    [InlineData("11111111111111", "todos os digitos iguais")]
    [InlineData("00000000000000", "tudo zero")]
    [InlineData("123", "curto demais")]
    [InlineData("368262460001640", "longo demais")]
    public void Rejeita_documento_invalido(string entrada, string motivo)
    {
        var documento = CpfCnpj.Criar(entrada);

        documento.EhValido.ShouldBeFalse(motivo);
        documento.PodeSerEnriquecido.ShouldBeFalse(motivo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Aceita_ausencia_sem_explodir(string? vazio)
    {
        // Despesa sem fornecedor ainda e' gasto publico e precisa ser persistida.
        // Lancar excecao aqui derrubaria a ingestao de um lote inteiro.
        var documento = CpfCnpj.Criar(vazio);

        documento.EhVazio.ShouldBeTrue();
        documento.EhValido.ShouldBeFalse();
        documento.Numero.ShouldBeEmpty();
    }

    [Fact]
    public void Documentos_com_mesmo_numero_sao_iguais()
    {
        // Igualdade por valor: o fornecedor e' deduplicado pelo documento durante a ingestao.
        CpfCnpj.Criar("36.826.246/0001-64").ShouldBe(CpfCnpj.Criar(CnpjReal));
        CpfCnpj.Criar(CnpjReal).GetHashCode().ShouldBe(CpfCnpj.Criar(CnpjReal).GetHashCode());
    }

    [Fact]
    public void Formata_cnpj_para_exibicao()
    {
        CpfCnpj.Criar(CnpjReal).Formatado.ShouldBe("36.826.246/0001-64");
    }

    [Fact]
    public void Formata_cpf_para_exibicao_sempre_mascarado()
    {
        // CPF de fornecedor pessoa fisica e' dado pessoal: nunca sai inteiro para a UI.
        CpfCnpj.Criar("52998224725").Formatado.ShouldBe("***.982.247-**");
    }

    // ------------------------------------------------------------------
    //  O CPF nunca é guardado inteiro (decisão de 26/09/2026)
    // ------------------------------------------------------------------

    [Fact]
    public void Cpf_e_validado_com_os_11_digitos_e_guardado_mascarado()
    {
        // A Câmara publica o CPF do fornecedor pessoa física inteiro — 325 deles, em 5.748
        // despesas, estavam gravados assim em 25/09. A validação precisa dos 11 dígitos; o
        // que fica no objeto (e no banco, e no backup) é a máscara que a Receita usa no QSA.
        var documento = CpfCnpj.Criar("52998224725");

        documento.Tipo.ShouldBe(TipoDocumento.Cpf);
        documento.Numero.ShouldBe("***982247**");
        documento.Numero.ShouldNotContain("52998224725");
    }

    [Fact]
    public void A_forma_mascarada_volta_do_banco_como_o_mesmo_cpf()
    {
        // O conversor do EF recria o documento a partir do que está gravado.
        var lido = CpfCnpj.Criar("***982247**");

        lido.Tipo.ShouldBe(TipoDocumento.Cpf);
        lido.ShouldBe(CpfCnpj.Criar("52998224725"));
        lido.Formatado.ShouldBe("***.982.247-**");
    }

    [Fact]
    public void Cpf_nao_vai_para_a_consulta_de_cnpj()
    {
        // A BrasilAPI só consulta CNPJ: o CPF voltava 400 e gastava uma requisição.
        CpfCnpj.Criar("52998224725").PodeSerEnriquecido.ShouldBeFalse();
        CpfCnpj.Criar("52998224725").EhValido.ShouldBeTrue();
    }

    [Fact]
    public void Onze_digitos_que_nao_sao_cpf_valido_continuam_invalidos()
    {
        CpfCnpj.Criar("12345678900").Tipo.ShouldBe(TipoDocumento.Invalido);
    }
}
