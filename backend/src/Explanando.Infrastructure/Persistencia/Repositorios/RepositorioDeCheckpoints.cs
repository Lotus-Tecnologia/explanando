using Explanando.Application.Ingestao;
using Explanando.Domain.Ingestao;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

public sealed class RepositorioDeCheckpoints(ExplanandoDbContext contexto) : IRepositorioDeCheckpoints
{
    public Task<CheckpointDeIngestao?> ObterAsync(
        int idExternoParlamentar,
        int legislatura,
        CancellationToken cancelamento = default) =>
        contexto.CheckpointsDeIngestao
            .FirstOrDefaultAsync(
                checkpoint => checkpoint.IdExternoParlamentar == idExternoParlamentar
                    && checkpoint.Legislatura == legislatura,
                cancelamento);

    public void Adicionar(CheckpointDeIngestao checkpoint) =>
        contexto.CheckpointsDeIngestao.Add(checkpoint);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
