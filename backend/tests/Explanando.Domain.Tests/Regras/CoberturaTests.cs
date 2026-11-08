using Explanando.Domain.Auditoria;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// A distinção que o motor não fazia: "examinei e não achei nada" contra "não tive como
/// examinar". Na calibragem real, duas regras ficaram mudas as 28.540 despesas inteiras —
/// e sem estes metadados o produto apresentaria o silêncio delas como aprovação.
/// </summary>
public sealed class CoberturaTests
{
    private static CoberturaDaRegra De(ResultadoDaAvaliacao resultado, string codigo) =>
        resultado.Cobertura.PorRegra.Single(regra => regra.Codigo == codigo);

    // ------------------------------------------------------------------
    //  Silêncio por falta de dado
    // ------------------------------------------------------------------

    [Fact]
    public void Regra_do_mei_fica_sem_dado_quando_a_opcao_e_desconhecida()
    {
        // 65% dos fornecedores da base real caem aqui: a BrasilAPI só informa quando sabe.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: null)
            .ComDespesa(1_000m)
            .Construir();

        var mei = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_KYB_004");

        mei.Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        mei.SilencioEhConclusivo.ShouldBeFalse("nao houve o que examinar");
        mei.DespesasComDado.ShouldBe(0);
        mei.DespesasSemDado.ShouldBe(1);
        mei.Cobertura.ShouldBe(0m);
        mei.DadoAusente.HasFlag(DadoExigido.OpcaoPeloMei).ShouldBeTrue();
    }

    [Fact]
    public void Regra_do_teto_fica_sem_dado_quando_nao_ha_limite_de_cota_configurado()
    {
        // Só 17% das despesas reais caem num período coberto pela tabela da CEAP.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(1_000m)
            .Construir();

        var teto = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_TX_002");

        teto.Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        teto.DadoAusente.HasFlag(DadoExigido.TetoDaCota).ShouldBeTrue();
    }

    [Fact]
    public void Regra_do_teto_passa_a_ter_dado_quando_o_limite_existe()
    {
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComLimiteDeCota("AP", 50_000m)
            .ComFornecedor()
            .ComDespesa(1_000m)
            .Construir();

        var teto = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_TX_002");

        teto.Situacao.ShouldBe(SituacaoDaRegra.SemAchados);
        teto.SilencioEhConclusivo.ShouldBeTrue("aqui o silêncio significa mesmo nada achado");
        teto.Cobertura.ShouldBe(1m);
    }

    [Fact]
    public void Teto_de_deputado_nao_vale_para_senador()
    {
        // CEAP e CEAPS sao cotas distintas com valores proprios para a mesma UF. Sem o
        // filtro por casa, o gasto de um senador seria comparado ao teto de um deputado —
        // e REG_TX_002 falaria sobre o numero errado, com nome e foto na tela.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDoSenado("AP")
            .ComLimiteDeCota("AP", 50_000m, casa: Domain.Parlamentares.TipoParlamentar.DeputadoFederal)
            .ComFornecedor()
            .ComDespesa(1_000m)
            .Construir();

        var teto = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_TX_002");

        teto.Situacao.ShouldBe(SituacaoDaRegra.SemDado, "o teto existente e' de outra casa");
    }

    [Fact]
    public void Teto_da_casa_certa_e_encontrado()
    {
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDoSenado("AP")
            .ComLimiteDeCota("AP", 50_000m, casa: Domain.Parlamentares.TipoParlamentar.Senador)
            .ComFornecedor()
            .ComDespesa(1_000m)
            .Construir();

        De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_TX_002")
            .Situacao.ShouldBe(SituacaoDaRegra.SemAchados);
    }

