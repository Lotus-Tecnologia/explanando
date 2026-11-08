using System.Diagnostics;
using System.Text.RegularExpressions;
using Shouldly;

namespace Explanando.Architecture.Tests;

/// <summary>
/// O repositório é público: nenhum arquivo versionado pode carregar CPF de pessoa real.
/// Todo CPF com dígito verificador válido precisa estar na lista de sintéticos abaixo.
/// Fixture nova com CPF de verdade quebra este teste antes de chegar ao histórico.
/// </summary>
public sealed partial class SemCpfRealVersionadoTests
{
    private static readonly HashSet<string> Sinteticos =
    [
        "11122233396", "12345678909", "52998224725", "98765432100",
        // Os que substituíram os CPFs reais de docs/contratos (Etapa 3).
        "90000000175", "90000000256", "90000000337", "90000000418",
    ];

    [GeneratedRegex(@"(?<![\d./-])(\d{3}\.\d{3}\.\d{3}-\d{2}|\d{11})(?![\d./-])")]
    private static partial Regex Candidato();

    [Fact]
    public void Nenhum_arquivo_versionado_tem_CPF_valido_fora_da_lista_de_sinteticos()
    {
        var raiz = Solucao.Raiz.Parent!.FullName;
        var achados = new List<string>();

        foreach (var arquivo in ArquivosVersionados(raiz))
        {
            var caminho = Path.Combine(raiz, arquivo);
            if (EhBinario(arquivo) || !File.Exists(caminho))
            {
                continue;
            }

            foreach (Match m in Candidato().Matches(File.ReadAllText(caminho)))
            {
                var digitos = new string(m.Value.Where(char.IsDigit).ToArray());
                if (DigitoConfere(digitos) && !Sinteticos.Contains(digitos))
                {
                    // O número não vai para a mensagem: o teste não pode ele mesmo publicar o CPF.
                    achados.Add($"{arquivo} (posição {m.Index})");
                }
            }
        }

        achados.ShouldBeEmpty();
    }

    private static bool EhBinario(string arquivo) =>
        Path.GetExtension(arquivo).ToLowerInvariant() is ".pdf" or ".ico" or ".png" or ".jpg" or ".gif" or ".woff" or ".woff2";

    private static bool DigitoConfere(string c)
    {
        if (c.Length != 11 || c.All(d => d == c[0]))
        {
            return false;
        }

        foreach (var n in new[] { 9, 10 })
        {
            var soma = 0;
            for (var i = 0; i < n; i++)
            {
                soma += (c[i] - '0') * (n + 1 - i);
            }

            if (soma * 10 % 11 % 10 != c[n] - '0')
            {
                return false;
            }
        }

        return true;
    }

    private static string[] ArquivosVersionados(string raiz)
    {
        using var git = Process.Start(new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = raiz,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        var saida = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0);
        return saida.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
