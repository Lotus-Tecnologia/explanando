using Explanando.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Explanando.Integration.Tests;

/// <summary>
/// Sobe um Postgres real em contêiner e aplica as migrations nele.
///
/// Banco de verdade, e nao provedor em memoria, de proposito: o que precisa de prova aqui
/// e' justamente o que o provedor em memoria nao tem — indice unico que rejeita duplicata,
/// tipo <c>text[]</c>, coluna <c>numeric(18,2)</c>, conversao de objeto de valor e a
/// propria migration. Um teste contra InMemory passaria com o mapeamento errado.
/// </summary>
public sealed class BancoDeTestes : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("explanando_testes")
        .WithUsername("explanando")
        .WithPassword("explanando_teste")
        .Build();

    public string StringDeConexao => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var contexto = CriarContexto();
        await contexto.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ExplanandoDbContext CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<ExplanandoDbContext>()
            .UseNpgsql(StringDeConexao)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ExplanandoDbContext(opcoes);
    }
}

[CollectionDefinition(Nome)]
public sealed class ColecaoDeBanco : ICollectionFixture<BancoDeTestes>
{
    public const string Nome = "banco-postgres";
}