    [Fact]
    public void Despesa_com_cnpj_sentinela_deixa_as_regras_de_fornecedor_sem_dado()
    {
        // 2 em 100 despesas reais têm "00000000000001", que não resolve para fornecedor.
        var contexto = new CenarioDeAvaliacao()
            .ComDespesa(500m, cnpj: "00000000000001")
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        De(resultado, "REG_KYB_001").Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        De(resultado, "REG_KYB_002").Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        De(resultado, "REG_GEO_002").Situacao.ShouldBe(SituacaoDaRegra.SemDado);
    }

    [Fact]
    public void Despesa_sem_documento_do_fornecedor_deixa_as_regras_por_fornecedor_sem_dado()
    {
        // Auditoria de 2026-09-25: 184.280 passagens aéreas da Câmara (26% da base) vêm sem
        // documento do fornecedor. REG_TX_001 e REG_TX_003 agrupam por fornecedor e as
        // descartavam, mas declaravam Exige=Nenhum — e a página dizia "examinou tudo e não
        // achou nada" sobre despesas que a regra nunca olhou.
        var contexto = new CenarioDeAvaliacao()
            .ComDespesa(500m, cnpj: "")
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        foreach (var codigo in new[] { "REG_TX_001", "REG_TX_003" })
        {
            var regra = De(resultado, codigo);
            regra.Situacao.ShouldBe(SituacaoDaRegra.SemDado, codigo);
            regra.SilencioEhConclusivo.ShouldBeFalse(codigo);
            regra.DadoAusente.HasFlag(DadoExigido.DocumentoDoFornecedor).ShouldBeTrue(codigo);
        }
    }

    [Fact]
    public void Valor_da_camara_sem_centavo_deixa_o_padrao_mensal_sem_dado()
    {
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComSerieMensal(18_970.4m, primeiroMes: 3, quantidadeDeMeses: 3)
            .Construir();

        var regra = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_TX_003");

        regra.DespesasSemDado.ShouldBe(3);
        regra.DadoAusente.HasFlag(DadoExigido.CentavosDoValor).ShouldBeTrue();
    }

    [Fact]
    public void Rubrica_do_Senado_que_junta_estadia_e_combustivel_deixa_o_recesso_sem_dado()
    {
        // REG_GEO_003 se abstém da rubrica 17 do Senado (não dá para saber se foi hotel ou
        // gasolina) — e declarava Exige=Nenhum. Resultado medido: "sem achados" para os 118
        // senadores, sobre 34.073 despesas que a regra não pôde examinar.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDoSenado("AP")
            .ComDespesa(5_000m, mes: 1, dia: 10,
                tipo: "Locomoção, hospedagem, alimentação, combustíveis e lubrificantes")
            .Construir();

        var recesso = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_GEO_003");

        recesso.Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        recesso.SilencioEhConclusivo.ShouldBeFalse();
        recesso.DadoAusente.HasFlag(DadoExigido.NaturezaDaDespesa).ShouldBeTrue();
    }

