namespace Explanando.Domain.Comum;

/// <summary>
/// CPF de socio como a Receita Federal o publica atraves da BrasilAPI: <c>***456789**</c>.
/// Dos 11 digitos, apenas 6 sao visiveis — as posicoes 4 a 9 (indices 3 a 8).
///
/// O tipo existe para impedir que esses 6 digitos sejam confundidos com um CPF completo em
/// algum ponto do sistema. A consequencia esta em REG_NET_001 (doacao cruzada): cruzar o
/// quadro societario com a base de doadores do TSE so' pode produzir <b>compatibilidade</b>,
/// nunca identidade. Ver ADR-0004 sobre a nomenclatura que isso exige na UI.
///
/// Efeito colateral util: como o dado completo nunca entra no sistema, nao ha CPF de socio
/// para vazar — parte da exigencia de LGPD se resolve na origem.
/// </summary>
public sealed record CpfParcial
{
    /// <summary>Quantidade de digitos que a fonte revela.</summary>
    private const int DigitosRevelados = 6;

    /// <summary>Primeiro indice visivel dentro de um CPF de 11 digitos.</summary>
    private const int PrimeiroIndiceVisivel = 3;

    private const int TamanhoDeCpf = 11;

    private CpfParcial(string digitosVisiveis, string mascarado)
    {
        DigitosVisiveis = digitosVisiveis;
        Mascarado = mascarado;
    }

    /// <summary>Os 6 digitos que a fonte revela. Vazio quando nao ha CPF informado.</summary>
    public string DigitosVisiveis { get; }

    /// <summary>A mascara como veio da origem, para exibicao direta na UI.</summary>
    public string Mascarado { get; }

    public bool TemDigitos => DigitosVisiveis.Length == DigitosRevelados;

    /// <summary>
    /// Teto de confianca de um cruzamento feito so' com estes digitos. Fica abaixo de 1
    /// por construcao: 5 digitos permanecem ocultos, o que deixa 10^5 combinacoes possiveis.
    /// Um match aqui e' indicio a ser somado ao nome do socio, nunca prova.
    /// </summary>
    public static decimal GrauDeConfiancaMaximo => 0.85m;

    public static CpfParcial Criar(string? mascaraDaOrigem)
    {
        if (string.IsNullOrWhiteSpace(mascaraDaOrigem))
        {
            return new CpfParcial(string.Empty, string.Empty);
        }

        var digitos = SomenteDigitos(mascaraDaOrigem);

        return digitos.Length == DigitosRevelados
            ? new CpfParcial(digitos, mascaraDaOrigem.Trim())
            : new CpfParcial(string.Empty, mascaraDaOrigem.Trim());
    }

    /// <summary>
    /// Mascara um CPF completo na mesma posicao em que a Receita mascara o do socio:
    /// <c>***NNNNNN**</c>, os digitos 4 a 9.
    ///
    /// <para>Existe para a carga do TSE, que publica o CPF do doador <b>inteiro</b>. O
    /// completo nao passa daqui: a entidade guarda so' o resultado, e o dado que o sistema
    /// nunca teve nao tem como vazar. Documento que nao tem 11 digitos devolve mascara vazia —
    /// e mascara vazia nao casa com ninguem.</para>
    /// </summary>
    public static CpfParcial DeCpfCompleto(string? cpfCompleto)
    {
        var digitos = SomenteDigitos(cpfCompleto ?? string.Empty);

        if (digitos.Length != TamanhoDeCpf)
        {
            return new CpfParcial(string.Empty, string.Empty);
        }

        var visiveis = digitos.Substring(PrimeiroIndiceVisivel, DigitosRevelados);
        return new CpfParcial(visiveis, $"***{visiveis}**");
    }

    /// <summary>
    /// Se um CPF completo — tipicamente da base de doadores do TSE — bate nos digitos
    /// que conhecemos. Verdadeiro significa <b>compativel</b>, e nada alem disso.
    /// </summary>
    public bool EhCompativelCom(string? cpfCompleto)
    {
        if (!TemDigitos || string.IsNullOrWhiteSpace(cpfCompleto))
        {
            return false;
        }

        var candidato = SomenteDigitos(cpfCompleto);

        if (candidato.Length != TamanhoDeCpf)
        {
            return false;
        }

        var trecho = candidato.AsSpan(PrimeiroIndiceVisivel, DigitosRevelados);
        return trecho.SequenceEqual(DigitosVisiveis);
    }

    public override string ToString() => Mascarado;

    /// <summary>
    /// Igualdade pelos digitos, ignorando a pontuacao da mascara: "***456789**" e
    /// "***.456.789-**" descrevem o mesmo socio e precisam deduplicar como um so'.
    /// </summary>
    public bool Equals(CpfParcial? outro) =>
        outro is not null
        && string.Equals(DigitosVisiveis, outro.DigitosVisiveis, StringComparison.Ordinal);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(DigitosVisiveis);

    private static string SomenteDigitos(string bruto)
    {
        var destino = new char[bruto.Length];
        var tamanho = 0;

        foreach (var caractere in bruto)
        {
            if (caractere is >= '0' and <= '9')
            {
                destino[tamanho++] = caractere;
            }
        }

        return new string(destino, 0, tamanho);
    }
}
