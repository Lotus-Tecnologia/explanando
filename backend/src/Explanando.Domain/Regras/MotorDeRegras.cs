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

    /// <summary>
    /// O que a passada cobriu, regra a regra.
    ///
    /// Publicar as flags sem isto deixa o leitor supor que o silencio de uma regra
    /// significa aprovacao, quando pode significar que ela nao teve dado para rodar.
    /// </summary>
    public MetadadosDeCobertura Cobertura { get; init; } = new();

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
    /// <item><b>3</b> — REG_NET_004 (concentracao partidaria) e metadados de cobertura.</item>
    /// </list>
    /// </summary>
    public const int VersaoAtual = 3;

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
        var cobertura = new List<CoberturaDaRegra>();

        var falhas = new List<FalhaDeRegra>();

        // Uma passada so' sobre as despesas, reaproveitada por todas as regras: medir a
        // disponibilidade dentro de cada regra faria 11 varreduras onde basta uma.
        var disponibilidade = contexto.Despesas
            .Select(despesa => DisponibilidadeDeDados.Em(contexto, despesa))
            .ToList();

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

                cobertura.Add(new CoberturaDaRegra
                {
                    Codigo = regra.Codigo,
                    Situacao = SituacaoDaRegra.Falhou,
                    MotivoDaFalha = erro.Message,
                });

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
            cobertura.Add(Medir(regra, doRegra.Count, disponibilidade));

            if (doRegra.Count == 0)
            {
                silenciosas.Add(regra.Codigo);
            }
        }

        var competencias = contexto.Despesas.Select(despesa => despesa.Competencia).ToList();

        return new ResultadoDaAvaliacao
        {
            Flags = flags,
            FlagsPorRegra = porRegra,
            RegrasSilenciosas = silenciosas,
            Falhas = falhas,
            VersaoDoMotor = Versao,
            Cobertura = new MetadadosDeCobertura
            {
                PorRegra = cobertura,
                DespesasAvaliadas = contexto.Despesas.Count,
                PrimeiraCompetencia = competencias.Count == 0 ? null : competencias.Min(),
                UltimaCompetencia = competencias.Count == 0 ? null : competencias.Max(),
                PisoAmostral = contexto.Parametros.PisoAmostralParaIndiceDeRisco,
                VersaoDoMotor = Versao,
            },
            TotalDeDespesasAvaliadas = contexto.Despesas.Count,
        };
    }

    /// <summary>
    /// Conta em quantas despesas a regra tinha todo o dado de que declara precisar, e
    /// traduz isso na situacao que a pagina vai exibir.
    /// </summary>
    private static CoberturaDaRegra Medir(
        IRegraAnomalia regra,
        int flagsEmitidas,
        List<DadoExigido> disponibilidade)
    {
        var exigido = regra.Exige;
        var comDado = 0;
        var ausente = DadoExigido.Nenhum;

        foreach (var disponivel in disponibilidade)
        {
            var faltando = exigido & ~disponivel;

            if (faltando == DadoExigido.Nenhum)
            {
                comDado++;
            }
            else
            {
                ausente |= faltando;
            }
        }

        var semDado = disponibilidade.Count - comDado;

        return new CoberturaDaRegra
        {
            Codigo = regra.Codigo,
            DespesasComDado = comDado,
            DespesasSemDado = semDado,
            FlagsEmitidas = flagsEmitidas,
            DadoAusente = ausente,
            Situacao = Classificar(flagsEmitidas, comDado, semDado, disponibilidade.Count),
        };
    }

    private static SituacaoDaRegra Classificar(int flags, int comDado, int semDado, int total)
    {
        // Sem despesa nenhuma nao ha o que cobrir; nao e' falta de dado da regra.
        if (total == 0)
        {
            return SituacaoDaRegra.SemAchados;
        }

        if (comDado == 0)
        {
            return SituacaoDaRegra.SemDado;
        }

        // Emitiu vem antes de CoberturaParcial de proposito: o que a regra achou e' um
        // fato sobre as despesas que ela examinou, independente de quantas pulou. A
        // extensao do que ficou de fora esta em DespesasSemDado, para quem quiser ler.
        if (flags > 0)
        {
            return SituacaoDaRegra.Emitiu;
        }

        return semDado > 0 ? SituacaoDaRegra.CoberturaParcial : SituacaoDaRegra.SemAchados;
    }
}
