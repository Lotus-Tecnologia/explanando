using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Explanando.Infrastructure.Persistencia;

/// <summary>
/// Registro do contexto no container. Fica na infraestrutura porque e' onde as escolhas
/// de tecnologia devem morar — Api e Worker apenas chamam, sem saber que ha Npgsql atras.
/// </summary>
public static class ServicosDePersistencia
{
    public static IServiceCollection AdicionarPersistencia(
        this IServiceCollection servicos,
        string stringDeConexao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stringDeConexao);

        servicos.AddDbContext<ExplanandoDbContext>(opcoes =>
            opcoes
                .UseNpgsql(stringDeConexao, npgsql => npgsql.EnableRetryOnFailure())
                // PascalCase no C#, snake_case no banco — sem poluir as entidades.
                .UseSnakeCaseNamingConvention());

        return servicos;
    }
}
