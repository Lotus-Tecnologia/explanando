namespace Explanando.Application.Consultas;

/// <summary>Uma despesa reduzida ao que a escada de pagamentos precisa.</summary>
public sealed record PagamentoBruto(
    string Documento,
    string? NomeFornecedor,
    int Ano,
    int Mes,
    decimal Valor,
    DateTimeOffset DataDocumento);

/// <summary>Um ano da escada, e se ele se compara com os outros.</summary>
public sealed record AnoDaEscada
{
    public required int Ano { get; init; }

    /// <summary>Em quantos meses deste ano o parlamentar teve despesa.</summary>
    public required int MesesComDespesa { get; init; }

    /// <summary>
    /// Verdadeiro quando o ano não tem os doze meses com despesa — o primeiro ano do mandato,
    /// o ano corrente, uma licença. <b>Não se calcula variação contra ano parcial</b>: onze
    /// meses contra doze inflam o salto sem que nada tenha mudado.
    /// </summary>
    public bool Parcial => MesesComDespesa < 12;
}

/// <summary>Quanto um fornecedor recebeu num ano.</summary>
public sealed record ValorNoAno
{
    public required int Ano { get; init; }

    public decimal Valor { get; init; }

    public int Despesas { get; init; }
}

/// <summary>Um fornecedor, ano a ano.</summary>
public sealed record FornecedorNaEscada
{
    /// <summary>CNPJ inteiro ou CPF mascarado, como a despesa o publica.</summary>
    public required string Documento { get; init; }

    public required string Nome { get; init; }

    public decimal Total { get; init; }

    public int Despesas { get; init; }

    /// <summary>Um valor por ano da escada, na ordem dos anos — zero onde não houve pagamento.</summary>
    public IReadOnlyList<ValorNoAno> PorAno { get; init; } = [];

    /// <summary>
    /// A variação entre os dois últimos anos completos seguidos, de 0 a N (1 = +100%).
    ///
    /// <para>Nula quando não há dois anos completos seguidos, ou quando o fornecedor não
    /// recebeu nos dois: um fornecedor com um ano só não tem crescimento — tem começo.</para>
    /// </summary>
    public decimal? Variacao { get; init; }
}

/// <summary>Os maiores fornecedores de um parlamentar, ano a ano — a Fase 4 do plano.</summary>
public sealed record PagamentosPorFornecedor
{
    public IReadOnlyList<AnoDaEscada> Anos { get; init; } = [];

    /// <summary>Os anos entre os quais a <see cref="FornecedorNaEscada.Variacao"/> foi medida.</summary>
    public int? AnoBaseDaVariacao { get; init; }

    public int? AnoDaVariacao { get; init; }

    public IReadOnlyList<FornecedorNaEscada> Fornecedores { get; init; } = [];

    /// <summary>Quantos fornecedores o parlamentar teve no total, além dos listados.</summary>
    public int TotalDeFornecedores { get; init; }
}

/// <summary>
/// Monta a escada: "2023 — 50 mil · 2024 — 200 mil · 2025 — 800 mil", por fornecedor.
///
/// <para>Aritmética pura, fora da consulta, porque as duas travas do plano são regra e
/// precisam de teste sem banco: o ano parcial é marcado e não entra em variação, e um
/// fornecedor com um ano só não ganha seta.</para>
/// </summary>
public static class EscadaDePagamentos
{
    public const int FornecedoresPadrao = 10;

    public static PagamentosPorFornecedor Montar(
        IReadOnlyCollection<PagamentoBruto> pagamentos,
        int quantos = FornecedoresPadrao)
    {
        ArgumentNullException.ThrowIfNull(pagamentos);

        if (pagamentos.Count == 0)
        {
            return new PagamentosPorFornecedor();
        }

        var anos = pagamentos
            .GroupBy(pagamento => pagamento.Ano)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new AnoDaEscada
            {
                Ano = grupo.Key,
                MesesComDespesa = grupo.Select(pagamento => pagamento.Mes).Distinct().Count(),
            })
            .ToList();

        // Os dois últimos anos completos E seguidos. Completo com um buraco no meio (2023 e
        // 2025, sem 2024) não é "de um ano para o outro".
        var completos = anos.Where(ano => !ano.Parcial).Select(ano => ano.Ano).ToHashSet();
        var anoDaVariacao = completos
            .Where(ano => completos.Contains(ano - 1))
            .DefaultIfEmpty()
            .Max();

        int? atual = anoDaVariacao == 0 ? null : anoDaVariacao;
        int? anterior = atual - 1;

        var porFornecedor = pagamentos
            .GroupBy(pagamento => pagamento.Documento, StringComparer.Ordinal)
            .Select(grupo =>
            {
                var porAno = anos
                    .Select(ano => new ValorNoAno
                    {
                        Ano = ano.Ano,
                        Valor = grupo.Where(p => p.Ano == ano.Ano).Sum(p => p.Valor),
                        Despesas = grupo.Count(p => p.Ano == ano.Ano),
                    })
                    .ToList();

                return new FornecedorNaEscada
                {
                    Documento = grupo.Key,

                    // O nome da nota mais recente: o fornecedor muda de razão social, e o
                    // nome de hoje é o que o leitor vai procurar.
                    Nome = grupo
                        .OrderByDescending(p => p.DataDocumento)
                        .Select(p => p.NomeFornecedor)
                        .FirstOrDefault(nome => !string.IsNullOrWhiteSpace(nome))
                        ?? "Fornecedor não identificado",

                    Total = grupo.Sum(p => p.Valor),
                    Despesas = grupo.Count(),
                    PorAno = porAno,
                    Variacao = Variar(porAno, anterior, atual),
                };
            })
            .ToList();

        return new PagamentosPorFornecedor
        {
            Anos = anos,
            AnoBaseDaVariacao = anterior,
            AnoDaVariacao = atual,
            TotalDeFornecedores = porFornecedor.Count,
            Fornecedores = [.. porFornecedor
                .OrderByDescending(fornecedor => fornecedor.Total)
                .ThenBy(fornecedor => fornecedor.Documento, StringComparer.Ordinal)
                .Take(quantos)],
        };
    }

    private static decimal? Variar(IReadOnlyList<ValorNoAno> porAno, int? anterior, int? atual)
    {
        if (anterior is null || atual is null)
        {
            return null;
        }

        var antes = porAno.FirstOrDefault(valor => valor.Ano == anterior)?.Valor ?? 0m;
        var depois = porAno.FirstOrDefault(valor => valor.Ano == atual)?.Valor ?? 0m;

        // Sem pagamento em um dos dois, não há "crescimento": há começo ou fim. E valor
        // negativo — ano de estorno maior que o gasto — faria a porcentagem trocar de sinal.
        return antes <= 0m || depois <= 0m ? null : (depois - antes) / antes;
    }
}
