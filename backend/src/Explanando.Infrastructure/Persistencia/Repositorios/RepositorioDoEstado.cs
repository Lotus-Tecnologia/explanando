using Explanando.Application.Estado;
using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Acesso ao grafo do Estado.
///
/// <para>Carrega tudo <b>rastreado</b>, e não <c>AsNoTracking</c>: a coleta chama
/// <c>Atualizar</c> e <c>MarcarDesaparecida</c> nas entidades que leu, e sem rastreamento
/// essas mudanças não chegariam ao banco. São ~20 mil linhas pequenas; o custo é aceitável
/// uma vez por mês, e a alternativa seria comparar dois conjuntos com 20 mil idas ao banco.</para>
/// </summary>
public sealed class RepositorioDoEstado(ExplanandoDbContext contexto) : IRepositorioDoEstado
{
    public async Task<IReadOnlyList<UnidadeDoEstado>> CarregarUnidadesAsync(
        CancellationToken cancelamento = default) =>
        await contexto.UnidadesDoEstado.ToListAsync(cancelamento).ConfigureAwait(false);

    public void Adicionar(UnidadeDoEstado unidade) => contexto.UnidadesDoEstado.Add(unidade);

    public void Registrar(MudancaEstrutural mudanca) => contexto.MudancasEstruturais.Add(mudanca);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
