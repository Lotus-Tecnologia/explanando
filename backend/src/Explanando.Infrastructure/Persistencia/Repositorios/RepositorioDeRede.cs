using Explanando.Application.Auditoria;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Carrega a base inteira para a passada de rede.
///
/// <para><b>Lê tudo de uma vez, e isso é deliberado</b>: a regra de concentração agrupa por
/// fornecedor sobre todos os parlamentares, e paginar não ajudaria — um fornecedor com
/// deputados em páginas diferentes ficaria invisível. Com 513 deputados e ordem de mil notas
/// cada, são centenas de milhares de linhas, então esta passada roda no worker e nunca numa
/// requisição HTTP.</para>
/// </summary>
public sealed class RepositorioDeRede(ExplanandoDbContext contexto) : IRepositorioDeRede
{
    public async Task<IReadOnlyList<Parlamentar>> ObterParlamentaresAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Parlamentares.AsNoTracking().ToListAsync(cancelamento).ConfigureAwait(false);

    public async Task<IReadOnlyList<Despesa>> ObterDespesasAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Despesas.AsNoTracking().ToListAsync(cancelamento).ConfigureAwait(false);

    public async Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Fornecedores.AsNoTracking().ToListAsync(cancelamento).ConfigureAwait(false);

    public async Task<IReadOnlyCollection<Guid>> RemoverFlagsDeRedeAsync(
        IReadOnlyCollection<string> codigosDeRegra,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(codigosDeRegra);

        if (codigosDeRegra.Count == 0)
        {
            return [];
        }

        var donos = await contexto.FlagsAnomalias
            .Where(flag => codigosDeRegra.Contains(flag.CodigoRegra))
            .Join(
                contexto.Despesas,
                flag => flag.DespesaId,
                despesa => despesa.Id,
                (_, despesa) => despesa.ParlamentarId)
            .Distinct()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        await contexto.FlagsAnomalias
            .Where(flag => codigosDeRegra.Contains(flag.CodigoRegra))
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);

        return donos;
    }

    public void Adicionar(FlagAnomalia flag) => contexto.FlagsAnomalias.Add(flag);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
