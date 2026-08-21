using Explanando.Domain.Auditoria;

namespace Explanando.Domain.Regras;

/// <summary>O que uma passada de rede produziu.</summary>
public sealed record ResultadoDeRede
{
    public IReadOnlyList<FlagAnomalia> Flags { get; init; } = [];

    public IReadOnlyDictionary<string, int> FlagsPorRegra { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyList<FalhaDeRegra> Falhas { get; init; } = [];

    public int VersaoDoMotor { get; init; }

    public int ParlamentaresNaBase { get; init; }

    public int DespesasVarridas { get; init; }

    /// <summary>
    /// Quantos parlamentares a base precisaria ter para a regra significar alguma coisa.
    ///
    /// <para>Uma passada de rede sobre 20 deputados encontra concentracao onde nao ha:
    /// o universo e' pequeno demais para "exclusividade partidaria" ser informativa. O
    /// numero viaja junto do resultado para que ninguem publique achado de amostra.</para>
    /// </summary>
    public int PisoDeParlamentares { get; init; }

    public bool BaseSuficiente => ParlamentaresNaBase >= PisoDeParlamentares;
}

/// <summary>
/// Executa as regras que cruzam parlamentares, sobre a base inteira.
///
/// <para>Separado do <see cref="MotorDeRegras"/> porque roda em outro momento: aquele e' a
/// varredura de um perfil, este e' uma passada de fechamento apos a ingestao. Juntar os
/// dois obrigaria a varredura de um unico deputado a carregar a base toda.</para>
/// </summary>
public sealed class MotorDeRede(
    IReadOnlyList<IRegraDeRede> regras,
    int versao = MotorDeRegras.VersaoAtual)
{
    /// <summary>
    /// Minimo de parlamentares na base para uma regra de rede ser publicavel.
    ///
    /// A Camara tem 513 deputados. Com meia duzia na base, tres deles do mesmo partido
    /// usando o mesmo fornecedor e' aritmetica, nao padrao.
    /// </summary>
    public const int PisoDeParlamentaresParaRede = 100;

    public int Versao { get; } = versao;

    public static MotorDeRede Padrao() => new([new RegraConcentracaoPartidaria()]);

    public IReadOnlyList<IRegraDeRede> Regras => regras;

    public ResultadoDeRede Avaliar(ContextoDeRede contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var flags = new List<FlagAnomalia>();
        var porRegra = new Dictionary<string, int>(StringComparer.Ordinal);
        var falhas = new List<FalhaDeRegra>();

        foreach (var regra in regras)
        {
            List<FlagAnomalia> daRegra;

            try
            {
                // ToList dentro do try: a regra usa iterador preguicoso, e sem isto a
                // excecao escaparia deste catch.
                daRegra = regra.Avaliar(contexto).ToList();
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                falhas.Add(new FalhaDeRegra(regra.Codigo, erro.Message));
                continue;
            }

            foreach (var flag in daRegra)
            {
                flag.RegistrarVersaoDoMotor(Versao);
            }

            flags.AddRange(daRegra);
            porRegra[regra.Codigo] = daRegra.Count;
        }

        return new ResultadoDeRede
        {
            Flags = flags,
            FlagsPorRegra = porRegra,
            Falhas = falhas,
            VersaoDoMotor = Versao,
            ParlamentaresNaBase = contexto.TotalDeParlamentares,
            DespesasVarridas = contexto.Despesas.Count,
            PisoDeParlamentares = PisoDeParlamentaresParaRede,
        };
    }
}
