namespace Explanando.Domain.Regras;

/// <summary>
/// Os limiares que decidem quando cada regra fala.
///
/// Ficam todos aqui, num lugar so', porque sao a parte mais arbitraria do motor: nenhum
/// deles vem de lei ou de dado publicado — sao escolhas de calibragem. Espalhados pelas
/// regras, ninguem conseguiria auditar o conjunto nem responder "por que 90 dias e nao 60?".
///
/// Todos foram escolhidos na direcao permissiva do
/// <c>ADR-0007</c>: preferem deixar passar a marcar indevidamente.
/// </summary>
public sealed record ParametrosDoMotor
{
    public static ParametrosDoMotor Padrao { get; } = new();

    // ---------------------------------------------------------------
    //  REG_KYB_003 — empresa recem-nascida
    // ---------------------------------------------------------------

    /// <summary>Idade abaixo da qual a empresa e' considerada recem-aberta. Vem do texto da regra.</summary>
    public int DiasParaEmpresaRecemNascida { get; init; } = 90;

    /// <summary>
    /// Valor a partir do qual faturar cedo chama atencao. Empresa nova prestando servico
    /// de R$ 200 e rotina; de R$ 5.000, menos.
    /// </summary>
    public decimal ValorMinimoParaEmpresaRecemNascida { get; init; } = 5_000m;

    // ---------------------------------------------------------------
    //  REG_KYB_004 — MEI acima do teto
    // ---------------------------------------------------------------

    /// <summary>
    /// Teto anual de faturamento do MEI. Este numero <b>vem de lei</b>, nao de calibragem,
    /// e por isso e' o unico do arquivo que nao pode ser afrouxado por decisao de produto.
    /// </summary>
    public decimal TetoAnualDoMei { get; init; } = 81_000m;

    // ---------------------------------------------------------------
    //  REG_KYB_005 — nota fiscal inicial
    // ---------------------------------------------------------------

    /// <summary>
    /// Numeracao abaixo da qual a nota e' considerada "inicial". Series fiscais comecam
    /// em 1, entao numero baixo sugere que o parlamentar esta entre os primeiros clientes.
    /// </summary>
    public int NumeroDeNotaConsideradoInicial { get; init; } = 50;

    /// <summary>Quantas notas iniciais do mesmo fornecedor sao precisas para falar.</summary>
    public int MinimoDeNotasIniciais { get; init; } = 3;

    // ---------------------------------------------------------------
    //  REG_TX_001 — fracionamento
    // ---------------------------------------------------------------

    public int MinimoDeNotasParaFracionamento { get; init; } = 4;

    /// <summary>Soma no mes, para o mesmo fornecedor, a partir da qual o fatiamento importa.</summary>
    public decimal SomaMinimaParaFracionamento { get; init; } = 5_000m;

    // ---------------------------------------------------------------
    //  REG_TX_002 — gasto cravado no teto da cota
    // ---------------------------------------------------------------

    /// <summary>Fracao do teto mensal a partir da qual o gasto e' considerado "cravado".</summary>
    public decimal FracaoDoTetoParaAlerta { get; init; } = 0.99m;

    // ---------------------------------------------------------------
    //  REG_TX_003 — padrao mensal identico
    // ---------------------------------------------------------------

    /// <summary>Meses consecutivos com valor identico ao centavo para o mesmo fornecedor.</summary>
    public int MesesConsecutivosIdenticos { get; init; } = 3;

    /// <summary>Abaixo disso, valor repetido e' assinatura ou mensalidade banal.</summary>
    public decimal ValorMinimoParaPadraoMensal { get; init; } = 1_000m;

    // ---------------------------------------------------------------
    //  REG_TX_004 — pico de consumo
    // ---------------------------------------------------------------

    /// <summary>Quantas vezes acima da media historica configura pico.</summary>
    public decimal FatorDePico { get; init; } = 3m;

    /// <summary>Meses de historico necessarios para que a media signifique alguma coisa.</summary>
    public int MinimoDeMesesDeHistorico { get; init; } = 3;

    /// <summary>Piso absoluto: triplicar R$ 50 nao e' pico, e' ruido.</summary>
    public decimal ValorMinimoParaPico { get; init; } = 2_000m;

    // ---------------------------------------------------------------
    //  REG_GEO_002 — gasto fora da base eleitoral
    // ---------------------------------------------------------------

    /// <summary>Valor a partir do qual gasto presencial fora da base merece nota.</summary>
    public decimal ValorMinimoParaGastoForaDaBase { get; init; } = 1_000m;

    // ---------------------------------------------------------------
    //  REG_GEO_003 — gasto em recesso
    // ---------------------------------------------------------------

    /// <summary>Valor a partir do qual alimentacao ou hospedagem em recesso merece nota.</summary>
    public decimal ValorMinimoParaGastoEmRecesso { get; init; } = 1_000m;
}
