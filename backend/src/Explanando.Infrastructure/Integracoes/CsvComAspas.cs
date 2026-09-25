using System.Text;

namespace Explanando.Infrastructure.Integracoes;

/// <summary>
/// Separa uma linha de CSV respeitando as aspas — o formato dos arquivos do governo (Portal da
/// Transparência, TSE): ponto e vírgula, campos entre aspas, aspa escapada por aspa dupla.
///
/// <para>Um <c>Split(';')</c> bastaria hoje, e é justamente por isso que ele não serve: o dia em
/// que um nome trouxer um ponto e vírgula, ele deslocaria todas as colunas daquela linha sem erro
/// nenhum.</para>
/// </summary>
internal static class CsvComAspas
{
    public static List<string> Separar(string linha, char separador)
    {
        var campos = new List<string>(64);
        var atual = new StringBuilder(64);
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];

            if (c == '"')
            {
                // Aspas duplicadas dentro de campo entre aspas representam uma aspa.
                if (dentroDeAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                    continue;
                }

                dentroDeAspas = !dentroDeAspas;
                continue;
            }

            if (c == separador && !dentroDeAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            atual.Append(c);
        }

        campos.Add(atual.ToString());

        return campos;
    }
}
