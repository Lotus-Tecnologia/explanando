namespace Explanando.Domain.Comum;

/// <summary>
/// Par ano/mes de uma despesa da cota parlamentar. A API da Camara ja entrega os dois
/// campos prontos, e o CEAP raciocina por mes: e' a unidade de agregacao de REG_TX_001
/// (fracionamento), REG_TX_002 (teto da cota) e REG_TX_003 (padrao mensal identico).
/// </summary>
public readonly record struct Competencia : IComparable<Competencia>
{
    /// <summary>Antes disso nao existe dado de cota parlamentar publicado.</summary>
    private const int PrimeiroAnoPlausivel = 1900;

    private Competencia(int ano, int mes)
    {
        Ano = ano;
        Mes = mes;
    }

    public int Ano { get; }

    public int Mes { get; }

    public static Competencia Criar(int ano, int mes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ano, PrimeiroAnoPlausivel);
        ArgumentOutOfRangeException.ThrowIfLessThan(mes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mes, 12);

        return new Competencia(ano, mes);
    }

    public static Competencia De(DateTimeOffset data) => new(data.Year, data.Month);

    /// <summary>Quantos meses se passaram desde <paramref name="anterior"/>, cruzando anos.</summary>
    public int MesesDesde(Competencia anterior) =>
        ((Ano - anterior.Ano) * 12) + (Mes - anterior.Mes);

    public int CompareTo(Competencia outra)
    {
        var porAno = Ano.CompareTo(outra.Ano);
        return porAno != 0 ? porAno : Mes.CompareTo(outra.Mes);
    }

    public static bool operator <(Competencia esquerda, Competencia direita) =>
        esquerda.CompareTo(direita) < 0;

    public static bool operator >(Competencia esquerda, Competencia direita) =>
        esquerda.CompareTo(direita) > 0;

    public static bool operator <=(Competencia esquerda, Competencia direita) =>
        esquerda.CompareTo(direita) <= 0;

    public static bool operator >=(Competencia esquerda, Competencia direita) =>
        esquerda.CompareTo(direita) >= 0;

    public override string ToString() => $"{Ano:D4}-{Mes:D2}";
}
