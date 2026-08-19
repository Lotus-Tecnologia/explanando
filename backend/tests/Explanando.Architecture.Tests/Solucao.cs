using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Explanando.Architecture.Tests;

/// <summary>
/// Le o layout real da solucao a partir dos arquivos .csproj em disco.
///
/// Ler o .csproj (e nao so' os assemblies via reflexao) e' proposital: uma ProjectReference
/// declarada mas ainda nao usada em codigo desaparece do assembly compilado, e passaria
/// despercebida numa checagem por reflexao. Aqui ela e' pega no ato da declaracao.
/// </summary>
internal static class Solucao
{
    private const string ArquivoDeSolucao = "Explanando.slnx";

    public static DirectoryInfo Raiz { get; } = LocalizarRaiz();

    public static IReadOnlyList<Projeto> Projetos { get; } = CarregarProjetos();

    public static Projeto Projeto(string nome) =>
        Projetos.SingleOrDefault(p => p.Nome == nome)
        ?? throw new InvalidOperationException(
            $"Projeto '{nome}' nao encontrado. Conhecidos: {string.Join(", ", Projetos.Select(p => p.Nome))}.");

    /// <summary>
    /// Ancora no caminho <b>do codigo-fonte</b>, e nao no diretorio de saida.
    ///
    /// A saida de build vive fora do repositorio (ver a nota sobre ArtifactsPath em
    /// Directory.Build.props), entao subir a partir de <c>AppContext.BaseDirectory</c>
    /// nunca encontraria a solution. <c>[CallerFilePath]</c> e' resolvido em tempo de
    /// compilacao e aponta para este proprio arquivo, dentro do repositorio.
    /// </summary>
    private static DirectoryInfo LocalizarRaiz([CallerFilePath] string arquivoDesteCodigo = "")
    {
        foreach (var ponto in new[] { Path.GetDirectoryName(arquivoDesteCodigo), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(ponto) || !Directory.Exists(ponto))
            {
                continue;
            }

            var diretorio = new DirectoryInfo(ponto);

            while (diretorio is not null && !diretorio.EnumerateFiles(ArquivoDeSolucao).Any())
            {
                diretorio = diretorio.Parent;
            }

            if (diretorio is not null)
            {
                return diretorio;
            }
        }

        throw new InvalidOperationException(
            $"Nao encontrei '{ArquivoDeSolucao}' a partir de '{arquivoDesteCodigo}' "
            + $"nem de '{AppContext.BaseDirectory}'.");
    }

    private static List<Projeto> CarregarProjetos()
    {
        var pastas = new[] { "src", "tests" };

        return pastas
            .Select(pasta => new DirectoryInfo(Path.Combine(Raiz.FullName, pasta)))
            .Where(pasta => pasta.Exists)
            .SelectMany(pasta => pasta.EnumerateFiles("*.csproj", SearchOption.AllDirectories))
            .Select(arquivo => Tests.Projeto.Ler(arquivo))
            .OrderBy(projeto => projeto.Nome, StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>Um projeto da solucao, com as dependencias que ele declara no proprio .csproj.</summary>
internal sealed record Projeto(
    string Nome,
    string Pasta,
    IReadOnlyList<string> ProjetosReferenciados,
    IReadOnlyList<string> PacotesReferenciados)
{
    public bool EhDeProducao => Pasta == "src";

    public static Projeto Ler(FileInfo arquivo)
    {
        var documento = XDocument.Load(arquivo.FullName);

        var projetos = documento.Descendants("ProjectReference")
            .Select(elemento => (string?)elemento.Attribute("Include"))
            .Where(caminho => !string.IsNullOrWhiteSpace(caminho))
            .Select(caminho => Path.GetFileNameWithoutExtension(caminho!.Replace('\\', '/')))
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        var pacotes = documento.Descendants("PackageReference")
            .Select(elemento => (string?)elemento.Attribute("Include"))
            .Where(nome => !string.IsNullOrWhiteSpace(nome))
            .Select(nome => nome!)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        var pasta = arquivo.Directory!.Parent!.Name;

        return new Projeto(Path.GetFileNameWithoutExtension(arquivo.Name), pasta, projetos, pacotes);
    }
}
