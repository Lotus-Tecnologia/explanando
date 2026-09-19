using Explanando.Application.Consultas;

namespace Explanando.Api;

/// <summary>
/// O mapa de fornecedores compartilhados — a vista de <b>pessoas</b> do grafo do Estado.
///
/// <para>É a nossa resposta ao <i>Power map</i> da referência
/// (<see href="https://graph.civlab.org/us"/>), com a aresta trocada. Lá dois nomes se
/// ligam por aparecerem nos mesmos artigos de jornal, o que mede a imprensa. Aqui se
/// ligam por dividirem um fornecedor pouco compartilhado e de valor alto, o que é um fato
/// da base — com CNPJ, nota fiscal e valor.</para>
///
/// <para>Fica separado dos endpoints de despesa pela mesma razão que o
/// <see cref="EndpointsDoEstado"/>: a pergunta não é "quanto este parlamentar gastou", e
/// nenhuma resposta daqui entra no índice de risco de ninguém.</para>
/// </summary>
public static class EndpointsDaRede
{
    private const string Prefixo = "/api/v1/rede";

    public static IEndpointRouteBuilder MapEndpointsDaRede(this IEndpointRouteBuilder rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        var grupo = rotas.MapGroup(Prefixo).WithTags("Rede").ComCacheDeLeitura();

        grupo.MapGet("/", ObterMapaAsync)
            .WithName("ObterMapaDaRede")
            .WithSummary("Os parlamentares e os fornecedores que eles dividem entre si.")
            .WithDescription(
                "Entram APENAS fornecedores que atendem de 2 a 5 parlamentares e somam ao "
                + "menos R$ 50 mil. O corte não é arbitrário e não é estético: dos 41.105 "
                + "fornecedores da base, os mais compartilhados são postos de combustível "
                + "de Brasília (417 parlamentares), companhias aéreas e operadoras de "
                + "telefonia. Desenhar essas arestas ligaria centenas de gabinetes por um "
                + "fato que é geografia, não arranjo. A resposta traz `ressalva` e "
                + "`porQueEsteCorte`, que devem acompanhar qualquer exibição destes dados: "
                + "fornecedor em comum NÃO é indício de irregularidade.");

        grupo.MapGet("/{parlamentarId:guid}", ObterLigacoesAsync)
            .WithName("ObterLigacoesDaRede")
            .WithSummary("Os fornecedores que um parlamentar divide com alguém.")
            .WithDescription(
                "Lista vazia quando ele não divide nenhum fornecedor que passe do corte — "
                + "o que é o caso da maioria, e não diz nada sobre ele.");

        return rotas;
    }

    private static async Task<IResult> ObterMapaAsync(
        IConsultasDaRede consultas,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterMapaAsync(cancelamento).ConfigureAwait(false));

    private static async Task<IResult> ObterLigacoesAsync(
        IConsultasDaRede consultas,
        Guid parlamentarId,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas
            .ObterLigacoesAsync(parlamentarId, cancelamento)
            .ConfigureAwait(false));
}
