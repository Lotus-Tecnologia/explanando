using Explanando.Domain.Varreduras;

namespace Explanando.Domain.Tests.Varreduras;

/// <summary>
/// O micro-financiamento. Dois pontos concentram o risco de dinheiro: a idempotencia do
/// webhook e a carencia que impede cobrar por dado que acabou de ser atualizado.
/// </summary>
public sealed class SolicitacaoVarreduraTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid UsuarioId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static SolicitacaoVarredura Solicitacao(decimal valor = 9.90m) =>
        SolicitacaoVarredura.Criar(ParlamentarId, valor, Agora);

    [Fact]
    public void Nasce_aguardando_pagamento()
    {
        var solicitacao = Solicitacao();

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.AguardandoPagamento);
        solicitacao.ParlamentarId.ShouldBe(ParlamentarId);
        solicitacao.ValorPago.ShouldBe(9.90m);
        solicitacao.DataSolicitacao.ShouldBe(Agora);
        solicitacao.PaymentId.ShouldBeNull();
        solicitacao.PagaEm.ShouldBeNull();
    }

    [Fact]
    public void Aceita_solicitacao_anonima()
    {
        // Pagar nao deve exigir cadastro: exigir conta antes do PIX corta a conversao
        // justamente no publico que chega por link compartilhado.
        Solicitacao().UsuarioId.ShouldBeNull();
    }

    [Fact]
    public void Guarda_o_usuario_quando_informado()
    {
        SolicitacaoVarredura
            .Criar(ParlamentarId, 9.90m, Agora, UsuarioId)
            .UsuarioId.ShouldBe(UsuarioId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recusa_valor_nao_positivo(decimal valor)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Solicitacao(valor));
    }

    // ------------------------------------------------------------------
    //  Idempotencia do webhook
    // ------------------------------------------------------------------

    [Fact]
    public void Confirmar_pagamento_marca_paga()
    {
        var solicitacao = Solicitacao();
        var quando = Agora.AddMinutes(3);

        solicitacao.ConfirmarPagamento("pay_123", quando);

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.Paga);
        solicitacao.PaymentId.ShouldBe("pay_123");
        solicitacao.PagaEm.ShouldBe(quando);
    }

    [Fact]
    public void Confirmar_pagamento_duas_vezes_nao_altera_nada()
    {
        // Gateways reenviam webhook. Sem esta guarda, o reenvio dispararia uma segunda
        // coleta cobrada uma unica vez.
        var solicitacao = Solicitacao();
        var primeira = Agora.AddMinutes(3);

        solicitacao.ConfirmarPagamento("pay_123", primeira);
        solicitacao.ConfirmarPagamento("pay_456", Agora.AddMinutes(9));

        solicitacao.PaymentId.ShouldBe("pay_123");
        solicitacao.PagaEm.ShouldBe(primeira);
    }

    [Fact]
    public void Webhook_atrasado_nao_reabre_solicitacao_ja_concluida()
    {
        var solicitacao = Solicitacao();
        solicitacao.ConfirmarPagamento("pay_123", Agora.AddMinutes(1));
        solicitacao.Concluir(Agora.AddMinutes(20));

        solicitacao.ConfirmarPagamento("pay_123", Agora.AddMinutes(30));

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.Concluida);
    }

    [Fact]
    public void Solicitacao_expirada_recusa_pagamento()
    {
        // Aqui a falha e' ruidosa de proposito: dinheiro entrou para uma cobranca que o
        // sistema ja deu por perdida, e isso precisa aparecer para decidir reembolso.
        var solicitacao = Solicitacao();
        solicitacao.Expirar();

        Should.Throw<InvalidOperationException>(
            () => solicitacao.ConfirmarPagamento("pay_123", Agora.AddHours(2)));
    }

    [Fact]
    public void Expirar_so_atinge_quem_ainda_aguardava_pagamento()
    {
        var solicitacao = Solicitacao();
        solicitacao.ConfirmarPagamento("pay_123", Agora.AddMinutes(1));

        solicitacao.Expirar();

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.Paga);
    }

    // ------------------------------------------------------------------
    //  Conclusao e falha
    // ------------------------------------------------------------------

    [Fact]
    public void So_solicitacao_paga_pode_concluir()
    {
        Should.Throw<InvalidOperationException>(() => Solicitacao().Concluir(Agora.AddHours(1)));
    }

    [Fact]
    public void Concluir_registra_o_momento()
    {
        var solicitacao = Solicitacao();
        solicitacao.ConfirmarPagamento("pay_123", Agora.AddMinutes(1));
        var fim = Agora.AddMinutes(25);

        solicitacao.Concluir(fim);

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.Concluida);
        solicitacao.ConcluidaEm.ShouldBe(fim);
    }

    [Fact]
    public void Falha_apos_pagamento_registra_o_motivo()
    {
        // O dinheiro ja entrou. O motivo e' o que permite escolher entre reprocessar e
        // devolver, e um estado "Falhou" sem texto nao permite nenhuma das duas.
        var solicitacao = Solicitacao();
        solicitacao.ConfirmarPagamento("pay_123", Agora.AddMinutes(1));

        solicitacao.RegistrarFalha("BrasilAPI indisponivel apos 5 tentativas.", Agora.AddMinutes(40));

        solicitacao.Situacao.ShouldBe(SituacaoDaSolicitacao.Falhou);
        solicitacao.MotivoDaFalha.ShouldNotBeNull().ShouldContain("BrasilAPI");
    }

    [Fact]
    public void Registrar_falha_exige_motivo()
    {
        Should.Throw<ArgumentException>(() => Solicitacao().RegistrarFalha("  ", Agora));
    }

    // ------------------------------------------------------------------
    //  Carencia: nao cobrar por dado recem-atualizado
    // ------------------------------------------------------------------

    [Fact]
    public void Carencia_e_de_vinte_e_quatro_horas()
    {
        SolicitacaoVarredura.CarenciaDeCobranca.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Varredura_de_uma_hora_atras_dispensa_nova_cobranca()
    {
        // Cobrar aqui seria vender nada — e isso e' relacao de consumo, nao so' produto.
        SolicitacaoVarredura
            .DentroDaCarencia(Agora.AddHours(-1), Agora)
            .ShouldBeTrue();
    }

    [Fact]
    public void Varredura_de_ontem_ja_permite_cobrar()
    {
        SolicitacaoVarredura
            .DentroDaCarencia(Agora.AddHours(-25), Agora)
            .ShouldBeFalse();
    }

    [Fact]
    public void Exatamente_no_limite_ja_permite_cobrar()
    {
        SolicitacaoVarredura
            .DentroDaCarencia(Agora - SolicitacaoVarredura.CarenciaDeCobranca, Agora)
            .ShouldBeFalse();
    }

    [Fact]
    public void Parlamentar_nunca_varrido_pode_ser_cobrado()
    {
        SolicitacaoVarredura.DentroDaCarencia(null, Agora).ShouldBeFalse();
    }
}
