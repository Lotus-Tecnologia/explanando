using Explanando.Domain.Comum;

namespace Explanando.Domain.Tests.Comum;

/// <summary>
/// A BrasilAPI entrega o CPF do socio no formato "***456789**": dos 11 digitos,
/// apenas 6 sao visiveis (as posicoes 4 a 9, ou indices 3 a 8). Descoberto na Etapa 01 -
/// docs/contratos/brasilapi-cnpj.json.
///
/// Este tipo existe para que essa limitacao seja explicita no dominio inteiro, em vez
/// de virar uma string que alguem trata como se fosse um CPF completo. A consequencia
/// direta e' REG_NET_001 (doacao cruzada): o cruzamento com a base do TSE nao pode ser
/// afirmativo, so' compativel. Ver ADR-0004.
/// </summary>
public sealed class CpfParcialTests
{
    // Socio ficticio do CNPJ 36826246000164 (FULANO DE TAL PRIMEIRO SOCIO).
    private const string MascaraDoSocio = "***456789**";

    // CPF completo cujos indices 3..8 sao exatamente "456789":
    //   1 2 3 | 4 5 6 7 8 9 | 0 9   ->  123.456.789-09
    private const string CpfQueCasa = "12345678909";

    [Fact]
    public void Extrai_os_seis_digitos_visiveis_da_mascara()
    {
        var parcial = CpfParcial.Criar(MascaraDoSocio);

        parcial.DigitosVisiveis.ShouldBe("456789");
        parcial.TemDigitos.ShouldBeTrue();
    }

    [Fact]
    public void Preserva_a_mascara_original_para_exibicao()
    {
        CpfParcial.Criar(MascaraDoSocio).Mascarado.ShouldBe(MascaraDoSocio);
    }

    [Theory]
    [InlineData("***000000**", "socio sem CPF informado")]
    [InlineData("***.456.789-**", "mascara com pontuacao")]
    public void Aceita_as_variacoes_que_a_fonte_produz(string entrada, string caso)
    {
        var parcial = CpfParcial.Criar(entrada);

        parcial.DigitosVisiveis.Length.ShouldBe(6, caso);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("***")]
    public void Aceita_ausencia_sem_explodir(string? vazio)
    {
        var parcial = CpfParcial.Criar(vazio);

        parcial.TemDigitos.ShouldBeFalse();
        parcial.DigitosVisiveis.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------------
    //  Compatibilidade com um CPF completo (a base de doadores do TSE).
    //  E' o cruzamento de REG_NET_001, e ele e' PROBABILISTICO por natureza.
    // ---------------------------------------------------------------------

    [Fact]
    public void Reconhece_compatibilidade_quando_os_seis_digitos_batem()
    {
        CpfParcial.Criar(MascaraDoSocio).EhCompativelCom(CpfQueCasa).ShouldBeTrue();
    }

    [Fact]
    public void Nao_reconhece_compatibilidade_quando_os_digitos_diferem()
    {
        // Indices 3..8 aqui sao "222333", nao "456789".
        CpfParcial.Criar(MascaraDoSocio).EhCompativelCom("11122233396").ShouldBeFalse();
    }

    [Fact]
    public void Aceita_cpf_completo_com_pontuacao()
    {
        CpfParcial.Criar(MascaraDoSocio).EhCompativelCom("123.456.789-09").ShouldBeTrue();
    }

    [Fact]
    public void Nao_e_compativel_com_nada_quando_nao_tem_digitos()
    {
        CpfParcial.Criar(null).EhCompativelCom(CpfQueCasa).ShouldBeFalse(
            "sem digitos nao ha o que comparar; retornar verdadeiro casaria com qualquer um");
    }

    [Theory]
    [InlineData("1234567890", "curto demais para ser CPF")]
    [InlineData("123456789090", "longo demais para ser CPF")]
    [InlineData("", "vazio")]
    [InlineData(null, "nulo")]
    public void Rejeita_candidato_que_nao_e_cpf(string? candidato, string motivo)
    {
        CpfParcial.Criar(MascaraDoSocio).EhCompativelCom(candidato).ShouldBeFalse(motivo);
    }

    [Fact]
    public void Compatibilidade_nunca_chega_a_certeza()
    {
        // Documentacao viva da regra: 6 digitos conhecidos deixam 10^5 combinacoes
        // possiveis para os 5 ocultos. "Compativel" nunca significa "e' a mesma pessoa",
        // e a UI tem de refletir isso (ADR-0004).
        CpfParcial.GrauDeConfiancaMaximo.ShouldBeLessThan(1.0m);
    }

    [Fact]
    public void Parciais_com_os_mesmos_digitos_sao_iguais()
    {
        CpfParcial.Criar("***.456.789-**").ShouldBe(CpfParcial.Criar(MascaraDoSocio));
    }
}
