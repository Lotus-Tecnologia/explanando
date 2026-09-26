using Explanando.Application.Ciclo;
using Explanando.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Explanando.Integration.Tests;

/// <summary>
/// O ciclo mensal é o que o agendador da VPS dispara — e ele coletava só a Câmara. A coleta
/// injetada era a da Câmara; a do Senado só existia montada à mão dentro do comando
/// `coletar-senado`. Em produção, a despesa nova dos senadores nunca entraria sozinha
/// (achado ao empacotar a VPS, 2026-09-25).
/// </summary>
public sealed class CicloColetaAsDuasCasasTests
{
    [Fact]
    public void O_ciclo_resolvido_pelo_conteiner_tem_uma_coleta_por_casa()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AdicionarInfraestrutura("Host=localhost;Database=nenhum;Username=x;Password=x");

        using var provedor = servicos.BuildServiceProvider();
        using var escopo = provedor.CreateScope();

        var coletas = escopo.ServiceProvider.GetRequiredService<ColetasDoCiclo>();

        coletas.PorCasa.Count.ShouldBe(2);
        coletas.PorCasa.Distinct().Count().ShouldBe(2);
        escopo.ServiceProvider.GetRequiredService<ServicoDeCicloMensal>().ShouldNotBeNull();
    }
}
