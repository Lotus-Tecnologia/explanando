using Explanando.Application.Ciclo;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// O que a passada mensal precisa saber do banco.
///
/// <para>Nenhuma tabela nova: a data da última varredura de cada parlamentar já está em
/// <c>scores_risco</c>, gravada pela própria varredura. Uma tabela de "última execução"
/// duplicaria esse fato e criaria a chance de os dois divergirem — e o que vale é o que a
/// varredura fez, não o que um registro paralelo diz que ela fez.</para>
/// </summary>
public sealed class RepositorioDoCiclo(ExplanandoDbContext contexto) : IRepositorioDoCiclo
{
    /// <summary>
    /// O <c>Min</c> aqui é a regra inteira.
    ///
    /// <para>Com <c>Max</c>, uma única varredura paga por mês manteria a base "atualizada"
    /// para sempre enquanto os outros 512 envelheciam — exatamente a distorção de frescor
    /// que a passada mensal existe para corrigir.</para>
    /// </summary>
    public async Task<DateTimeOffset?> VarreduraMaisAntigaAsync(
        CancellationToken cancelamento = default)
    {
        var houveVarredura = await contexto.ScoresRisco
            .AsNoTracking()
            .AnyAsync(cancelamento)
            .ConfigureAwait(false);

        if (!houveVarredura)
        {
            return null;
        }

        return await contexto.ScoresRisco
            .AsNoTracking()
            .MinAsync(score => score.DataUltimaVarredura, cancelamento)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> IdsDosParlamentaresAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Parlamentares
            .AsNoTracking()
            .OrderBy(parlamentar => parlamentar.Id)
            .Select(parlamentar => parlamentar.Id)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);
}
