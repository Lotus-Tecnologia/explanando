using System.Xml.Linq;

namespace Explanando.Architecture.Tests;

/// <summary>
/// Central Package Management: as versoes vivem so' em Directory.Packages.props.
/// Versao solta num .csproj e' divergencia esperando para acontecer.
/// </summary>
public sealed class VersionamentoDePacotesTests
{
    [Fact]
    public void Nenhum_csproj_declara_versao_de_pacote_inline()
    {
        var infratores = new List<string>();

        foreach (var arquivo in ArquivosDeProjeto())
        {
            var comVersaoInline = XDocument.Load(arquivo.FullName)
                .Descendants("PackageReference")
                .Where(elemento => elemento.Attribute("Version") is not null)
                .Select(elemento => (string?)elemento.Attribute("Include") ?? "?");

            infratores.AddRange(comVersaoInline.Select(pacote => $"{arquivo.Name} -> {pacote}"));
        }

        infratores.ShouldBeEmpty(
            "as versoes pertencem a Directory.Packages.props, nao aos .csproj individuais");
    }

    [Fact]
    public void Todo_pacote_referenciado_tem_versao_central_declarada()
    {
        var central = XDocument.Load(Path.Combine(Solucao.Raiz.FullName, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Select(elemento => (string?)elemento.Attribute("Include"))
            .Where(nome => !string.IsNullOrWhiteSpace(nome))
            .Select(nome => nome!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var semVersaoCentral = Solucao.Projetos
            .SelectMany(projeto => projeto.PacotesReferenciados.Select(pacote => (projeto.Nome, pacote)))
            .Where(item => !central.Contains(item.pacote))
            .Select(item => $"{item.Nome} -> {item.pacote}")
            .Distinct()
            .ToList();

        semVersaoCentral.ShouldBeEmpty(
            "todo PackageReference precisa de um PackageVersion correspondente em Directory.Packages.props");
    }

    private static IEnumerable<FileInfo> ArquivosDeProjeto()
    {
        string[] pastas = ["src", "tests"];

        return pastas
            .Select(pasta => new DirectoryInfo(Path.Combine(Solucao.Raiz.FullName, pasta)))
            .Where(pasta => pasta.Exists)
            .SelectMany(pasta => pasta.EnumerateFiles("*.csproj", SearchOption.AllDirectories));
    }
}
