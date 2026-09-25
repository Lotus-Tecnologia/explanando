using System.Text;

namespace Explanando.Domain.Comum;

/// <summary>
/// Um nome reduzido à forma que dois cadastros diferentes escrevem igual: maiúsculas, sem
/// acento, sem hífen e com um espaço só entre as palavras.
///
/// <para><b>Normalização, e não aproximação.</b> "Acácio da Silva" e "ACACIO DA SILVA" viram a
/// mesma string; "Acácio Silva" continua outra. A Receita, a Câmara e o TSE grafam o mesmo nome
/// com e sem acento, e isso não é diferença de pessoa. Distância de edição seria — e o projeto
/// não casa por ela em lugar nenhum.</para>
///
/// <para><b>Tabela própria, e não <c>string.Normalize</c>.</b> A solução roda com
/// <c>InvariantGlobalization</c>, e nesse modo a decomposição Unicode não acontece: "JOSÉ"
/// continuava "JOSÉ", e o nome acentuado nunca casava. Os nomes brasileiros cabem no Latin-1 e
/// no Latin Extended-A, e a tabela cobre os dois.</para>
/// </summary>
public static class NomeNormalizado
{
    public static string De(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return string.Empty;
        }

        var construtor = new StringBuilder(nome.Length);
        var espacoPendente = false;

        foreach (var caractere in nome)
        {
            if (char.IsWhiteSpace(caractere) || caractere == '-')
            {
                espacoPendente = construtor.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                construtor.Append(' ');
                espacoPendente = false;
            }

            construtor.Append(SemAcento(char.ToUpperInvariant(caractere)));
        }

        return construtor.ToString();
    }

    private static char SemAcento(char c) => c switch
    {
        'À' or 'Á' or 'Â' or 'Ã' or 'Ä' or 'Å' or 'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' => 'A',
        'È' or 'É' or 'Ê' or 'Ë' or 'è' or 'é' or 'ê' or 'ë' => 'E',
        'Ì' or 'Í' or 'Î' or 'Ï' or 'ì' or 'í' or 'î' or 'ï' => 'I',
        'Ò' or 'Ó' or 'Ô' or 'Õ' or 'Ö' or 'ò' or 'ó' or 'ô' or 'õ' or 'ö' => 'O',
        'Ù' or 'Ú' or 'Û' or 'Ü' or 'ù' or 'ú' or 'û' or 'ü' => 'U',
        'Ç' or 'ç' => 'C',
        'Ñ' or 'ñ' => 'N',
        'Ý' or 'ý' or 'ÿ' => 'Y',
        _ => c,
    };
}
