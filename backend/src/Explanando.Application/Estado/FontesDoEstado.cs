using Explanando.Domain.Estado;

namespace Explanando.Application.Estado;

/// <summary>
/// A árvore de uma unidade raiz, ou a falha que impediu de obtê-la.
///
/// <para><b>A falha é um valor, não uma exceção</b>, porque ela muda o significado do
/// resultado inteiro: uma subárvore que não veio faz a coleta ficar incompleta, e coleta
/// incompleta não pode fazer órgão desaparecer. Se isso subisse como exceção, o serviço
/// teria de reconstruir a informação a partir de um <c>catch</c>.</para>
/// </summary>
public sealed record SubarvoreDoEstado
{
    public required int CodigoDoOrgao { get; init; }

    public IReadOnlyList<NovaUnidadeDoEstado> Unidades { get; init; } = [];

    /// <summary>Nulo quando deu certo.</summary>
    public string? Falha { get; init; }

    public bool DeuCerto => Falha is null;
}

/// <summary>
/// Fonte da estrutura organizacional do Estado — hoje o SIORG.
///
/// <para>Os três métodos existem porque a fonte tem três formas de responder, e nenhuma
/// substitui as outras. Medido em <c>docs/contratos/siorg.md</c>:</para>
///
/// <list type="bullet">
/// <item>a lista de órgãos e entidades vem inteira numa chamada só;</item>
/// <item>a árvore abaixo de um órgão exige uma chamada por órgão — e só funciona para
/// órgão ou entidade: pedir a árvore de um <c>ente</c> responde <c>codigoErro 104</c>;</item>
/// <item>os ancestrais acima dos órgãos (a União, os quatro poderes, a República) só saem
/// unidade a unidade.</item>
/// </list>
/// </summary>
public interface IFonteDaEstruturaDoEstado
{
    /// <summary>Os órgãos e entidades que servem de raiz para a varredura.</summary>
    Task<IReadOnlyList<NovaUnidadeDoEstado>> ListarOrgaosAsync(
        CancellationToken cancelamento = default);

    /// <summary>Tudo que pende abaixo de um órgão, incluindo ele mesmo.</summary>
    Task<SubarvoreDoEstado> ObterSubarvoreAsync(
        int codigoDoOrgao,
        CancellationToken cancelamento = default);

    /// <summary>Uma unidade só. Nulo quando a fonte não a conhece.</summary>
    Task<NovaUnidadeDoEstado?> ObterUnidadeAsync(
        int codigo,
        CancellationToken cancelamento = default);
}

/// <summary>Leitura e escrita do grafo do Estado.</summary>
public interface IRepositorioDoEstado
{
    /// <summary>
    /// Todas as unidades já conhecidas, ativas ou não.
    ///
    /// <para>Carregar tudo de uma vez é deliberado: a comparação é entre dois conjuntos
    /// inteiros, e são ~20 mil linhas pequenas. Consultar uma a uma seriam 20 mil idas ao
    /// banco para responder a mesma pergunta.</para>
    /// </summary>
    Task<IReadOnlyList<UnidadeDoEstado>> CarregarUnidadesAsync(
        CancellationToken cancelamento = default);

    void Adicionar(UnidadeDoEstado unidade);

    void Registrar(MudancaEstrutural mudanca);

    Task SalvarAsync(CancellationToken cancelamento = default);
}
