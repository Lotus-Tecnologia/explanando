using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

public sealed class RegrasDeTransacaoEGeografiaTests
{
    // =====================================================================
    //  REG_TX_001 — fracionamento
    // =====================================================================

    [Fact]
    public void Fracionamento_sinaliza_muitas_notas_do_mesmo_fornecedor_no_mes()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();

        for (var i = 0; i < 5; i++)
        {
            cenario.ComDespesa(1_500m, mes: 7, dia: 5 + i);
        }

        var flags = cenario.Rodar(new RegraFracionamento());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("5 notas");
    }

    [Fact]
    public void Fracionamento_se_cala_com_poucas_notas()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();
        cenario.ComDespesa(3_000m, mes: 7, dia: 5).ComDespesa(3_000m, mes: 7, dia: 6);

        cenario.Rodar(new RegraFracionamento()).ShouldBeEmpty();
    }

    [Fact]
    public void Fracionamento_se_cala_quando_a_soma_e_baixa()
    {
        // Cinco abastecimentos de R$ 100 num mês é rotina, não fatiamento.
        var cenario = new CenarioDeAvaliacao().ComFornecedor();

        for (var i = 0; i < 5; i++)
        {
            cenario.ComDespesa(100m, mes: 7, dia: 5 + i);
        }

        cenario.Rodar(new RegraFracionamento()).ShouldBeEmpty();
    }

    [Fact]
    public void Fracionamento_nao_junta_meses_diferentes()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();

        for (var mes = 4; mes <= 8; mes++)
        {
            cenario.ComDespesa(1_500m, mes: mes);
        }

        cenario.Rodar(new RegraFracionamento())
            .ShouldBeEmpty("uma nota por mes durante cinco meses e' contrato, nao fatiamento");
    }

    // =====================================================================
    //  REG_TX_002 — gasto cravado no teto da cota
    // =====================================================================

    [Fact]
    public void Teto_da_cota_sinaliza_mes_que_encosta_no_limite()
    {
        var flags = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComLimiteDeCota("AP", 55_929.26m)
            .ComFornecedor()
            .ComDespesa(55_900m, ano: 2026, mes: 7)
            .Rodar(new RegraGastoNoTetoDaCota());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("AP");
    }

    [Fact]
    public void Teto_da_cota_se_cala_bem_abaixo_do_limite()
    {
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComLimiteDeCota("AP", 55_929.26m)
            .ComFornecedor()
            .ComDespesa(10_000m, ano: 2026, mes: 7)
            .Rodar(new RegraGastoNoTetoDaCota())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Teto_da_cota_se_abstem_quando_o_periodo_nao_tem_limite_configurado()
    {
        // Os valores de 2023 a 2025 ainda não foram levantados. Comparar uma despesa de
        // 2024 ao teto de 2026 seria inventar referência.
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComLimiteDeCota("AP", 55_929.26m, ano: 2026)
            .ComFornecedor()
            .ComDespesa(55_900m, ano: 2024, mes: 7)
            .Rodar(new RegraGastoNoTetoDaCota())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Teto_da_cota_usa_o_limite_da_uf_do_parlamentar()
    {
        // O teto de RR é bem maior que o de AP: usar a UF errada inverteria o resultado.
        new CenarioDeAvaliacao()
            .ParlamentarDe("RR")
            .ComLimiteDeCota("AP", 55_929.26m)
            .ComLimiteDeCota("RR", 58_474.70m)
            .ComFornecedor()
            .ComDespesa(56_000m, ano: 2026, mes: 7)
            .Rodar(new RegraGastoNoTetoDaCota())
            .ShouldBeEmpty("56 mil nao encosta no teto de RR, embora passasse o de AP");
    }

    // =====================================================================
    //  REG_TX_003 — padrão mensal idêntico
    // =====================================================================

    [Fact]
    public void Padrao_mensal_sinaliza_valor_identico_em_meses_seguidos()
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(4_500m, primeiroMes: 3, quantidadeDeMeses: 4)
            .Rodar(new RegraPadraoMensalIdentico());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("4 meses consecutivos");
    }

    [Fact]
    public void Padrao_mensal_se_cala_quando_os_meses_nao_sao_consecutivos()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(4_500m, mes: 3)
            .ComDespesa(4_500m, mes: 5)
            .ComDespesa(4_500m, mes: 9)
            .Rodar(new RegraPadraoMensalIdentico())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Padrao_mensal_se_cala_para_valores_baixos()
    {
        // Conta de telefone repetida é rotina, não sinal.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(89.90m, primeiroMes: 3, quantidadeDeMeses: 6, tipo: CenarioDeAvaliacao.Telefonia)
            .Rodar(new RegraPadraoMensalIdentico())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Padrao_mensal_se_cala_quando_o_valor_varia()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor();
        cenario.ComDespesa(4_500m, mes: 3).ComDespesa(4_501m, mes: 4).ComDespesa(4_499m, mes: 5);

        cenario.Rodar(new RegraPadraoMensalIdentico())
            .ShouldBeEmpty("variacao de centavos ja descaracteriza o repasse fixo");
    }

    // =====================================================================
    //  REG_TX_004 — pico de consumo
    // =====================================================================

    [Fact]
    public void Pico_sinaliza_salto_muito_acima_da_media()
    {
        // O exemplo do documento: média de R$ 2.000/mês que salta para R$ 12.000.
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(2_000m, primeiroMes: 1, quantidadeDeMeses: 5)
            .ComDespesa(12_000m, mes: 6)
            .Rodar(new RegraPicoDeConsumo());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("média", Case.Insensitive);
    }

    [Fact]
    public void Pico_se_cala_sem_historico_suficiente()
    {
        // Média de um mês só não é média.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(2_000m, mes: 5)
            .ComDespesa(12_000m, mes: 6)
            .Rodar(new RegraPicoDeConsumo())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Pico_se_cala_para_variacao_normal()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(2_000m, primeiroMes: 1, quantidadeDeMeses: 5)
            .ComDespesa(2_800m, mes: 6)
            .Rodar(new RegraPicoDeConsumo())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Pico_se_cala_quando_os_valores_sao_pequenos()
    {
        // Triplicar cinquenta reais não é pico, é ruído.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(50m, primeiroMes: 1, quantidadeDeMeses: 5)
            .ComDespesa(500m, mes: 6)
            .Rodar(new RegraPicoDeConsumo())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Pico_compara_dentro_do_mesmo_tipo_de_despesa()
    {
        // Gastar muito em divulgação não é pico de combustível.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(2_000m, primeiroMes: 1, quantidadeDeMeses: 5, tipo: CenarioDeAvaliacao.Combustivel)
            .ComDespesa(50_000m, mes: 6, tipo: "DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.")
            .Rodar(new RegraPicoDeConsumo())
            .ShouldBeEmpty("a serie de divulgacao nao tem historico proprio");
    }

    // =====================================================================
    //  REG_GEO_002 — gasto fora da base eleitoral
    // =====================================================================

    [Fact]
    public void Fora_da_base_sinaliza_o_caso_real_da_amostra()
    {
        // Deputado do AP abastecendo em Goiânia/GO — encontrado na amostra da Etapa 01.
        var flags = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(uf: "GO")
            .ComDespesa(2_500m, tipo: CenarioDeAvaliacao.Combustivel)
            .Rodar(new RegraGastoForaDaBaseEleitoral());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("GO");
        flags[0].Descricao.ShouldContain("AP");
    }

    [Fact]
    public void Fora_da_base_se_cala_na_propria_uf()
    {
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(uf: "AP")
            .ComDespesa(2_500m)
            .Rodar(new RegraGastoForaDaBaseEleitoral())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Fora_da_base_nunca_sinaliza_brasilia()
    {
        // O parlamentar trabalha em Brasília: gasto no DF é o esperado.
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(uf: "DF")
            .ComDespesa(2_500m)
            .Rodar(new RegraGastoForaDaBaseEleitoral())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Fora_da_base_se_cala_para_despesa_que_nao_exige_presenca()
    {
        // Contratar consultoria de empresa de outro estado é rotina; abastecer lá, não.
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(uf: "SP", cnaePrincipal: "6204000")
            .ComDespesa(2_500m, tipo: CenarioDeAvaliacao.Consultoria)
            .Rodar(new RegraGastoForaDaBaseEleitoral())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Fora_da_base_se_cala_quando_o_fornecedor_nao_foi_resolvido()
    {
        new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComDespesa(2_500m, cnpj: "00000000000001")
            .Rodar(new RegraGastoForaDaBaseEleitoral())
            .ShouldBeEmpty();
    }

    // =====================================================================
    //  REG_GEO_003 — gasto em recesso
    // =====================================================================

    [Theory]
    [InlineData(7, 25, "recesso de meio de ano")]
    [InlineData(12, 28, "recesso de fim de ano")]
    [InlineData(1, 15, "janeiro inteiro")]
    public void Recesso_sinaliza_alimentacao_cara_fora_da_sessao_legislativa(int mes, int dia, string caso)
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(3_000m, mes: mes, dia: dia, tipo: CenarioDeAvaliacao.Alimentacao)
            .Rodar(new RegraGastoEmRecesso());

        flags.Count.ShouldBe(1, caso);
        flags[0].Descricao.ShouldContain("art. 57");
    }

    [Theory]
    [InlineData(7, 10, "antes de 18 de julho")]
    [InlineData(3, 15, "meio da sessao legislativa")]
    [InlineData(12, 20, "antes de 23 de dezembro")]
    public void Recesso_se_cala_durante_a_sessao_legislativa(int mes, int dia, string caso)
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(3_000m, mes: mes, dia: dia, tipo: CenarioDeAvaliacao.Alimentacao)
            .Rodar(new RegraGastoEmRecesso())
            .ShouldBeEmpty(caso);
    }

    [Fact]
    public void Recesso_se_cala_para_despesa_que_nao_e_estadia()
    {
        // Conta de telefone não para no recesso.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(3_000m, mes: 7, dia: 25, tipo: CenarioDeAvaliacao.Telefonia)
            .Rodar(new RegraGastoEmRecesso())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Recesso_se_cala_para_valor_modesto()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(150m, mes: 7, dia: 25, tipo: CenarioDeAvaliacao.Alimentacao)
            .Rodar(new RegraGastoEmRecesso())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Recesso_se_cala_quando_a_rubrica_junta_estadia_com_combustivel()
    {
        // A rubrica da CEAPS que junta cinco coisas. Ela contém os três marcadores de
        // estadia E combustível — e responde por 43% das despesas do Senado.
        //
        // Sem a ressalva, um tanque de gasolina no recesso virava "alimentação ou hospedagem
        // expressiva". Medido contra a base real: 480 das 480 flags de REG_GEO_003 no Senado
        // saíam daqui, 22 vezes a taxa da Câmara.
        const string RubricaDoSenado =
            "Locomoção, hospedagem, alimentação, combustíveis e lubrificantes";

        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(3_000m, mes: 7, dia: 25, tipo: RubricaDoSenado)
            .Rodar(new RegraGastoEmRecesso())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Recesso_continua_falando_na_rubrica_agrupada_da_CAMARA()
    {
        // O limite da ressalva, e a diferença exata entre as duas casas: a rubrica agrupada
        // da Câmara junta locomoção, alimentação e hospedagem — mas NÃO combustível. Ela
        // segue conclusiva, e a correção não pode levá-la junto.
        const string RubricaDaCamara = "LOCOMOÇÃO, ALIMENTAÇÃO E HOSPEDAGEM";

        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(3_000m, mes: 7, dia: 25, tipo: RubricaDaCamara)
            .Rodar(new RegraGastoEmRecesso())
            .Count.ShouldBe(1);
    }
}
