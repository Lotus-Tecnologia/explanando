using Explanando.Domain.Auditoria;

namespace Explanando.Domain.Regras;

/// <summary>Uma regra que lancou excecao durante a passada.</summary>
public sealed record FalhaDeRegra(string CodigoRegra, string Motivo);

/// <summary>O que uma passada do motor produziu, com o que e' preciso para audita-la.</summary>
public sealed record ResultadoDaAvaliacao
{
    public IReadOnlyList<FlagAnomalia> Flags { get; init; } = [];

    /// <summary>Quantas flags cada regra emitiu, por codigo.</summary>
    public IReadOnlyDictionary<string, int> FlagsPorRegra { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Regras que rodaram e nao emitiram nada.</summary>
    public IReadOnlyList<string> RegrasSilenciosas { get; init; } = [];

    /// <summary>
    /// Regras que quebraram. Lista vazia e' o esperado; qualquer item aqui e' defeito a
    /// investigar, e o resultado da passada esta incompleto naquele codigo.
    /// </summary>
    public IReadOnlyList<FalhaDeRegra> Falhas { get; init; } = [];

    /// <summary>Versao do motor que produziu este resultado. Ver MotorDeRegras.VersaoAtual.</summary>
    public int VersaoDoMotor { get; init; }

    public int TotalDeDespesasAvaliadas { get; init; }

    /// <summary>
    /// Fracao das despesas que recebeu ao menos uma flag.
    ///
    /// Metrica de calibragem, nao de resultado: com a postura permissiva do ADR-0007,
    /// espera-se um numero baixo. Se subir muito, alguma regra afrouxou; se for zero em
    /// toda a base, as tabelas curadas estao pobres demais e o motor esta mudo sem avisar.
    /// </summary>
    public decimal TaxaDeDespesasSinalizadas => TotalDeDespesasAvaliadas == 0
        ? 0m
        : (decimal)Flags.Select(flag => flag.DespesaId).Distinct().Count() / TotalDeDespesasAvaliadas;
}

/// <summary>
/// Executa as regras determinísticas sobre o contexto de um parlamentar.
///
/// O motor nao decide nada por si: ele encadeia regras independentes e junta o que cada uma
/// disse. Regra nova e' uma classe nova na lista — nao ha condicional a alterar aqui.
///
/// <para><b>Uma regra com defeito nao derruba as outras.</b> Excecao em uma regra e'
/// capturada e registrada, e as demais continuam. O oposto significaria que um erro em
/// REG_GEO_003 apagaria os achados de todas as outras dez.</para>
/// </summary>
public sealed class MotorDeRegras(
    IReadOnlyList<IRegraAnomalia> regras,
    int versao = MotorDeRegras.VersaoAtual)
{
    /// <summary>
    /// Versao do conjunto de regras e da calibragem.
    ///
    /// <para><b>Precisa ser incrementada a cada mudanca que altere o resultado</b> — regra
    /// nova, limiar ajustado, tabela CNAE editada. Sem isso, um degrau no historico fica
    /// indistinguivel entre "o parlamentar mudou" e "o software mudou", e a notificacao ao
    /// usuario que favoritou dispara por motivo errado.</para>
    ///
    /// <para>Nao e' hipotese: a recalibragem de 2026-08-20 levou REG_KYB_001 de 1.477 para
    /// 297 flags de uma vez. Em producao, todo favoritante teria recebido alerta sobre uma
    /// mudanca que nao teve nada a ver com o politico.</para>
    ///
    /// <list type="bullet">
    /// <item><b>1</b> — 11 regras, calibragem inicial (Etapa 04).</item>
    /// <item><b>2</b> — tabela CNAE calibrada contra 28.540 despesas reais (Etapa 04b).</item>
    /// </list>
    /// </summary>
    public const int VersaoAtual = 2;

    /// <summary>Versao efetiva desta instancia. Testes podem fixar outra.</summary>
    public int Versao { get; } = versao;

    /// <summary>As 11 regras que o dado disponivel sustenta no V1.</summary>
    public static MotorDeRegras Padrao() => new(
    [
        new RegraCnaeDivergente(),
        new RegraSituacaoCadastralIrregular(),
        new RegraEmpresaRecemNascida(),
        new RegraMeiAcimaDoTeto(),
        new RegraNotaFiscalInicial(),
        new RegraFracionamento(),
        new RegraGastoNoTetoDaCota(),
        new RegraPadraoMensalIdentico(),
        new RegraPicoDeConsumo(),
        new RegraGastoForaDaBaseEleitoral(),
        new RegraGastoEmRecesso(),
    ]);

    public IReadOnlyList<IRegraAnomalia> Regras => regras;

    public ResultadoDaAvaliacao Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var flags = new List<FlagAnomalia>();
        var porRegra = new Dictionary<string, int>(StringComparer.Ordinal);
        var silenciosas = new List<string>();

        var falhas = new List<FalhaDeRegra>();

        foreach (var regra in regras)
        {
            List<FlagAnomalia> doRegra;

            try
            {
                // Materializa dentro do try de proposito: as regras usam iteradores
                // preguicosos, e sem o ToList aqui a excecao so' apareceria mais adiante,
                // ja fora do alcance deste catch.
                doRegra = regra.Avaliar(contexto).ToList();
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // Uma regra com defeito nao pode apagar os achados das outras dez.
                falhas.Add(new FalhaDeRegra(regra.Codigo, erro.Message));
                continue;
            }

            // Carimba a versao antes de acumular: e' o que permite a notificacao
            // distinguir achado por despesa nova de achado por regra alterada.
            foreach (var flag in doRegra)
            {
                flag.RegistrarVersaoDoMotor(Versao);
            }

            flags.AddRange(doRegra);
            porRegra[regra.Codigo] = doRegra.Count;

            if (doRegra.Count == 0)
            {
                silenciosas.Add(regra.Codigo);
            }
        }

        return new ResultadoDaAvaliacao
        {
            Flags = flags,
            FlagsPorRegra = porRegra,
            RegrasSilenciosas = silenciosas,
            Falhas = falhas,
            VersaoDoMotor = Versao,
            TotalDeDespesasAvaliadas = contexto.Despesas.Count,
        };
    }
}
