using Explanando.Domain.Auditoria;

namespace Explanando.Domain.Tests.Auditoria;

public sealed class FlagAnomaliaTests
{
    private static readonly Guid DespesaId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static FlagAnomalia Flag(
        string descricao = "Fornecedor com CNAE incompatível com o tipo de despesa.",
        Gravidade gravidade = Gravidade.Alta,
        decimal confianca = 1.0m) =>
        FlagAnomalia.Criar(DespesaId, "REG_KYB_001", gravidade, descricao, confianca);

    [Fact]
    public void Guarda_o_codigo_da_regra_que_disparou()
    {
        // A UI sempre atribui o achado a uma regra nomeada — nunca a um juizo do sistema.
        Flag().CodigoRegra.ShouldBe("REG_KYB_001");
    }

    [Fact]
    public void Flag_de_gravidade_alta_e_a_vermelha()
    {
        Flag(gravidade: Gravidade.Alta).EhVermelha.ShouldBeTrue();
        Flag(gravidade: Gravidade.Media).EhVermelha.ShouldBeFalse();
        Flag(gravidade: Gravidade.Baixa).EhVermelha.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  ADR-0004 aplicado como invariante, nao como recomendacao
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("Indício de fraude no documento")]
    [InlineData("Padrão de lavagem de dinheiro")]
    [InlineData("Desvio de recursos identificado")]
    public void Recusa_descricao_com_vocabulario_acusatorio(string descricaoProibida)
    {
        var erro = Should.Throw<ArgumentException>(() => Flag(descricao: descricaoProibida));

        erro.Message.ShouldContain("ADR-0004");
    }

    [Fact]
    public void Aceita_descricao_com_o_vocabulario_tecnico()
    {
        var flag = Flag(descricao: "Atipicidade de volume financeiro frente à média histórica.");

        flag.Descricao.ShouldBe("Atipicidade de volume financeiro frente à média histórica.");
    }

    // ------------------------------------------------------------------
    //  Grau de confianca — exigido por REG_NET_001 (CPF parcial)
    // ------------------------------------------------------------------

    [Fact]
    public void Regra_deterministica_tem_confianca_total()
    {
        Flag(confianca: 1.0m).GrauDeConfianca.ShouldBe(1.0m);
        Flag(confianca: 1.0m).EhDeterministica.ShouldBeTrue();
    }

    [Fact]
    public void Regra_probabilistica_carrega_a_incerteza_junto()
    {
        // REG_NET_001 cruza 6 digitos de CPF: o achado e' indicio, nao prova.
        var provavel = Flag(confianca: 0.85m);

        provavel.EhDeterministica.ShouldBeFalse();
        provavel.GrauDeConfianca.ShouldBe(0.85m);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(0)]
    public void Rejeita_grau_de_confianca_fora_do_intervalo_valido(decimal invalido)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Flag(confianca: invalido));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Exige_descricao(string? vazia)
    {
        Should.Throw<ArgumentException>(() => Flag(descricao: vazia!));
    }

    [Fact]
    public void Exige_codigo_de_regra_no_formato_esperado()
    {
        Should.Throw<ArgumentException>(() =>
            FlagAnomalia.Criar(DespesaId, "kyb1", Gravidade.Alta, "Inconsistência.", 1.0m));
    }

    [Theory]
    [InlineData("REG_KYB_001")]
    [InlineData("REG_NET_004")]
    [InlineData("REG_TX_002")]
    [InlineData("REG_GEO_003")]
    public void Aceita_os_codigos_reais_do_motor(string codigo)
    {
        FlagAnomalia.Criar(DespesaId, codigo, Gravidade.Media, "Inconsistência.", 1.0m)
            .CodigoRegra.ShouldBe(codigo);
    }

    [Fact]
    public void Peso_no_score_cresce_com_a_gravidade_e_cai_com_a_incerteza()
    {
        var certaEGrave = Flag(gravidade: Gravidade.Alta, confianca: 1.0m);
        var incertaEGrave = Flag(gravidade: Gravidade.Alta, confianca: 0.5m);
        var certaELeve = Flag(gravidade: Gravidade.Baixa, confianca: 1.0m);

        certaEGrave.Peso.ShouldBeGreaterThan(incertaEGrave.Peso);
        certaEGrave.Peso.ShouldBeGreaterThan(certaELeve.Peso);
    }
}