    [Fact]
    public void Numeracao_inicial_sem_cadastro_do_fornecedor_fica_sem_dado()
    {
        // REG_KYB_005 só olha despesa cujo fornecedor está no cadastro, e declarava
        // Exige=Nenhum.
        var contexto = new CenarioDeAvaliacao()
            .ComDespesa(500m)
            .Construir();

        var inicial = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_KYB_005");

        inicial.Situacao.ShouldBe(SituacaoDaRegra.SemDado);
        inicial.DadoAusente.HasFlag(DadoExigido.CadastroDoFornecedor).ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    //  Silêncio conclusivo
    // ------------------------------------------------------------------

    [Fact]
    public void Regra_que_nao_depende_de_fonte_externa_tem_cobertura_total()
    {
        // REG_GEO_003 só precisa do calendário e da data da nota. Se ela fica muda, é
        // porque não havia gasto em recesso — e isso a página pode afirmar.
        var contexto = new CenarioDeAvaliacao()
            .ComDespesa(500m, mes: 3, dia: 10)
            .Construir();

        var recesso = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_GEO_003");

        recesso.Situacao.ShouldBe(SituacaoDaRegra.SemAchados);
        recesso.SilencioEhConclusivo.ShouldBeTrue();
        recesso.DespesasSemDado.ShouldBe(0);
        recesso.Cobertura.ShouldBe(1m);
    }

    [Fact]
    public void Cobertura_parcial_quando_parte_das_despesas_tem_o_dado()
    {
        // O caso comum na base real: alguns fornecedores resolvidos, outros não.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Ativa)
            .ComDespesa(500m)
            .ComDespesa(500m, cnpj: "00000000000001")
            .Construir();

        var situacao = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_KYB_002");

        situacao.Situacao.ShouldBe(SituacaoDaRegra.CoberturaParcial);
        situacao.SilencioEhConclusivo.ShouldBeFalse();
        situacao.DespesasComDado.ShouldBe(1);
        situacao.DespesasSemDado.ShouldBe(1);
        situacao.Cobertura.ShouldBe(0.5m);
    }

    [Fact]
    public void Regra_que_emitiu_registra_o_que_emitiu()
    {
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(
                situacao: SituacaoCadastral.Baixada,
                dataSituacao: new DateOnly(2020, 1, 1))
            .ComDespesa(1_000m)
            .Construir();

        var situacao = De(MotorDeRegras.Padrao().Avaliar(contexto), "REG_KYB_002");

        situacao.Situacao.ShouldBe(SituacaoDaRegra.Emitiu);
        situacao.FlagsEmitidas.ShouldBe(1);
        situacao.SilencioEhConclusivo.ShouldBeTrue();
    }

    [Fact]
    public void Regra_que_estourou_aparece_como_falha_e_nao_como_silencio()
    {
        // O pior desfecho possível seria uma regra quebrada entrar no relatório como
        // "nenhum indicador encontrado".
        var contexto = new CenarioDeAvaliacao().ComDespesa(500m).Construir();
        var motor = new MotorDeRegras([new RegraQueEstoura()]);

        var quebrada = De(motor.Avaliar(contexto), "REG_XXX_998");

        quebrada.Situacao.ShouldBe(SituacaoDaRegra.Falhou);
        quebrada.SilencioEhConclusivo.ShouldBeFalse();
        quebrada.MotivoDaFalha.ShouldNotBeNullOrWhiteSpace();
    }

    // ------------------------------------------------------------------
    //  Metadados da passada
    // ------------------------------------------------------------------

    [Fact]
    public void Declara_o_periodo_coberto()
    {
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor()
            .ComDespesa(100m, ano: 2026, mes: 2)
            .ComDespesa(100m, ano: 2026, mes: 9)
            .ComDespesa(100m, ano: 2026, mes: 5)
            .Construir();

        var cobertura = MotorDeRegras.Padrao().Avaliar(contexto).Cobertura;

        cobertura.PrimeiraCompetencia.ShouldNotBeNull().ToString().ShouldBe("2026-02");
        cobertura.UltimaCompetencia.ShouldNotBeNull().ToString().ShouldBe("2026-09");
        cobertura.DespesasAvaliadas.ShouldBe(3);
    }

    [Fact]
    public void Sem_despesa_o_periodo_e_nulo_em_vez_de_inventado()
    {
        var cobertura = MotorDeRegras.Padrao()
            .Avaliar(new CenarioDeAvaliacao().Construir())
            .Cobertura;

        cobertura.PrimeiraCompetencia.ShouldBeNull();
        cobertura.UltimaCompetencia.ShouldBeNull();
        cobertura.DespesasAvaliadas.ShouldBe(0);
    }

    [Fact]
    public void Declara_o_piso_amostral_e_se_a_amostra_o_alcanca()
    {
        // Coerente com o ADR-0009: abaixo do piso o índice de risco não é publicável, e
        // quem consome a API precisa saber disso sem ter de deduzir.
        var cenario = new CenarioDeAvaliacao().ComFornecedor();

        for (var i = 0; i < 3; i++)
        {
            cenario.ComDespesa(100m);
        }

        var cobertura = MotorDeRegras.Padrao().Avaliar(cenario.Construir()).Cobertura;

        cobertura.PisoAmostral.ShouldBe(100);
        cobertura.AmostraSuficiente.ShouldBeFalse();
    }

    [Fact]
    public void Carrega_a_versao_do_motor_junto()
    {
        var contexto = new CenarioDeAvaliacao().ComDespesa(100m).Construir();

        MotorDeRegras.Padrao().Avaliar(contexto).Cobertura
            .VersaoDoMotor.ShouldBe(MotorDeRegras.VersaoAtual);
    }

    [Fact]
    public void Toda_regra_do_motor_padrao_aparece_no_relatorio()
    {
        // Regra que rode sem entrar na cobertura seria uma afirmação silenciosa a mais.
        var contexto = new CenarioDeAvaliacao().ComFornecedor().ComDespesa(100m).Construir();
        var motor = MotorDeRegras.Padrao();

        var cobertura = motor.Avaliar(contexto).Cobertura;

        cobertura.PorRegra.Count.ShouldBe(motor.Regras.Count);
        cobertura.PorRegra.Select(regra => regra.Codigo)
            .ShouldBe(motor.Regras.Select(regra => regra.Codigo), ignoreOrder: true);
    }

    [Fact]
    public void Separa_as_regras_sem_dado_das_parciais_e_das_falhas()
    {
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: null)
            .ComDespesa(1_000m)
            .ComDespesa(1_000m, cnpj: "00000000000001")
            .Construir();

        var cobertura = MotorDeRegras.Padrao().Avaliar(contexto).Cobertura;

        cobertura.SemDado.ShouldNotBeEmpty();
        cobertura.SemDado.ShouldAllBe(regra => regra.Situacao == SituacaoDaRegra.SemDado);
        cobertura.Parciais.ShouldAllBe(regra => regra.Situacao == SituacaoDaRegra.CoberturaParcial);
        cobertura.Falhas.ShouldBeEmpty();
        cobertura.RegrasConclusivas.ShouldBeLessThan(cobertura.PorRegra.Count);
    }

