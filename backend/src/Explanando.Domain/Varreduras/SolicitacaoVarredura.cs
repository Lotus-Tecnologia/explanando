using Explanando.Domain.Comum;

namespace Explanando.Domain.Varreduras;

/// <summary>Estado de uma solicitação paga de varredura.</summary>
public enum SituacaoDaSolicitacao
{
    /// <summary>Criada, cobrança PIX emitida, pagamento ainda não confirmado.</summary>
    AguardandoPagamento = 1,

    /// <summary>Pagamento confirmado pelo gateway; a coleta pode rodar.</summary>
    Paga = 2,

    /// <summary>Coleta e dossiê concluídos.</summary>
    Concluida = 3,

    /// <summary>Pagamento expirou sem confirmação.</summary>
    Expirada = 4,

    /// <summary>Pagamento confirmado mas o processamento falhou. Exige reembolso ou reprocesso.</summary>
    Falhou = 5,
}

/// <summary>
/// Pedido de varredura sob demanda — o micro-financiamento do
/// <see href="../../../docs/especificacao/Explanando_Visao_Escopo_V1.md">documento de Visão</see>.
///
/// <para><b>O que se vende é a Camada 2, não o dado.</b> As flags determinísticas são
/// públicas e gratuitas; o pagamento aciona a coleta atualizada e o dossiê narrativo. O dado
/// resultante fica público para todos — quem paga patrocina a atualização.</para>
/// </summary>
public sealed class SolicitacaoVarredura : Entidade
{
    /// <summary>
    /// Janela em que uma varredura recém-concluída dispensa nova cobrança.
    ///
    /// Cobrar por dado que acabou de ser atualizado seria vender nada — e isso é problema de
    /// relação de consumo, não só de produto.
    /// </summary>
    public static TimeSpan CarenciaDeCobranca => TimeSpan.FromHours(24);

    private SolicitacaoVarredura()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>Nulo para solicitação anônima — pagar não deve exigir cadastro.</summary>
    public Guid? UsuarioId { get; private set; }

    public DateTimeOffset DataSolicitacao { get; private set; }

    public SituacaoDaSolicitacao Situacao { get; private set; }

    /// <summary>Identificador no gateway. Unicidade garantida por índice no banco.</summary>
    public string? PaymentId { get; private set; }

    public decimal ValorPago { get; private set; }

    public DateTimeOffset? PagaEm { get; private set; }

    public DateTimeOffset? ConcluidaEm { get; private set; }

    /// <summary>Motivo da falha, quando houver. Necessário para decidir reembolso.</summary>
    public string? MotivoDaFalha { get; private set; }

    public static SolicitacaoVarredura Criar(
        Guid parlamentarId,
        decimal valor,
        DateTimeOffset momento,
        Guid? usuarioId = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);

        return new SolicitacaoVarredura
        {
            ParlamentarId = parlamentarId,
            UsuarioId = usuarioId,
            ValorPago = valor,
            DataSolicitacao = momento,
            Situacao = SituacaoDaSolicitacao.AguardandoPagamento,
        };
    }

    /// <summary>
    /// Confirma o pagamento. Idempotente de propósito: gateways reenviam webhook, e
    /// processar duas vezes dispararia duas coletas cobradas uma vez.
    /// </summary>
    public void ConfirmarPagamento(string paymentId, DateTimeOffset momento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentId);

        if (Situacao is SituacaoDaSolicitacao.Paga or SituacaoDaSolicitacao.Concluida)
        {
            return;
        }

        if (Situacao == SituacaoDaSolicitacao.Expirada)
        {
            throw new InvalidOperationException(
                $"Solicitacao {Id} expirou e nao pode receber pagamento.");
        }

        PaymentId = paymentId;
        PagaEm = momento;
        Situacao = SituacaoDaSolicitacao.Paga;
    }

    public void Concluir(DateTimeOffset momento)
    {
        if (Situacao != SituacaoDaSolicitacao.Paga)
        {
            throw new InvalidOperationException(
                $"Solicitacao {Id} esta em '{Situacao}'; so' uma solicitacao paga pode concluir.");
        }

        Situacao = SituacaoDaSolicitacao.Concluida;
        ConcluidaEm = momento;
    }

    /// <summary>
    /// Marca falha depois do pagamento. O dinheiro já entrou, então o registro do motivo é
    /// o que permite decidir entre reprocessar e reembolsar.
    /// </summary>
    public void RegistrarFalha(string motivo, DateTimeOffset momento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        Situacao = SituacaoDaSolicitacao.Falhou;
        MotivoDaFalha = motivo;
        ConcluidaEm = momento;
    }

    public void Expirar()
    {
        if (Situacao == SituacaoDaSolicitacao.AguardandoPagamento)
        {
            Situacao = SituacaoDaSolicitacao.Expirada;
        }
    }

    /// <summary>
    /// Se uma varredura concluída em <paramref name="ultimaVarredura"/> ainda cobre o pedido,
    /// dispensando nova cobrança.
    /// </summary>
    public static bool DentroDaCarencia(DateTimeOffset? ultimaVarredura, DateTimeOffset agora) =>
        ultimaVarredura is { } quando && agora - quando < CarenciaDeCobranca;
}
