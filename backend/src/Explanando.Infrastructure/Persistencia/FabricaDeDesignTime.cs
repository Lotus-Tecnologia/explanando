using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Explanando.Infrastructure.Persistencia;

/// <summary>
/// Usada apenas pelas ferramentas do EF Core (<c>dotnet ef migrations</c>).
///
/// Existe para que gerar migration nao dependa de a Api estar configurada nem de haver
/// banco no ar: a string de conexao aqui so' precisa ser sintaticamente valida, porque
/// nenhuma conexao e' aberta para produzir o script.
/// </summary>
public sealed class FabricaDeDesignTime : IDesignTimeDbContextFactory<ExplanandoDbContext>
{
    private const string ConexaoDeDesenvolvimento =
        "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev";

    public ExplanandoDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
            ?? ConexaoDeDesenvolvimento;

        var opcoes = new DbContextOptionsBuilder<ExplanandoDbContext>()
            .UseNpgsql(conexao)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ExplanandoDbContext(opcoes);
    }
}
