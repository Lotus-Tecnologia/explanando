using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

public sealed class MotorDeRegrasTests
{
    [Fact]
    public void Motor_padrao_carrega_as_doze_regras()
    {
        var motor = MotorDeRegras.Padrao();

        motor.Regras.Count.ShouldBe(12);
        motor.Regras.Select(regra => regra.Codigo).ShouldBe(
            [
                "REG_KYB_001", "REG_KYB_002", "REG_KYB_003", "REG_KYB_004", "REG_KYB_005",
                "REG_TX_001", "REG_TX_002", "REG_TX_003", "REG_TX_004",
                "REG_GEO_002", "REG_GEO_003",
                "REG_NET_001",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Codigos_de_regra_sao_unicos()
    {
        var codigos = MotorDeRegras.Padrao().Regras.Select(regra => regra.Codigo).ToList();

        codigos.Distinct(StringComparer.Ordinal).Count().ShouldBe(codigos.Count);
    }

    [Fact]
    public void Toda_regra_tem_descricao_dentro_do_vocabulario_permitido()
    {
        // O ADR-0004 vale também para o texto que descreve a própria regra, não só para
        // o achado emitido.
        foreach (var regra in MotorDeRegras.Padrao().Regras)
        {
            VocabularioDeCompliance.ContemTermoProibido(regra.Descricao)
                .ShouldBeFalse($"{regra.Codigo}: '{regra.Descricao}'");
        }
    }

    // ------------------------------------------------------------------
    //  Passada completa
    // ------------------------------------------------------------------

    [Fact]
    public void Parlamentar_sem_nada_de_errado_nao_recebe_flag_nenhuma()
    {
        // O caso mais importante do motor sob a postura permissiva: o silêncio.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(uf: "AP", cnaePrincipal: "4731800")
            .ComLimiteDeCota("AP", 55_929.26m)
            .ComDespesa(300m, mes: 3)
            .ComDespesa(280m, mes: 4)
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        resultado.Flags.ShouldBeEmpty();
        resultado.Falhas.ShouldBeEmpty();
        resultado.RegrasSilenciosas.Count.ShouldBe(12);
        resultado.TaxaDeDespesasSinalizadas.ShouldBe(0m);
    }

    [Fact]
    public void Passada_completa_junta_achados_de_regras_diferentes()
    {
        // Fornecedor baixado, de ramo alheio, em outro estado — três regras distintas
        // sobre a mesma despesa.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(
                uf: "GO",
                cnaePrincipal: "4723700",
                situacao: SituacaoCadastral.Baixada,
                dataSituacao: new DateOnly(2026, 1, 15))
            .ComDespesa(9_000m, ano: 2026, mes: 7, tipo: CenarioDeAvaliacao.Consultoria)
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        resultado.Flags.Select(flag => flag.CodigoRegra).ShouldContain("REG_KYB_001");
        resultado.Flags.Select(flag => flag.CodigoRegra).ShouldContain("REG_KYB_002");
        resultado.Falhas.ShouldBeEmpty();
    }

    [Fact]
    public void Toda_flag_emitida_respeita_o_vocabulario_de_compliance()
    {
        // FlagAnomalia.Criar já recusa vocabulário acusatório, então uma regra que
        // escrevesse "fraude" na descrição explodiria aqui em vez de chegar à tela.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(
                uf: "GO",
                cnaePrincipal: "4723700",
                situacao: SituacaoCadastral.Inapta,
                dataSituacao: new DateOnly(2026, 1, 15),
                dataAbertura: new DateOnly(2026, 6, 1),
                optantePeloMei: true,
                opcaoPeloMei: new DateOnly(2020, 1, 1))
            .ComLimiteDeCota("AP", 55_929.26m)
            .ComDespesa(60_000m, ano: 2026, mes: 7, dia: 25, tipo: CenarioDeAvaliacao.Alimentacao)
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        resultado.Flags.ShouldNotBeEmpty("o cenario foi montado para disparar varias regras");

        foreach (var flag in resultado.Flags)
        {
            VocabularioDeCompliance.ContemTermoProibido(flag.Descricao)
                .ShouldBeFalse($"{flag.CodigoRegra}: '{flag.Descricao}'");
        }
    }

    [Fact]
    public void Toda_flag_e_deterministica_no_v1()
    {
        // Sem campanha no contexto, nenhuma regra probabilística dispara: REG_NET_001, a
        // única que é (CPF de seis dígitos), precisa das doações do TSE.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700")
            .ComDespesa(9_000m, tipo: CenarioDeAvaliacao.Consultoria)
            .Construir();

        MotorDeRegras.Padrao().Avaliar(contexto).Flags
            .ShouldAllBe(flag => flag.EhDeterministica);
    }

    // ------------------------------------------------------------------
    //  Isolamento de falhas
    // ------------------------------------------------------------------

    [Fact]
    public void Regra_que_quebra_nao_derruba_as_outras()
    {
        // Um erro em uma regra não pode apagar os achados das outras dez.
        var motor = new MotorDeRegras([new RegraQuebrada(), new RegraCnaeDivergente()]);

        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(cnaePrincipal: "4723700")
            .ComDespesa(9_000m, tipo: CenarioDeAvaliacao.Consultoria)
            .Construir();

        var resultado = motor.Avaliar(contexto);

        resultado.Flags.Count.ShouldBe(1, "a regra saudavel continuou rodando");
        resultado.Flags[0].CodigoRegra.ShouldBe("REG_KYB_001");
        resultado.Falhas.Count.ShouldBe(1);
        resultado.Falhas[0].CodigoRegra.ShouldBe("REG_XXX_999");
        resultado.Falhas[0].Motivo.ShouldContain("defeito");
    }

    [Fact]
    public void Taxa_de_sinalizacao_mede_despesas_e_nao_flags()
    {
        // Três flags sobre a mesma despesa não são "300% sinalizado".
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(
                uf: "GO",
                cnaePrincipal: "4723700",
                situacao: SituacaoCadastral.Baixada,
                dataSituacao: new DateOnly(2026, 1, 15))
            .ComDespesa(9_000m, mes: 7, tipo: CenarioDeAvaliacao.Consultoria)
            .ComDespesa(50m, mes: 8, tipo: CenarioDeAvaliacao.Telefonia, cnpj: "00000000000001")
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        resultado.TotalDeDespesasAvaliadas.ShouldBe(2);
        resultado.Flags.Count.ShouldBeGreaterThan(1);
        resultado.TaxaDeDespesasSinalizadas.ShouldBe(0.5m);
    }

    // ------------------------------------------------------------------
    //  Versao do motor: separar "mudou o parlamentar" de "mudou o software"
    // ------------------------------------------------------------------

    [Fact]
    public void Carimba_a_versao_do_motor_em_toda_flag_emitida()
    {
        // Sem este carimbo, a recalibragem da tabela CNAE (que levou REG_KYB_001 de 1.477
        // para 297 flags de uma vez) teria disparado alerta para todo mundo que favoritou
        // um parlamentar — sobre uma mudanca que nao teve nada a ver com ele.
        var contexto = new CenarioDeAvaliacao()
            .ParlamentarDe("AP")
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2020, 1, 1))
            .ComDespesa(1_000m)
            .Construir();

