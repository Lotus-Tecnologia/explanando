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
    /// Apaga as flags das despesas informadas. A varredura recalcula do zero em vez de
    /// somar: rodar de novo com as mesmas regras nao pode dobrar o score, e uma regra
    /// afrouxada tem de conseguir <b>remover</b> um achado anterior.
    /// </summary>
    Task RemoverFlagsAsync(IReadOnlyCollection<Guid> despesaIds, CancellationToken cancelamento = default);

    void Adicionar(FlagAnomalia flag);

    void Adicionar(ScoreRiscoParlamentar score);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>O que uma varredura produziu para um parlamentar.</summary>
public sealed record ResultadoDaVarredura
{
    public required Guid ParlamentarId { get; init; }
    public int DespesasAvaliadas { get; init; }
    public int FlagsEmitidas { get; init; }
    public int FlagsVermelhas { get; init; }
    public decimal ScoreCalculado { get; init; }
    public decimal TaxaDeDespesasSinalizadas { get; init; }
    public IReadOnlyDictionary<string, int> FlagsPorRegra { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IReadOnlyList<string> RegrasSilenciosas { get; init; } = [];
    public IReadOnlyList<FalhaDeRegra> Falhas { get; init; } = [];
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
    IRelogioDaVarredura relogio)
{
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

        var contexto = new ContextoDeAvaliacao(parlamentar, despesas, fornecedores, limites);
        var avaliacao = motor.Avaliar(contexto);

        // Recalcula do zero: apaga o que a passada anterior escreveu antes de gravar.
        await repositorio.RemoverFlagsAsync(
            despesas.Select(despesa => despesa.Id).ToList(), cancelamento).ConfigureAwait(false);

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

        score.Recalcular(avaliacao.Flags, relogio.Agora);

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return new ResultadoDaVarredura
        {
            ParlamentarId = parlamentarId,
            DespesasAvaliadas = despesas.Count,
            FlagsEmitidas = avaliacao.Flags.Count,
            FlagsVermelhas = avaliacao.Flags.Count(flag => flag.EhVermelha),
            ScoreCalculado = score.ScoreCalculado,
            TaxaDeDespesasSinalizadas = avaliacao.TaxaDeDespesasSinalizadas,
            FlagsPorRegra = avaliacao.FlagsPorRegra,
            RegrasSilenciosas = avaliacao.RegrasSilenciosas,
            Falhas = avaliacao.Falhas,
        };
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
