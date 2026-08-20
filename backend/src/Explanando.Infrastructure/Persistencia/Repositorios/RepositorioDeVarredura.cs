using Explanando.Application.Auditoria;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

public sealed class RepositorioDeVarredura(ExplanandoDbContext contexto) : IRepositorioDeVarredura
{
    public Task<Parlamentar?> ObterParlamentarAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        contexto.Parlamentares.FirstOrDefaultAsync(p => p.Id == parlamentarId, cancelamento);

    public async Task<IReadOnlyList<Despesa>> ObterDespesasAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync(
        IReadOnlyCollection<Guid> fornecedorIds,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(fornecedorIds);

        if (fornecedorIds.Count == 0)
        {
            return [];
        }

        // Uma consulta so' para todos os fornecedores da passada: carregar um a um seria
        // N+1 sobre as ~1.200 despesas de um deputado.
        return await contexto.Fornecedores
            .AsNoTracking()
            .Where(fornecedor => fornecedorIds.Contains(fornecedor.Id))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LimiteCota>> ObterLimitesAsync(
        string uf,
        CancellationToken cancelamento = default) =>
        await contexto.LimitesCota
            .AsNoTracking()
            .Where(limite => limite.Uf == uf)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public Task<ScoreRiscoParlamentar?> ObterScoreAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        contexto.ScoresRisco.FirstOrDefaultAsync(s => s.ParlamentarId == parlamentarId, cancelamento);

    public async Task RemoverFlagsAsync(
        IReadOnlyCollection<Guid> despesaIds,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(despesaIds);

        if (despesaIds.Count == 0)
        {
            return;
        }

        await contexto.FlagsAnomalias
            .Where(flag => despesaIds.Contains(flag.DespesaId))
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public void Adicionar(FlagAnomalia flag) => contexto.FlagsAnomalias.Add(flag);

    public void Adicionar(ScoreRiscoParlamentar score) => contexto.ScoresRisco.Add(score);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
