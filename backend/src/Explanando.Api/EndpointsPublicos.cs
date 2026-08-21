using Explanando.Application.Consultas;

namespace Explanando.Api;

/// <summary>
/// Os endpoints de leitura da API pública.
///
/// <para>Tudo aqui é <c>GET</c>. As escritas do domínio — favoritar, contestar, solicitar
/// varredura — existem como entidades mas não têm superfície HTTP nesta etapa: são outro
/// ciclo de vida, exigem autenticação e pagamento, e misturá-las aqui faria a API pública
/// carregar preocupações que ela não tem.</para>
/// </summary>
public static class EndpointsPublicos
{
    private const string Prefixo = "/api/v1";

    public static IEndpointRouteBuilder MapEndpointsPublicos(this IEndpointRouteBuilder rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        var grupo = rotas.MapGroup(Prefixo).WithTags("Público").ComCacheDeLeitura();

        grupo.MapGet("/parlamentares", RanquearAsync)
            .WithName("Ranquear")
            .WithSummary("Ranking de parlamentares.")
            .WithDescription(
                "Três ordenações, porque são perguntas diferentes: 'volume' é quanto se "
                + "gastou, 'indice' é a densidade de indicadores por 100 despesas, e "
                + "'gravidade' ordena só pelos achados de gravidade alta — a fila de "
                + "triagem. As duas últimas excluem quem está abaixo do piso amostral; "
                + "'volume' inclui todos, porque gasto não depende de amostra.");

        grupo.MapGet("/parlamentares/{id:guid}", ObterPerfilAsync)
            .WithName("ObterPerfil")
            .WithSummary("Perfil do parlamentar, com a cobertura das regras.")
            .WithDescription(
                "A cobertura vem ao lado dos números, não em rodapé. Cada regra declara se "
                + "avaliou e o que faltou quando não avaliou — sem isso o leitor supõe que "
                + "silêncio significa aprovação.");

        grupo.MapGet("/parlamentares/{id:guid}/despesas", ListarDespesasAsync)
            .WithName("ListarDespesas")
            .WithSummary("Despesas do parlamentar, com os achados de cada uma.");

        grupo.MapGet("/despesas/{id:guid}", ObterDespesaAsync)
            .WithName("ObterDespesa")
            .WithSummary("Uma despesa e seus achados.");

        return rotas;
    }

    private static async Task<IResult> RanquearAsync(
        IConsultasPublicas consultas,
        string? ordenar,
        int? pagina,
        int? itens,
        CancellationToken cancelamento)
    {
        if (!TentarLerOrdenacao(ordenar, out var ordenacao))
        {
            return Results.Problem(
                title: "Ordenação desconhecida",
                detail: $"'{ordenar}' não é uma ordenação válida. Use 'volume', 'indice' ou "
                    + "'gravidade'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultado = await consultas
            .RanquearAsync(ordenacao, pagina ?? 1, itens ?? 50, cancelamento)
            .ConfigureAwait(false);

        return Results.Ok(resultado);
    }

    private static async Task<IResult> ObterPerfilAsync(
        IConsultasPublicas consultas,
        Guid id,
        CancellationToken cancelamento)
    {
        var perfil = await consultas.ObterPerfilAsync(id, cancelamento).ConfigureAwait(false);

        return perfil is null ? NaoEncontrado("parlamentar", id) : Results.Ok(perfil);
    }

    private static async Task<IResult> ListarDespesasAsync(
        IConsultasPublicas consultas,
        Guid id,
        int? ano,
        int? mes,
        string? tipo,
        bool? comFlag,
        int? pagina,
        int? itens,
        CancellationToken cancelamento)
    {
        if (mes is { } m && m is < 1 or > 12)
        {
            return Results.Problem(
                title: "Mês inválido",
                detail: $"O mês precisa estar entre 1 e 12; recebido {m}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Mes sem ano nao identifica competencia nenhuma: "julho" de que ano? Aceitar em
        // silencio devolveria a lista inteira e o leitor acharia que filtrou.
        if (mes is not null && ano is null)
        {
            return Results.Problem(
                title: "Mês sem ano",
                detail: "Filtrar por mês exige informar o ano.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var filtro = new FiltroDeDespesas
        {
            Ano = ano,
            Mes = mes,
            TipoDespesa = tipo,
            ComFlag = comFlag,
            Pagina = pagina ?? 1,
            ItensPorPagina = itens ?? 50,
        };

        var resultado = await consultas
            .ListarDespesasAsync(id, filtro, cancelamento)
            .ConfigureAwait(false);

        return Results.Ok(resultado);
    }

    private static async Task<IResult> ObterDespesaAsync(
        IConsultasPublicas consultas,
        Guid id,
        CancellationToken cancelamento)
    {
        var despesa = await consultas.ObterDespesaAsync(id, cancelamento).ConfigureAwait(false);

        return despesa is null ? NaoEncontrado("despesa", id) : Results.Ok(despesa);
    }

    private static bool TentarLerOrdenacao(string? bruto, out OrdenacaoDoRanking ordenacao)
    {
        ordenacao = OrdenacaoDoRanking.Volume;

        if (string.IsNullOrWhiteSpace(bruto))
        {
            return true;
        }

        switch (bruto.Trim().ToLowerInvariant())
        {
            case "volume":
                ordenacao = OrdenacaoDoRanking.Volume;
                return true;
            case "indice":
            case "índice":
                ordenacao = OrdenacaoDoRanking.Indice;
                return true;
            case "gravidade":
                ordenacao = OrdenacaoDoRanking.Gravidade;
                return true;
            default:
                return false;
        }
    }

    private static IResult NaoEncontrado(string recurso, Guid id) =>
        Results.Problem(
            title: "Não encontrado",
            detail: $"Nenhum {recurso} com o identificador {id}.",
            statusCode: StatusCodes.Status404NotFound);
}
