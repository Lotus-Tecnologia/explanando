using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;

namespace Explanando.Application.Auditoria;

/// <summary>Porta de leitura e escrita da varredura.</summary>
public interface IRepositorioDeVarredura
{
    Task<Parlamentar?> ObterParlamentarAsync(Guid parlamentarId, CancellationToken cancelamento = default);

    Task<IReadOnlyList<Despesa>> ObterDespesasAsync(Guid parlamentarId, CancellationToken cancelamento = default);

    /// <summary>Fornecedores referenciados pelas despesas informadas.</summary>
    Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync(
        IReadOnlyCollection<Guid> fornecedorIds,
        CancellationToken cancelamento = default);

    Task<IReadOnlyList<LimiteCota>> ObterLimitesAsync(string uf, CancellationToken cancelamento = default);

    Task<ScoreRiscoParlamentar?> ObterScoreAsync(Guid parlamentarId, CancellationToken cancelamento = default);

    /// <summary>
    /// As doações de pessoa física à campanha do parlamentar. <b>Nulo</b> quando não há
    /// campanha ligada a ele na base do TSE — e nulo é diferente de lista vazia (ADR-0009).
    /// </summary>
    Task<IReadOnlyList<Domain.Eleicoes.DoacaoEleitoral>?> ObterDoacoesDaCampanhaAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    /// <summary>
    /// O ponto do historico daquele dia, se ja existir. A varredura roda mais de uma vez
    /// no mesmo dia — cada solicitacao paga dispara uma — e o indice e' unico por
    /// (parlamentar, data).
    /// </summary>
    Task<HistoricoScore?> ObterPontoDoHistoricoAsync(
        Guid parlamentarId,
        DateOnly referencia,
        CancellationToken cancelamento = default);

    /// <summary>
    /// As medicoes de cobertura ja gravadas para o parlamentar, por codigo de regra.
    ///
    /// Rastreadas, porque a varredura as atualiza no lugar: o indice e' unico por
    /// (parlamentar, regra) e inserir de novo violaria a chave.
    /// </summary>
    Task<IReadOnlyDictionary<string, CoberturaDoParlamentar>> ObterCoberturaAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    /// <summary>
    /// Apaga as flags que <b>esta passada</b> produziu antes, nas despesas informadas. A
    /// varredura recalcula do zero em vez de somar: rodar de novo com as mesmas regras nao
    /// pode dobrar o score, e uma regra afrouxada tem de conseguir <b>remover</b> um achado
    /// anterior.
    ///
    /// <para><b>Recorta pelo codigo da regra, e nao so' pela despesa.</b> A passada de rede
    /// grava achados nas mesmas despesas; apagar por despesa levava junto o REG_NET_004 a
    /// cada varredura individual, e o indice nunca os contava.</para>
    /// </summary>
    Task RemoverFlagsAsync(
        IReadOnlyCollection<Guid> despesaIds,
        IReadOnlyCollection<string> codigosDaPassada,
        CancellationToken cancelamento = default);

    /// <summary>
    /// As flags gravadas nessas despesas por <b>outra</b> passada — hoje, a de rede. Entram
    /// no score junto com as desta, porque o leitor ve as duas na mesma lista de achados.
    /// </summary>
    Task<IReadOnlyList<FlagAnomalia>> ObterFlagsDeOutrasPassadasAsync(
        IReadOnlyCollection<Guid> despesaIds,
        IReadOnlyCollection<string> codigosDaPassada,
        CancellationToken cancelamento = default);

    void Adicionar(FlagAnomalia flag);

    void Adicionar(ScoreRiscoParlamentar score);

    void Adicionar(HistoricoScore ponto);

    void Adicionar(CoberturaDoParlamentar cobertura);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>O que uma varredura produziu para um parlamentar.</summary>
public sealed record ResultadoDaVarredura
{
    public required Guid ParlamentarId { get; init; }
    public int DespesasAvaliadas { get; init; }
    public int FlagsEmitidas { get; init; }
    public int FlagsVermelhas { get; init; }

