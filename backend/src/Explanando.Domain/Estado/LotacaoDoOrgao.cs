using Explanando.Domain.Comum;

namespace Explanando.Domain.Estado;

/// <summary>Quantas pessoas ocupam uma sigla de função num órgão.</summary>
public sealed class ContagemPorFuncao : Entidade
{
    private ContagemPorFuncao()
    {
        // EF Core.
        Sigla = string.Empty;
    }

    internal ContagemPorFuncao(string sigla, int quantidade)
    {
        Sigla = sigla;
        Quantidade = quantidade;
    }

    /// <summary>
    /// A sigla como a fonte a escreve: <c>CD</c>, <c>FCE</c>, <c>FG</c>, <c>FEX</c>…
    ///
    /// <para>Guardada crua de propósito. Traduzir aqui — "cargo de direção", "função
    /// gratificada" — congelaria uma leitura nossa sobre um código que o Estado pode
    /// redefinir; a tradução é da camada de leitura, que pode mudar sem migration.</para>
    /// </summary>
    public string Sigla { get; private set; }

    public int Quantidade { get; private set; }

    public Guid LotacaoId { get; private set; }
}

/// <summary>Dados de uma lotação, já agregados, vindos do cadastro de pessoal.</summary>
public sealed record NovaLotacao
{
    public required Competencia Competencia { get; init; }

    /// <summary>O código SIAFI do órgão de exercício.</summary>
    public required int CodigoSiafi { get; init; }

    /// <summary>O nome do <b>órgão</b> como a fonte o escreve. Não é nome de pessoa.</summary>
    public required string NomeNaOrigem { get; init; }

    /// <summary>O nó do grafo, quando a ponte alcança. Nulo é resposta legítima.</summary>
    public int? CodigoSiorg { get; init; }

    public required int Vinculos { get; init; }

    /// <summary>Sigla da função → quantas pessoas a ocupam.</summary>
    public IReadOnlyDictionary<string, int> PorFuncao { get; init; } =
        new Dictionary<string, int>();
}

/// <summary>
/// O quadro de pessoal de um órgão numa competência — <b>em contagens, nunca em pessoas</b>.
///
/// <para>A fonte publica 778.337 vínculos nominais por mês, com nome e CPF mascarado. A
/// pergunta que o produto faz — "quantas pessoas, em que funções, em cada órgão" — se
/// responde com somas. Guardar a lista nominal traria dado pessoal em escala, 444 MB por
/// mês e uma avaliação de legítimo interesse, para entregar exatamente a mesma tela.</para>
///
/// <para>Por isso a agregação acontece <b>na ingestão</b>, antes de qualquer gravação, e
/// esta entidade é estruturalmente incapaz de representar uma pessoa. Há um teste que
/// quebra o build se alguém acrescentar um campo que sugira o contrário.</para>
///
/// <para><b>Uma lotação sem par no grafo é registro válido.</b> Medido: cerca de 4% dos
/// vínculos estão em rubricas que o SIORG não tem — os governos dos ex-territórios, por
/// exemplo. Recusá-las apagaria o número; guardá-las com <see cref="CodigoSiorg"/> nulo
/// deixa a tela dizer "sem correspondência no grafo", que é a verdade.</para>
/// </summary>
public sealed class LotacaoDoOrgao : Entidade
{
    private readonly List<ContagemPorFuncao> _funcoes = [];

    private LotacaoDoOrgao()
    {
        // EF Core.
        NomeNaOrigem = string.Empty;
    }

    public Competencia Competencia { get; private set; }

    public int CodigoSiafi { get; private set; }

    /// <summary>O nome do órgão na origem. É o que permite auditar a ponte.</summary>
    public string NomeNaOrigem { get; private set; }

    public int? CodigoSiorg { get; private set; }

    public int Vinculos { get; private set; }

    /// <summary>
    /// Quantos ocupam alguma função de confiança.
    ///
    /// <para>Derivado da soma das funções, e não recebido pronto: uma conta só, feita num
    /// lugar só, é o que impede um total que não bate com as partes.</para>
    /// </summary>
    public int ComFuncaoDeConfianca { get; private set; }

    public IReadOnlyList<ContagemPorFuncao> Funcoes => _funcoes;

    public static LotacaoDoOrgao Criar(NovaLotacao nova)
    {
        ArgumentNullException.ThrowIfNull(nova);

        if (nova.CodigoSiafi <= 0)
        {
            throw new ArgumentException(
                $"Código de órgão precisa ser positivo; recebido {nova.CodigoSiafi}.",
                nameof(nova));
        }

        if (nova.Vinculos < 0)
        {
            throw new ArgumentException(
                $"Um órgão não pode ter {nova.Vinculos} vínculos.",
                nameof(nova));
        }

        var lotacao = new LotacaoDoOrgao
        {
            Competencia = nova.Competencia,
            CodigoSiafi = nova.CodigoSiafi,
            NomeNaOrigem = string.IsNullOrWhiteSpace(nova.NomeNaOrigem)
                ? $"Órgão {nova.CodigoSiafi}"
                : nova.NomeNaOrigem.Trim(),
            CodigoSiorg = nova.CodigoSiorg,
            Vinculos = nova.Vinculos,
        };

        foreach (var (sigla, quantidade) in nova.PorFuncao)
        {
            if (quantidade < 0)
            {
                throw new ArgumentException(
                    $"A função {sigla} do órgão {nova.CodigoSiafi} veio com {quantidade}.",
                    nameof(nova));
            }

            // A fonte usa "-1" e vazio para "sem funcao". Sao ruido de codificacao, nao uma
            // funcao chamada "-1", e deixa-los entrar poria uma barra "-1" no grafico.
            if (string.IsNullOrWhiteSpace(sigla) || sigla.Trim() == "-1")
            {
                continue;
            }

            lotacao._funcoes.Add(new ContagemPorFuncao(sigla.Trim(), quantidade));
        }

        lotacao.ComFuncaoDeConfianca = lotacao._funcoes.Sum(funcao => funcao.Quantidade);

        if (lotacao.ComFuncaoDeConfianca > lotacao.Vinculos)
        {
            // Nao e' zelo excessivo: e' a unica checagem que pega um erro de agregacao —
            // somar a mesma pessoa duas vezes, ou contar funcao de quem esta' lotado
            // noutro lugar. Sem ela a tela mostraria "120% em cargo de confianca".
            throw new ArgumentException(
                $"O órgão {nova.CodigoSiafi} tem {lotacao.ComFuncaoDeConfianca} pessoas em "
                + $"função e só {lotacao.Vinculos} vínculos. A agregação contou alguém duas vezes.",
                nameof(nova));
        }

        return lotacao;
    }
}
