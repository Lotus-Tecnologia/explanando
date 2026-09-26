using Explanando.Application.Eleicoes;
using Explanando.Domain.Comum;
using Explanando.Domain.Eleicoes;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

public sealed class RepositorioDoTse(ExplanandoDbContext contexto) : IRepositorioDoTse
{
    public async Task<IReadOnlyList<Parlamentar>> ObterParlamentaresAsync(
        CancellationToken cancelamento = default) =>
        await contexto.Parlamentares.ToListAsync(cancelamento).ConfigureAwait(false);

    public async Task<IReadOnlySet<ChaveDeSocio>> ObterChavesDeSociosAsync(
        CancellationToken cancelamento = default)
    {
        var socios = await contexto.Sociedades
            .AsNoTracking()
            .Select(socio => new { socio.NomeSocio, socio.Cpf })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return socios
            .Where(socio => socio.Cpf.TemDigitos)
            .Select(socio => new ChaveDeSocio(NomeNormalizado.De(socio.NomeSocio), socio.Cpf.DigitosVisiveis))
            .ToHashSet();
    }

    public async Task LimparAnoAsync(int ano, CancellationToken cancelamento = default)
    {
        // Os votos saem em cascata com a candidatura.
        await contexto.Candidaturas
            .Where(candidatura => candidatura.AnoDaEleicao == ano)
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);

        await contexto.DoacoesEleitorais
            .Where(doacao => doacao.AnoDaEleicao == ano)
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A transação roda DENTRO da estratégia de execução — a conexão usa
    /// <c>EnableRetryOnFailure</c>, e o Npgsql recusa transação aberta fora dela (ver
    /// RepositorioDeCadeiras). Numa retentativa o trabalho recomeça do zero, então o
    /// rastreamento é esvaziado antes: sobra de uma tentativa que caiu viraria duplicata.
    /// </summary>
    public async Task EmUmaTransacaoAsync(
        Func<CancellationToken, Task> trabalho,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalho);

        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            contexto.ChangeTracker.Clear();

            await using var transacao = await contexto.Database
                .BeginTransactionAsync(cancelamento)
                .ConfigureAwait(false);

            await trabalho(cancelamento).ConfigureAwait(false);

            await transacao.CommitAsync(cancelamento).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    public void Adicionar(CandidaturaDoParlamentar candidatura) => contexto.Candidaturas.Add(candidatura);

    public void Adicionar(VotosNoMunicipio votos) => contexto.VotosPorMunicipio.Add(votos);

    public void Adicionar(DoacaoEleitoral doacao) => contexto.DoacoesEleitorais.Add(doacao);

    public async Task SalvarAsync(CancellationToken cancelamento = default)
    {
        await contexto.SaveChangesAsync(cancelamento).ConfigureAwait(false);

        // A carga passa de dezenas de milhares de linhas; rastrear todas deixaria cada
        // SaveChanges mais lento que o anterior.
        contexto.ChangeTracker.Clear();
    }
}
