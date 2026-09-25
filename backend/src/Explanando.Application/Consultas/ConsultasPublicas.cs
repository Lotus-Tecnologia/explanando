using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Consultas;

/// <summary>
/// As consultas que a API pública faz. É o lado de <b>leitura</b> do CQRS.
///
/// <para>Devolve modelos de leitura, nunca entidades de domínio. Duas razões, e a segunda é
/// a que importa:</para>
///
/// <list type="number">
/// <item>Entidade de escrita carrega invariante e comportamento que a tela não usa, e
/// serializá-la acopla o contrato público ao modelo interno.</item>
/// <item><b>Dado pessoal não sai por acidente.</b> Projetar campo a campo obriga a decidir
/// explicitamente o que é publicado. Uma entidade serializada inteira vaza o que for
/// acrescentado a ela depois, sem ninguém revisar.</item>
/// </list>
/// </summary>
public interface IConsultasPublicas
{
    /// <summary>
    /// O ranking, na ordenação pedida.
    ///
    /// <para>Quem está abaixo do piso amostral <b>sai do ranking de risco</b> — em qualquer
    /// ordenação por densidade — mas continua no de volume, que não depende de amostra.
    /// Ordenar por um índice que não se sustenta seria publicar ruído com nome e foto.</para>
    /// </summary>
    /// <para><b>A casa é obrigatória, e não um filtro opcional.</b> Deputado e senador
    /// disputam cotas distintas — a CEAP e a CEAPS têm regras e valores próprios —, e um
    /// ranking que os misture põe no mesmo eixo coisas que não se comparam. Sem parâmetro
    /// padrão aqui, esquecer de separar deixa de ser possível: vira erro de compilação, e
    /// não uma lista silenciosamente errada.</para>
    Task<Pagina<ItemDoRanking>> RanquearAsync(
        OrdenacaoDoRanking ordenacao,
        TipoParlamentar casa,
        int pagina,
        int itensPorPagina,
        CancellationToken cancelamento = default);

    /// <summary>O perfil, ou nulo se o parlamentar não existe.</summary>
    Task<PerfilDoParlamentar?> ObterPerfilAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    /// <summary>As despesas do parlamentar, filtradas e paginadas.</summary>
    Task<Pagina<ItemDeDespesa>> ListarDespesasAsync(
        Guid parlamentarId,
        FiltroDeDespesas filtro,
        CancellationToken cancelamento = default);

    /// <summary>
    /// As despesas com achado que entraram na base por último — o que o feed de alertas
    /// publica. Com <paramref name="parlamentarId"/>, só as dele; sem, as de todos.
    ///
    /// <para>Nulo quando o parlamentar pedido não existe: lista vazia diria que ele existe e
    /// não teve achado novo.</para>
    /// </summary>
    Task<IReadOnlyList<AchadoRecente>?> ListarAchadosRecentesAsync(
        Guid? parlamentarId,
        int itens,
        CancellationToken cancelamento = default);

    /// <summary>Uma despesa, com seus achados. Nulo se não existe.</summary>
    Task<DespesaDetalhada?> ObterDespesaAsync(
        Guid despesaId,
        CancellationToken cancelamento = default);
}