    // ------------------------------------------------------------------
    //  A frase que vai para a página
    // ------------------------------------------------------------------

    [Fact]
    public void Resumo_avisa_quando_ha_regra_nao_avaliada_integralmente()
    {
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: null)
            .ComDespesa(1_000m)
            .Construir();

        var resumo = MotorDeRegras.Padrao().Avaliar(contexto).Cobertura.Resumo();

        resumo.ShouldContain("ausência de dado");
        resumo.ShouldContain("não significa ausência de achado");
    }

    [Fact]
    public void Resumo_passa_pelo_vocabulario_de_compliance()
    {
        // A frase vai para a tela do mesmo jeito que a descrição de uma flag vai. Se
        // alguém redigi-la com verbo de imputação, o ADR-0004 é violado pela borda.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(optantePeloMei: null)
            .ComDespesa(1_000m)
            .Construir();

        var resumo = MotorDeRegras.Padrao().Avaliar(contexto).Cobertura.Resumo();

        Should.NotThrow(() => Domain.Comum.VocabularioDeCompliance
            .GarantirVocabularioTecnico(resumo, nameof(resumo)));
    }

    [Fact]
    public void Resumo_sem_regra_alguma_nao_finge_cobertura()
    {
        new MetadadosDeCobertura().Resumo().ShouldContain("Nenhuma regra");
    }

    /// <summary>Dublê que estoura, para exercitar a situação de falha na cobertura.</summary>
    private sealed class RegraQueEstoura : IRegraAnomalia
    {
        public string Codigo => "REG_XXX_998";

        public string Descricao => "Regra de teste que sempre estoura.";

        public DadoExigido Exige => DadoExigido.Nenhum;

        public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto) =>
            throw new InvalidOperationException("Defeito proposital.");
    }
}
