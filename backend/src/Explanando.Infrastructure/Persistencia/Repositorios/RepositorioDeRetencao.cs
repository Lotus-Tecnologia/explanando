using Explanando.Application.Retencao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Retencao;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

public sealed class RepositorioDeRetencao(ExplanandoDbContext contexto) : IRepositorioDeRetencao
{
    public async Task<IReadOnlyList<Guid>> ObterIdsDeParlamentaresAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Parlamentares
            .AsNoTracking()
            .Select(parlamentar => parlamentar.Id)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Despesa>> ObterDespesasAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ConsolidadoMensal>> ObterConsolidadosAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default) =>
        // Rastreado: o servico chama Recalcular e MarcarDetalheDescartado nestes objetos.
        await contexto.ConsolidadosMensais
            .Where(consolidado => consolidado.ParlamentarId == parlamentarId)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public void Adicionar(ConsolidadoMensal consolidado) =>
        contexto.ConsolidadosMensais.Add(consolidado);

    public async Task<int> RemoverDespesasAnterioresAsync(
        Guid parlamentarId,
        Competencia primeiraPreservada,
        CancellationToken cancelamento = default)
    {
        // SQL direto de proposito. Competencia e' objeto de valor com conversor para o
        // inteiro AAAAMM, e nem `Competencia < corte` nem `Competencia.Ano * 100 + Mes`
        // traduzem: o LINQ nao enxerga as partes do objeto convertido, e o operador `<`
        // e' definido em C#, nao no provedor. A codificacao AAAAMM preserva a ordem
        // cronologica, entao a comparacao numerica aqui e' exatamente a comparacao de
        // competencia — e o indice de (parlamentar, competencia) atende o filtro.
        var corte = (primeiraPreservada.Ano * 100) + primeiraPreservada.Mes;

        return await contexto.Database
            .ExecuteSqlAsync(
                $"DELETE FROM despesas WHERE parlamentar_id = {parlamentarId} AND competencia < {corte}",
                cancelamento)
            .ConfigureAwait(false);
    }

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
