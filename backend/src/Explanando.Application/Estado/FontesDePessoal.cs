using Explanando.Domain.Comum;

namespace Explanando.Application.Estado;

/// <summary>
/// Um vínculo, como a fonte o descreve — <b>reduzido a três campos de propósito</b>.
///
/// <para>O arquivo de origem traz 43 colunas por linha, entre elas <c>NOME</c> e <c>CPF</c>
/// mascarado. Este tipo é a fronteira, e ele existe para que o dado pessoal <b>pare aqui</b>:
/// o que atravessa para a aplicação são o órgão e a sigla da função, que é tudo de que a
/// contagem precisa.</para>
///
/// <para>Não é zelo simbólico. Um leitor que devolvesse a linha inteira deixaria a decisão
/// de descartar o nome espalhada por quem consome — e bastaria um <c>.ToList()</c> em algum
/// lugar para 778 mil nomes irem parar na memória de um processo que não deveria conhecê-los.</para>
/// </summary>
public sealed record VinculoDaFonte
{
    /// <summary>O código SIAFI do órgão de exercício.</summary>
    public required int CodigoSiafi { get; init; }

    /// <summary>O nome do <b>órgão</b>. É o que a ponte usa.</summary>
    public required string NomeDoOrgao { get; init; }

    /// <summary>
    /// A sigla da função de confiança, ou nulo.
    ///
    /// <para>A fonte escreve <c>-1</c> para "nenhuma", e o domínio descarta esse valor —
    /// mas o leitor não precisa saber disso: passar adiante o que veio mantém a tradução
    /// num lugar só.</para>
    /// </summary>
    public string? SiglaDaFuncao { get; init; }
}

/// <summary>Fonte do cadastro de pessoal do Executivo federal.</summary>
public interface IFonteDePessoal
{
    /// <summary>
    /// Percorre os vínculos de uma competência, um a um.
    ///
    /// <para><b>Fluxo, e não lista</b>: são 778 mil registros e 444 MB de texto. Materializar
    /// isso numa coleção custaria memória que o plano de hospedagem não tem, e não traria
    /// nada — a agregação consome cada linha uma vez e a esquece.</para>
    /// </summary>
    IAsyncEnumerable<VinculoDaFonte> LerVinculosAsync(
        Competencia competencia,
        CancellationToken cancelamento = default);
}

/// <summary>Uma unidade do grafo, no mínimo que a ponte precisa.</summary>
public sealed record UnidadeParaPonte
{
    public required int Codigo { get; init; }
    public required string Nome { get; init; }

    /// <summary>
    /// Ente, órgão ou entidade.
    ///
    /// <para>A ponte tenta primeiro só entre os de topo e só depois entre todas as
    /// unidades. A ordem importa: "Secretaria-Executiva" existe dezenas de vezes na base, e
    /// procurar entre todas de saída faria um ministério casar com a secretaria de outro.</para>
    /// </summary>
    public required bool EhDeTopo { get; init; }
}

/// <summary>Leitura e escrita das contagens de pessoal.</summary>
public interface IRepositorioDePessoal
{
    /// <summary>Os nomes do grafo, para montar a ponte.</summary>
    Task<IReadOnlyList<UnidadeParaPonte>> CarregarUnidadesParaPonteAsync(
        CancellationToken cancelamento = default);

    /// <summary>
    /// Apaga as contagens de uma competência antes de regravá-las.
    ///
    /// <para>Recoletar o mesmo mês é rotina — a fonte republica o arquivo com correções.
    /// Sem isto, a segunda coleta dobraria o quadro de pessoal de todo órgão.</para>
    /// </summary>
    Task LimparCompetenciaAsync(Competencia competencia, CancellationToken cancelamento = default);

    void Adicionar(Domain.Estado.LotacaoDoOrgao lotacao);

    Task SalvarAsync(CancellationToken cancelamento = default);
}