        var resultado = MotorDeRegras.Padrao().Avaliar(contexto);

        resultado.Flags.ShouldNotBeEmpty();
        resultado.Flags.ShouldAllBe(flag => flag.VersaoDoMotor == MotorDeRegras.VersaoAtual);
    }

    [Fact]
    public void Resultado_declara_a_versao_que_o_produziu()
    {
        var contexto = new CenarioDeAvaliacao().ComDespesa(100m).Construir();

        MotorDeRegras.Padrao().Avaliar(contexto).VersaoDoMotor.ShouldBe(MotorDeRegras.VersaoAtual);
    }

    [Fact]
    public void Versao_pode_ser_fixada_na_instancia()
    {
        // O reprocessamento historico precisa poder rodar com a versao de origem; sem isso
        // nao ha como recompor um grafico antigo.
        var contexto = new CenarioDeAvaliacao()
            .ComFornecedor(situacao: SituacaoCadastral.Baixada, dataSituacao: new DateOnly(2020, 1, 1))
            .ComDespesa(1_000m)
            .Construir();

        var motor = new MotorDeRegras([new RegraSituacaoCadastralIrregular()], versao: 7);

        var resultado = motor.Avaliar(contexto);

        resultado.VersaoDoMotor.ShouldBe(7);
        resultado.Flags.ShouldAllBe(flag => flag.VersaoDoMotor == 7);
    }

    [Fact]
    public void Versao_atual_e_maior_que_zero()
    {
        // FlagAnomalia recusa versao nao positiva; a constante e o contrato tem de casar.
        MotorDeRegras.VersaoAtual.ShouldBeGreaterThan(0);
    }

    /// <summary>Dublê que estoura, para provar o isolamento de falhas do motor.</summary>
    private sealed class RegraQuebrada : IRegraAnomalia
    {
        public string Codigo => "REG_XXX_999";

        public string Descricao => "Regra de teste que sempre estoura.";

        public DadoExigido Exige => DadoExigido.Nenhum;

        public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto) =>
            throw new InvalidOperationException("Regra com defeito proposital.");
    }
}
