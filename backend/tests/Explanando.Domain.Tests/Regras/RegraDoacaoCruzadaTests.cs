using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Eleicoes;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// REG_NET_001 — sócio do fornecedor compatível com doador da campanha.
///
/// <para>Os CPFs daqui são inventados e com dígito verificador qualquer: a regra nunca valida o
/// CPF, só compara os seis dígitos do meio, que é tudo o que a Receita publica do sócio.</para>
/// </summary>
public class RegraDoacaoCruzadaTests
{
    // Os seis do meio de "12345678901" são "456789".
    private const string CpfDoDoador = "12345678901";
    private const string MascaraDoSocio = "***456789**";

    private static CenarioDeAvaliacao Cenario() =>
        new CenarioDeAvaliacao()
            .ComFornecedor(razaoSocial: "AGENCIA DE COMUNICACAO LTDA", cnaePrincipal: "7319002")
            .ComSocio("JOSÉ DA SILVA", MascaraDoSocio)
            .ComDespesa(5_000m, mes: 3, dia: 10, tipo: CenarioDeAvaliacao.Consultoria)
            .ComDespesa(5_000m, mes: 4, dia: 10, tipo: CenarioDeAvaliacao.Consultoria)
            .ComDespesa(5_000m, mes: 5, dia: 10, tipo: CenarioDeAvaliacao.Consultoria);

    [Fact]
    public void Socio_compativel_com_doador_gera_uma_flag_so_por_fornecedor()
    {
        var flags = Cenario().ComDoacao("JOSE DA SILVA", CpfDoDoador, 20_000m).Rodar(new RegraDoacaoCruzada());

        // Três pagamentos à mesma empresa, uma flag: o índice mede o cruzamento, não a
        // frequência do pagamento.
        var flag = flags.ShouldHaveSingleItem();
        flag.CodigoRegra.ShouldBe("REG_NET_001");
        flag.Gravidade.ShouldBe(Gravidade.Media);
        flag.GrauDeConfianca.ShouldBe(CpfParcial.GrauDeConfiancaMaximo);
        flag.Descricao.ShouldContain("compatível");
        flag.Descricao.ShouldContain("R$ 20.000,00");
        flag.Descricao.ShouldContain("R$ 15.000,00");
        flag.Descricao.ShouldContain("'JOSÉ DA SILVA'");
    }

    [Fact]
    public void A_flag_vai_na_primeira_despesa_da_empresa()
    {
        var cenario = Cenario().ComDoacao("JOSE DA SILVA", CpfDoDoador, 1_000m);
        var contexto = cenario.Construir();

        var flag = new RegraDoacaoCruzada().Avaliar(contexto).ShouldHaveSingleItem();

        flag.DespesaId.ShouldBe(contexto.Despesas.MinBy(d => d.DataDocumento)!.Id);
    }

    [Fact]
    public void Mesmo_cpf_com_nome_diferente_nao_e_o_mesmo_socio()
    {
        // Seis dígitos batem em 10^5 CPFs diferentes. Sem o nome idêntico, não há ligação.
        Cenario().ComDoacao("JOAO DE SOUZA", CpfDoDoador, 20_000m)
            .Rodar(new RegraDoacaoCruzada())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Mesmo_nome_com_cpf_diferente_nao_e_o_mesmo_socio()
    {
        Cenario().ComDoacao("JOSE DA SILVA", "12399999901", 20_000m)
            .Rodar(new RegraDoacaoCruzada())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Sem_campanha_a_regra_se_cala()
    {
        Cenario().Rodar(new RegraDoacaoCruzada()).ShouldBeEmpty();
    }

    [Fact]
    public void Sem_campanha_a_cobertura_diz_que_faltou_dado_e_nao_que_esta_limpo()
    {
        var resultado = MotorDeRegras.Padrao().Avaliar(Cenario().Construir());

        var cobertura = resultado.Cobertura.PorRegra.Single(r => r.Codigo == "REG_NET_001");
        cobertura.SilencioEhConclusivo.ShouldBeFalse();
        cobertura.DadoAusente.HasFlag(DadoExigido.CampanhaDoParlamentar).ShouldBeTrue();
    }

    [Fact]
    public void Com_campanha_e_sem_casamento_o_silencio_e_conclusivo()
    {
        var resultado = MotorDeRegras.Padrao().Avaliar(
            Cenario().ComDoacao("OUTRA PESSOA", "98765432100", 500m).Construir());

        var cobertura = resultado.Cobertura.PorRegra.Single(r => r.Codigo == "REG_NET_001");
        cobertura.SilencioEhConclusivo.ShouldBeTrue();
        resultado.Flags.ShouldNotContain(f => f.CodigoRegra == "REG_NET_001");
    }

    [Fact]
    public void A_descricao_passa_pelo_vocabulario_do_adr_0004()
    {
        var flag = Cenario().ComDoacao("JOSE DA SILVA", CpfDoDoador, 20_000m)
            .Rodar(new RegraDoacaoCruzada())
            .Single();

        VocabularioDeCompliance.ContemTermoProibido(flag.Descricao).ShouldBeFalse();
    }
}

/// <summary>A máscara que a carga do TSE aplica ao CPF completo, e a chave de nome.</summary>
public class DoacaoEleitoralTests
{
    [Fact]
    public void O_cpf_completo_vira_a_mascara_da_receita_e_nao_e_guardado()
    {
        var cpf = CpfParcial.DeCpfCompleto("123.456.789-01");

        cpf.Mascarado.ShouldBe("***456789**");
        cpf.DigitosVisiveis.ShouldBe("456789");
        cpf.ShouldBe(CpfParcial.Criar("***456789**"));
    }

    [Fact]
    public void Documento_que_nao_e_cpf_nao_vira_mascara()
    {
        CpfParcial.DeCpfCompleto("12345678000190").TemDigitos.ShouldBeFalse();
        CpfParcial.DeCpfCompleto(null).TemDigitos.ShouldBeFalse();
    }

    [Fact]
    public void A_doacao_nunca_carrega_o_cpf_completo()
    {
        var doacao = DoacaoEleitoral.Criar(
            2022, 1, DestinoDaDoacao.Candidato, 10, null, "Maria Souza", "11122233344",
            "CANDIDATO", "XYZ", "SP", "Deputado Federal", new DateOnly(2022, 9, 1), 100m);

        doacao.CpfDoDoador.Mascarado.ShouldBe("***222333**");
        System.Text.Json.JsonSerializer.Serialize(doacao).ShouldNotContain("11122233344");
    }

    [Fact]
    public void Doacao_com_cnpj_e_recusada()
    {
        Should.Throw<ArgumentException>(() => DoacaoEleitoral.Criar(
            2022, 1, DestinoDaDoacao.Partido, null, null, "PARTIDO X", "12345678000190",
            "X", "X", "SP", null, new DateOnly(2022, 9, 1), 100m));
    }

    [Theory]
    [InlineData("José da Silva", "JOSE DA SILVA")]
    [InlineData("  JOSÉ   DA  SILVA ", "JOSE DA SILVA")]
    [InlineData("Ana-Maria Côrtes", "ANA MARIA CORTES")]
    public void Nome_normalizado_tira_acento_caixa_e_espaco(string entrada, string esperado)
    {
        NomeNormalizado.De(entrada).ShouldBe(esperado);
    }

    [Fact]
    public void Nome_normalizado_nao_aproxima()
    {
        NomeNormalizado.De("José Silva").ShouldNotBe(NomeNormalizado.De("José da Silva"));
    }
}
