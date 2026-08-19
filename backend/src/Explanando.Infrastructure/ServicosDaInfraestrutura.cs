using Explanando.Application.Ingestao;
using Explanando.Infrastructure.Integracoes;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace Explanando.Infrastructure;

/// <summary>
/// Ponto unico de composicao da infraestrutura. Api e Worker chamam so' este metodo e
/// nao precisam saber que existe Npgsql, Polly ou HttpClient atras.
/// </summary>
public static class ServicosDaInfraestrutura
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection servicos,
        string stringDeConexao)
    {
        servicos.AdicionarPersistencia(stringDeConexao);
        servicos.AdicionarIntegracoes();

        servicos.AddScoped<IRepositorioDeIngestao, RepositorioDeIngestao>();
        servicos.AddScoped<IRepositorioDeCheckpoints, RepositorioDeCheckpoints>();
        servicos.AddSingleton<IRelogio, RelogioDoSistema>();

        servicos.AddScoped<ServicoDeIngestao>();
        servicos.AddScoped<ServicoDeColeta>();

        return servicos;
    }
}