    /// <summary>O numero do B2C: soma em reais (ADR-0009).</summary>
    public decimal VolumeTotalGasto { get; init; }

    /// <summary>O numero do B2B: densidade por 100 despesas (ADR-0009).</summary>
    public decimal IndiceDeRisco { get; init; }

    /// <summary>Falso quando o parlamentar nao tem despesas bastantes para ser ranqueado.</summary>
    public bool AmostraSuficiente { get; init; }

    public decimal TaxaDeDespesasSinalizadas { get; init; }
    public IReadOnlyDictionary<string, int> FlagsPorRegra { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IReadOnlyList<string> RegrasSilenciosas { get; init; } = [];
    public IReadOnlyList<FalhaDeRegra> Falhas { get; init; } = [];

    /// <summary>
    /// O que a passada cobriu e o que nao cobriu.
    ///
    /// Viaja junto do resultado porque publicar as flags sem isto deixa o leitor supor que
    /// o silencio de uma regra significa aprovacao, quando pode significar que ela nao teve
    /// dado para rodar.
    /// </summary>
    public MetadadosDeCobertura Cobertura { get; init; } = new();
}

/// <summary>
/// Roda o motor de regras sobre um parlamentar e persiste o resultado.
///
/// Monta o contexto inteiro em memoria antes de avaliar: o motor nao toca banco nem rede,
/// e isso garante que uma regra nova nao introduza consulta N+1 sem ninguem perceber.
/// </summary>
public sealed class ServicoDeVarredura(
    IRepositorioDeVarredura repositorio,
    MotorDeRegras motor,
    IRelogioDaVarredura relogio,
    ParametrosDoMotor? parametros = null)
{
    private readonly ParametrosDoMotor _parametros = parametros ?? ParametrosDoMotor.Padrao;

    public async Task<ResultadoDaVarredura> VarrerAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        var parlamentar = await repositorio.ObterParlamentarAsync(parlamentarId, cancelamento)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Parlamentar {parlamentarId} nao encontrado.");

        var despesas = await repositorio.ObterDespesasAsync(parlamentarId, cancelamento)
            .ConfigureAwait(false);

        var fornecedorIds = despesas
            .Select(despesa => despesa.FornecedorId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var fornecedores = await repositorio.ObterFornecedoresAsync(fornecedorIds, cancelamento)
            .ConfigureAwait(false);

        var limites = await repositorio.ObterLimitesAsync(parlamentar.Uf, cancelamento)
            .ConfigureAwait(false);

        var doacoes = await repositorio.ObterDoacoesDaCampanhaAsync(parlamentarId, cancelamento)
            .ConfigureAwait(false);

        var contexto = new ContextoDeAvaliacao(
            parlamentar, despesas, fornecedores, limites, _parametros, doacoes);
        var avaliacao = motor.Avaliar(contexto);

        // Recalcula do zero: apaga o que a passada anterior escreveu antes de gravar — mas
        // so' o que ESTA passada escreve. O achado de rede e' de outra passada e fica.
        var despesaIds = despesas.Select(despesa => despesa.Id).ToList();
        var codigosDaPassada = motor.Regras.Select(regra => regra.Codigo).ToList();

        await repositorio.RemoverFlagsAsync(despesaIds, codigosDaPassada, cancelamento)
            .ConfigureAwait(false);

        var flagsDeOutrasPassadas = await repositorio
            .ObterFlagsDeOutrasPassadasAsync(despesaIds, codigosDaPassada, cancelamento)
            .ConfigureAwait(false);

        foreach (var flag in avaliacao.Flags)
        {
            repositorio.Adicionar(flag);
        }

        var score = await repositorio.ObterScoreAsync(parlamentarId, cancelamento).ConfigureAwait(false);

        if (score is null)
        {
            score = ScoreRiscoParlamentar.Criar(parlamentarId);
            repositorio.Adicionar(score);
        }

        // O indice conta tudo o que a pagina lista. Sem as flags de rede aqui, a soma das
        // parcelas por regra nao fecharia com o indice publicado.
        score.Recalcular(
            despesas,
            [.. avaliacao.Flags, .. flagsDeOutrasPassadas],
            relogio.Agora,
            _parametros.PisoAmostralParaIndiceDeRisco,
            despesasComFornecedor: despesas.Count(despesa => contexto.FornecedorDe(despesa) is not null),
            _parametros.PisoDeCoberturaDeFornecedor);

        await RegistrarNoHistoricoAsync(score, avaliacao.VersaoDoMotor, cancelamento)
            .ConfigureAwait(false);

        await RegistrarCoberturaAsync(parlamentarId, avaliacao, cancelamento)
            .ConfigureAwait(false);

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return new ResultadoDaVarredura
        {
            ParlamentarId = parlamentarId,
            DespesasAvaliadas = despesas.Count,
            FlagsEmitidas = avaliacao.Flags.Count,
            FlagsVermelhas = avaliacao.Flags.Count(flag => flag.EhVermelha),
            VolumeTotalGasto = score.VolumeTotalGasto,
            IndiceDeRisco = score.IndiceDeRisco,
            AmostraSuficiente = score.AmostraSuficiente,
            TaxaDeDespesasSinalizadas = avaliacao.TaxaDeDespesasSinalizadas,
            FlagsPorRegra = avaliacao.FlagsPorRegra,
            RegrasSilenciosas = avaliacao.RegrasSilenciosas,
            Falhas = avaliacao.Falhas,
            Cobertura = avaliacao.Cobertura,
        };
    }

    /// <summary>
    /// Grava o que cada regra conseguiu avaliar.
    ///
    /// <para>Sem isto a <see cref="MetadadosDeCobertura"/> morre com a passada, e a API fica
    /// sem como distinguir "avaliei e esta limpo" de "nao tive como avaliar" — distincao que
    /// o ADR-0013 tornou obrigatoria na interface.</para>
    /// </summary>
    private async Task RegistrarCoberturaAsync(
        Guid parlamentarId,
        ResultadoDaAvaliacao avaliacao,
        CancellationToken cancelamento)
    {
        var existentes = await repositorio
            .ObterCoberturaAsync(parlamentarId, cancelamento)
            .ConfigureAwait(false);

        var momento = relogio.Agora;

        foreach (var medicao in avaliacao.Cobertura.PorRegra)
        {
            if (existentes.TryGetValue(medicao.Codigo, out var registro))
            {
                registro.Atualizar(medicao, avaliacao.VersaoDoMotor, momento);
                continue;
            }

            repositorio.Adicionar(CoberturaDoParlamentar.Registrar(
                parlamentarId, medicao, avaliacao.VersaoDoMotor, momento));
        }
    }

    /// <summary>
    /// Fotografa os dois numeros do dia, com a versao do motor que os produziu.
    ///
    /// A versao e' o que impede o grafico de mentir: sem ela, um degrau na curva ficaria
    /// indistinguivel entre "o parlamentar mudou" e "as regras mudaram".
    /// </summary>
    private async Task RegistrarNoHistoricoAsync(
        ScoreRiscoParlamentar score,
        int versaoDoMotor,
        CancellationToken cancelamento)
    {
        var momento = relogio.Agora;
        var referencia = DateOnly.FromDateTime(momento.UtcDateTime);

        var existente = await repositorio
            .ObterPontoDoHistoricoAsync(score.ParlamentarId, referencia, cancelamento)
            .ConfigureAwait(false);

        if (existente is null)
        {
            repositorio.Adicionar(HistoricoScore.Capturar(score, versaoDoMotor, momento));
            return;
        }

        // Regrava em vez de inserir: o indice e' unico por dia, e a segunda varredura do
        // mesmo dia falharia por chave duplicada.
        existente.Regravar(score, versaoDoMotor, momento);
    }
}

/// <summary>
/// Relogio da varredura. Existe separado do relogio da ingestao para que os dois possam
/// ser congelados independentemente nos testes.
/// </summary>
public interface IRelogioDaVarredura
{
    DateTimeOffset Agora { get; }
}

public sealed class RelogioDaVarreduraDoSistema : IRelogioDaVarredura
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
