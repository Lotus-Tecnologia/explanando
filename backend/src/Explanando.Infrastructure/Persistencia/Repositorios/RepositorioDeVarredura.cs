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
        IReadOnlyCollection<string> codigosDaPassada,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(despesaIds);
        ArgumentNullException.ThrowIfNull(codigosDaPassada);

        if (despesaIds.Count == 0 || codigosDaPassada.Count == 0)
        {
            return;
        }

        await contexto.FlagsAnomalias
            .Where(flag => despesaIds.Contains(flag.DespesaId)
                && codigosDaPassada.Contains(flag.CodigoRegra))
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FlagAnomalia>> ObterFlagsDeOutrasPassadasAsync(
        IReadOnlyCollection<Guid> despesaIds,
        IReadOnlyCollection<string> codigosDaPassada,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(despesaIds);
        ArgumentNullException.ThrowIfNull(codigosDaPassada);

        if (despesaIds.Count == 0)
        {
            return [];
        }

        return await contexto.FlagsAnomalias
            .AsNoTracking()
            .Where(flag => despesaIds.Contains(flag.DespesaId)
                && !codigosDaPassada.Contains(flag.CodigoRegra))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public Task<HistoricoScore?> ObterPontoDoHistoricoAsync(
        Guid parlamentarId,
        DateOnly referencia,
        CancellationToken cancelamento = default) =>
        // Rastreado de proposito: quando existe, o servico chama Regravar nele, e o EF
        // precisa enxergar a alteracao.
        contexto.HistoricoScores.FirstOrDefaultAsync(
            ponto => ponto.ParlamentarId == parlamentarId && ponto.Referencia == referencia,
            cancelamento);

    public void Adicionar(FlagAnomalia flag) => contexto.FlagsAnomalias.Add(flag);

    public void Adicionar(ScoreRiscoParlamentar score) => contexto.ScoresRisco.Add(score);

    public void Adicionar(HistoricoScore ponto) => contexto.HistoricoScores.Add(ponto);

    public void Adicionar(CoberturaDoParlamentar cobertura) =>
        contexto.CoberturasDeRegra.Add(cobertura);

    public async Task<IReadOnlyDictionary<string, CoberturaDoParlamentar>> ObterCoberturaAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        // Rastreado de proposito: o servico chama Atualizar nestes objetos.
        await contexto.CoberturasDeRegra
            .Where(cobertura => cobertura.ParlamentarId == parlamentarId)
            .ToDictionaryAsync(cobertura => cobertura.CodigoRegra, cancelamento)
            .ConfigureAwait(false);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
