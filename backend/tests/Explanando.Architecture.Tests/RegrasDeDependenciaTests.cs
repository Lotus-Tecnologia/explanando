namespace Explanando.Architecture.Tests;

/// <summary>
/// A separacao entre dominio e infraestrutura nao e' disciplina de revisao de codigo:
/// e' garantia de build. Se alguem violar a direcao das dependencias, o build fica vermelho.
///
/// Direcao permitida:
///     Domain  <-  Application  <-  Infrastructure
///                      ^                 ^
///                      +----- Api -------+
///                      +---- Worker -----+
/// </summary>
public sealed class RegrasDeDependenciaTests
{
    [Fact]
    public void Dominio_nao_referencia_nenhum_outro_projeto()
    {
        var dominio = Solucao.Projeto("Explanando.Domain");

        dominio.ProjetosReferenciados.ShouldBeEmpty(
            "o dominio e' o centro da cebola: nada do que ele precisa pode vir de fora dele");
    }

    [Fact]
    public void Dominio_nao_depende_de_nenhum_pacote_externo()
    {
        var dominio = Solucao.Projeto("Explanando.Domain");

        dominio.PacotesReferenciados.ShouldBeEmpty(
            "o dominio e' C# puro (BCL apenas). EF Core, MediatR e afins vivem na infraestrutura");
    }

    [Fact]
    public void Application_depende_somente_do_dominio()
    {
        var application = Solucao.Projeto("Explanando.Application");

        application.ProjetosReferenciados.ShouldBe(["Explanando.Domain"],
            "casos de uso orquestram o dominio; eles nao conhecem banco, HTTP nem fila");
    }

    [Fact]
    public void Infrastructure_nao_e_referenciada_por_dominio_nem_por_application()
    {
        string[] camadasInternas = ["Explanando.Domain", "Explanando.Application"];

        foreach (var nome in camadasInternas)
        {
            Solucao.Projeto(nome).ProjetosReferenciados.ShouldNotContain("Explanando.Infrastructure",
                $"{nome} apontar para a infraestrutura inverte a direcao da dependencia");
        }
    }

    [Theory]
    [InlineData("Explanando.Api")]
    [InlineData("Explanando.Worker")]
    public void Hosts_montam_application_e_infrastructure_sem_alcancar_o_dominio_direto(string nomeDoHost)
    {
        var host = Solucao.Projeto(nomeDoHost);

        host.ProjetosReferenciados.ShouldBe(
            ["Explanando.Application", "Explanando.Infrastructure"],
            $"{nomeDoHost} e' composition root: injeta a infraestrutura nos casos de uso e para por ai");
    }

    [Fact]
    public void Nenhum_projeto_de_producao_referencia_projeto_de_teste()
    {
        var producao = Solucao.Projetos.Where(projeto => projeto.EhDeProducao);

        foreach (var projeto in producao)
        {
            projeto.ProjetosReferenciados
                .ShouldNotContain(referencia => referencia.EndsWith("Tests", StringComparison.Ordinal),
                    $"{projeto.Nome} nao pode arrastar codigo de teste para producao");
        }
    }

    [Fact]
    public void Todo_projeto_de_producao_vive_em_src_e_todo_teste_vive_em_tests()
    {
        foreach (var projeto in Solucao.Projetos)
        {
            var pastaEsperada = projeto.Nome.EndsWith("Tests", StringComparison.Ordinal) ? "tests" : "src";

            projeto.Pasta.ShouldBe(pastaEsperada,
                $"{projeto.Nome} esta na pasta errada: o layout da solucao tem que ser legivel sem abrir a IDE");
        }
    }
}
