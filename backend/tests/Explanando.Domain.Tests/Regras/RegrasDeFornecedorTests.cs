using Explanando.Domain.Auditoria;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// Cada regra tem dois tipos de teste: um que prova que ela <b>fala</b> no caso que existe
/// para pegar, e vários que provam que ela <b>se cala</b> onde não tem base — que é a parte
/// que importa sob a postura permissiva do ADR-0007.
/// </summary>
public sealed class RegrasDeFornecedorTests
{
    // =====================================================================
    //  REG_KYB_001 — CNAE divergente
    // =====================================================================

    [Fact]
    public void Cnae_divergente_sinaliza_ramo_claramente_alheio()
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700")  // comércio varejista de bebidas
            .ComDespesa(3_000m, tipo: CenarioDeAvaliacao.Consultoria)
            .Rodar(new RegraCnaeDivergente());

        flags.Count.ShouldBe(1);
        flags[0].CodigoRegra.ShouldBe("REG_KYB_001");
        flags[0].Gravidade.ShouldBe(Gravidade.Media);
    }

    [Theory]
    [InlineData("6462000")]
    [InlineData("6463800")]
    public void Aluguel_pago_a_holding_nao_e_ramo_divergente(string cnae)
    {
        // Decisão de 26/09/2026. Holding patrimonial dona do imóvel alugado é comum, e era
        // de onde vinham 89% do índice do primeiro do ranking do Senado.
        new CenarioDeAvaliacao()
            .ParlamentarDoSenado()
            .ComFornecedor(cnaePrincipal: cnae)
            .ComDespesa(8_000m,
                tipo: "Aluguel de imóveis para escritório político, compreendendo despesas concernentes a eles.")
            .Rodar(new RegraCnaeDivergente())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Nenhuma_descricao_abre_com_veredito()
    {
        // Decisão de 26/09/2026: "Alerta de compliance:" e "Fator de risco elevado:" abriam
        // 13 mil descrições, que vão inteiras para o RSS. Não imputam conduta, mas soam como
        // veredito antes do fato. A descrição abre com o fato.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700", dataAbertura: new DateOnly(2026, 6, 20), uf: "SP")
            .ComDespesa(3_000m, mes: 7, dia: 6, tipo: CenarioDeAvaliacao.Consultoria, numeroDocumento: "1")
            .ComDespesa(3_000m, mes: 7, dia: 7, tipo: CenarioDeAvaliacao.Consultoria, numeroDocumento: "2")
            .ComDespesa(3_000m, mes: 7, dia: 8, tipo: CenarioDeAvaliacao.Consultoria, numeroDocumento: "3")
            .ComDespesa(3_000m, mes: 1, dia: 10, tipo: "HOSPEDAGEM ,EXCETO DO PARLAMENTAR NO DISTRITO FEDERAL.")
            .Construir();

        var flags = MotorDeRegras.Padrao().Avaliar(contexto).Flags;

        flags.ShouldNotBeEmpty();
        foreach (var flag in flags)
        {
            flag.Descricao.ShouldNotStartWith("Alerta de compliance", Case.Insensitive);
            flag.Descricao.ShouldNotStartWith("Fator de risco", Case.Insensitive);
        }
    }

    [Fact]
    public void Cnae_divergente_emite_uma_flag_por_fornecedor_e_tipo()
    {
        // Levantamento de 08/11/2026: um par deputado–posto gerava 342 flags, e o indice
        // passava a medir quantas vezes se abasteceu. O fato e' do fornecedor: uma flag,
        // na despesa de maior valor, com quantidade e total na descricao.
        var cenario = new CenarioDeAvaliacao().ComFornecedor(cnaePrincipal: "4723700");
        for (var mes = 1; mes <= 10; mes++)
        {
            cenario.ComDespesa(100m * mes, mes: mes, tipo: CenarioDeAvaliacao.Combustivel);
        }

        cenario.ComDespesa(-300m, mes: 11, tipo: CenarioDeAvaliacao.Combustivel);
        var contexto = cenario.Construir();

        var flags = new RegraCnaeDivergente().Avaliar(contexto).ToList();

        flags.Count.ShouldBe(1);
        var maior = contexto.Despesas.MaxBy(d => d.ValorLiquido)!;
        flags[0].DespesaId.ShouldBe(maior.Id);
        flags[0].Descricao.ShouldContain("10 despesas");
        flags[0].Descricao.ShouldContain("R$ 5.500,00");
    }

    [Fact]
    public void Cnae_divergente_separa_tipos_diferentes_do_mesmo_fornecedor()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700")
            .ComDespesa(3_000m, mes: 3, tipo: CenarioDeAvaliacao.Combustivel)
            .ComDespesa(3_000m, mes: 4, tipo: CenarioDeAvaliacao.Consultoria)
            .Rodar(new RegraCnaeDivergente())
            .Count.ShouldBe(2);
    }

    [Fact]
    public void Cnae_divergente_nao_marca_estorno()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700")
            .ComDespesa(-3_000m, tipo: CenarioDeAvaliacao.Consultoria)
            .Rodar(new RegraCnaeDivergente())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Cnae_divergente_se_cala_quando_o_ramo_bate()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4731800")
            .ComDespesa(3_000m, tipo: CenarioDeAvaliacao.Combustivel)
            .Rodar(new RegraCnaeDivergente())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Cnae_divergente_se_cala_quando_um_secundario_bate()
    {
        // O posto real tem "bares" entre os secundários. Exigir que o principal batesse
        // marcaria empresas legítimas de atividade mista.
        new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "5611204", cnaesSecundarios: ["4731800"])
            .ComDespesa(3_000m, tipo: CenarioDeAvaliacao.Combustivel)
            .Rodar(new RegraCnaeDivergente())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Cnae_divergente_se_cala_quando_o_fornecedor_nao_foi_resolvido()
    {
        // Caso do CNPJ sentinela e da passagem aérea com documento vazio.
        new CenarioDeAvaliacao()
            .ComDespesa(3_000m, cnpj: "00000000000001", tipo: CenarioDeAvaliacao.Consultoria)
            .Rodar(new RegraCnaeDivergente())
            .ShouldBeEmpty();
    }

    // =====================================================================
    //  REG_KYB_002 — situação cadastral irregular
    // =====================================================================

    [Fact]
    public void Situacao_irregular_sinaliza_nota_posterior_a_baixa()
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2026, 1, 15))
            .ComDespesa(500m, ano: 2026, mes: 7, dia: 6)
            .Rodar(new RegraSituacaoCadastralIrregular());

        flags.Count.ShouldBe(1);
        flags[0].Gravidade.ShouldBe(Gravidade.Alta, "emitir nota apos a baixa e' sinal forte");
    }

    [Fact]
    public void Situacao_irregular_emite_uma_flag_por_fornecedor_sem_citar_a_casa()
    {
        var cenario = new CenarioDeAvaliacao()
            .ParlamentarDoSenado()
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2026, 1, 15));
        cenario.ComDespesa(500m, mes: 3).ComDespesa(800m, mes: 4).ComDespesa(200m, mes: 5);
        var contexto = cenario.Construir();

        var flags = new RegraSituacaoCadastralIrregular().Avaliar(contexto).ToList();

        flags.Count.ShouldBe(1);
        flags[0].DespesaId.ShouldBe(contexto.Despesas.MaxBy(d => d.ValorLiquido)!.Id);
        flags[0].Descricao.ShouldNotContain("Câmara");
        flags[0].Descricao.ShouldContain("3 notas");
        flags[0].Descricao.ShouldContain("R$ 1.500,00");
    }

    [Fact]
    public void Situacao_irregular_de_uma_nota_abre_pela_nota()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2026, 1, 15))
            .ComDespesa(500m, mes: 7, dia: 6)
            .Rodar(new RegraSituacaoCadastralIrregular())[0]
            .Descricao.ShouldStartWith("A nota de 06/07/2026");
    }

    [Fact]
    public void Situacao_irregular_nao_contamina_nota_anterior_a_baixa()
    {
        // Sinalizar aqui seria acusar retroativamente uma nota que era legítima.
        new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2026, 8, 1))
            .ComDespesa(500m, ano: 2026, mes: 7, dia: 6)
            .Rodar(new RegraSituacaoCadastralIrregular())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Situacao_irregular_se_cala_para_empresa_ativa()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Ativa)
            .ComDespesa(500m)
            .Rodar(new RegraSituacaoCadastralIrregular())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Situacao_irregular_se_cala_quando_a_situacao_e_desconhecida()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Desconhecida)
            .ComDespesa(500m)
            .Rodar(new RegraSituacaoCadastralIrregular())
            .ShouldBeEmpty();
    }

    // =====================================================================
    //  REG_KYB_003 — empresa recém-nascida
    // =====================================================================

    [Fact]
    public void Empresa_recem_nascida_sinaliza_faturamento_alto_logo_apos_abertura()
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor(dataAbertura: new DateOnly(2026, 6, 1))
            .ComDespesa(20_000m, ano: 2026, mes: 7, dia: 6)
            .Rodar(new RegraEmpresaRecemNascida());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("35 dias");
    }

    [Fact]
    public void Empresa_recem_nascida_se_cala_para_valor_pequeno()
    {
        // Empresa nova prestando serviço de R$ 200 é rotina.
        new CenarioDeAvaliacao()
            .ComFornecedor(dataAbertura: new DateOnly(2026, 6, 1))
            .ComDespesa(200m, ano: 2026, mes: 7, dia: 6)
            .Rodar(new RegraEmpresaRecemNascida())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Empresa_recem_nascida_se_cala_para_empresa_antiga()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(dataAbertura: new DateOnly(1991, 7, 23))
            .ComDespesa(20_000m)
            .Rodar(new RegraEmpresaRecemNascida())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Empresa_recem_nascida_se_cala_quando_a_abertura_e_desconhecida()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor(dataAbertura: null)
            .ComDespesa(20_000m)
            .Rodar(new RegraEmpresaRecemNascida())
            .ShouldBeEmpty("sem data de abertura a regra tem de se abster, nao chutar");
    }

    // =====================================================================
    //  REG_KYB_004 — MEI acima do teto
    // =====================================================================

    [Fact]
    public void Mei_acima_do_teto_sinaliza_quando_um_so_parlamentar_ja_estoura()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor(optantePeloMei: true, opcaoPeloMei: new DateOnly(2020, 1, 1));

        for (var mes = 1; mes <= 12; mes++)
        {
            cenario.ComDespesa(8_000m, ano: 2026, mes: mes);  // 96.000 no ano
        }

        var flags = cenario.Rodar(new RegraMeiAcimaDoTeto());

        flags.Count.ShouldBe(1);
        flags[0].Gravidade.ShouldBe(Gravidade.Alta);
        flags[0].Descricao.ShouldContain("MEI");
    }

    [Fact]
    public void Mei_acima_do_teto_se_cala_dentro_do_limite()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor(optantePeloMei: true, opcaoPeloMei: new DateOnly(2020, 1, 1));

        for (var mes = 1; mes <= 6; mes++)
        {
            cenario.ComDespesa(5_000m, ano: 2026, mes: mes);  // 30.000 no ano
        }

        cenario.Rodar(new RegraMeiAcimaDoTeto()).ShouldBeEmpty();
    }

    [Fact]
    public void Mei_acima_do_teto_nao_soma_anos_diferentes()
    {
        // O teto é anual. Somar a legislatura inteira acusaria qualquer MEI recorrente.
        var cenario = new CenarioDeAvaliacao().ComFornecedor(optantePeloMei: true, opcaoPeloMei: new DateOnly(2020, 1, 1));

        for (var mes = 1; mes <= 6; mes++)
        {
            cenario.ComDespesa(9_000m, ano: 2025, mes: mes);
            cenario.ComDespesa(9_000m, ano: 2026, mes: mes);
        }

        cenario.Rodar(new RegraMeiAcimaDoTeto())
            .ShouldBeEmpty("54.000 em cada ano, nenhum deles acima do teto");
    }

    [Fact]
    public void Mei_acima_do_teto_so_soma_o_que_foi_pago_enquanto_era_mei()
    {
        // Levantamento de 08/11/2026: a regra usava a opcao de HOJE para pagamentos de
        // 2023 a 2025. Na BrasilAPI ha fornecedor que optou pelo MEI em marco de 2026.
        var cenario = new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: true, opcaoPeloMei: new DateOnly(2026, 3, 30));

        for (var mes = 1; mes <= 12; mes++)
        {
            cenario.ComDespesa(8_000m, ano: 2025, mes: mes);  // 96.000, antes da opcao
        }

        cenario.Rodar(new RegraMeiAcimaDoTeto()).ShouldBeEmpty();
    }

    [Fact]
    public void Mei_acima_do_teto_nao_soma_depois_da_exclusao()
    {
        var cenario = new CenarioDeAvaliacao().ComFornecedor(
            optantePeloMei: false,
            opcaoPeloMei: new DateOnly(2020, 1, 1),
            exclusaoDoMei: new DateOnly(2024, 12, 31));

        for (var mes = 1; mes <= 12; mes++)
        {
            cenario.ComDespesa(8_000m, ano: 2024, mes: mes);
            cenario.ComDespesa(8_000m, ano: 2025, mes: mes);
        }

        var flags = cenario.Rodar(new RegraMeiAcimaDoTeto());

        flags.Count.ShouldBe(1, "2024 era MEI; 2025 ja nao");
        flags[0].Descricao.ShouldContain("2024");
    }

    [Fact]
    public void Mei_acima_do_teto_se_cala_sem_a_data_da_opcao()
    {
        // Sem saber desde quando, a regra nao tem como dizer que pagamento foi a um MEI.
        var cenario = new CenarioDeAvaliacao().ComFornecedor(optantePeloMei: true);

        for (var mes = 1; mes <= 12; mes++)
        {
            cenario.ComDespesa(10_000m, ano: 2026, mes: mes);
        }

        cenario.Rodar(new RegraMeiAcimaDoTeto()).ShouldBeEmpty();
    }

    [Fact]
    public void Mei_acima_do_teto_se_cala_quando_a_opcao_pelo_mei_e_desconhecida()
    {
        // No CNPJ real da amostra, `opcao_pelo_mei` veio nulo.
        var cenario = new CenarioDeAvaliacao().ComFornecedor(optantePeloMei: null);

        for (var mes = 1; mes <= 12; mes++)
        {
            cenario.ComDespesa(10_000m, ano: 2026, mes: mes);
        }

        cenario.Rodar(new RegraMeiAcimaDoTeto()).ShouldBeEmpty();
    }

    // =====================================================================
    //  REG_KYB_005 — nota fiscal inicial
    // =====================================================================

    [Fact]
    public void Nota_inicial_sinaliza_numeracao_baixa_e_repetida()
    {
        var flags = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m, mes: 3, numeroDocumento: "1")
            .ComDespesa(1_000m, mes: 4, numeroDocumento: "2")
            .ComDespesa(1_000m, mes: 5, numeroDocumento: "3")
            .Rodar(new RegraNotaFiscalInicial());

        flags.Count.ShouldBe(1);
        flags[0].Descricao.ShouldContain("1, 2, 3");
    }

    [Fact]
    public void Nota_inicial_se_cala_com_poucas_notas()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m, mes: 3, numeroDocumento: "1")
            .ComDespesa(1_000m, mes: 4, numeroDocumento: "2")
            .Rodar(new RegraNotaFiscalInicial())
            .ShouldBeEmpty("duas notas nao formam padrao");
    }

    [Fact]
    public void Nota_inicial_se_cala_com_numeracao_alta()
    {
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m, mes: 3, numeroDocumento: "8887")
            .ComDespesa(1_000m, mes: 4, numeroDocumento: "9134")
            .ComDespesa(1_000m, mes: 5, numeroDocumento: "9502")
            .Rodar(new RegraNotaFiscalInicial())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Nota_inicial_ignora_documento_que_nao_e_nota_fiscal()
    {
        // A descoberta da Etapa 03: bilhete aéreo tem numeração de 13 dígitos e recibo
        // não segue série sequencial do emitente. Incluí-los produziria ruído justamente
        // na regra mais delicada do conjunto.
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m, mes: 3, numeroDocumento: "1", codigoTipoDocumento: CenarioDeAvaliacao.BilheteAereo)
            .ComDespesa(1_000m, mes: 4, numeroDocumento: "2", codigoTipoDocumento: CenarioDeAvaliacao.BilheteAereo)
            .ComDespesa(1_000m, mes: 5, numeroDocumento: "3", codigoTipoDocumento: CenarioDeAvaliacao.BilheteAereo)
            .Rodar(new RegraNotaFiscalInicial())
            .ShouldBeEmpty();
    }

    [Fact]
    public void Nota_inicial_ignora_nota_sem_numero()
    {
        // O marcador textual da ingestão não pode ser lido como "número muito baixo".
        new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m, mes: 3, numeroDocumento: Application_NumeroAusente)
            .ComDespesa(1_000m, mes: 4, numeroDocumento: Application_NumeroAusente)
            .ComDespesa(1_000m, mes: 5, numeroDocumento: Application_NumeroAusente)
            .Rodar(new RegraNotaFiscalInicial())
            .ShouldBeEmpty();
    }

    /// <summary>Espelha o marcador que o tradutor da ingestão usa para nota sem número.</summary>
    private const string Application_NumeroAusente = "(sem numero)";
}
