using Explanando.Application.Consultas;
using Explanando.Domain.Parlamentares;

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
            .WithSummary("Ranking de parlamentares de UMA casa.")
            .WithDescription(
                "`casa` é obrigatório — 'deputados' ou 'senadores'. Os dois rankings são "
                + "separados porque as cotas são: a CEAP e a CEAPS têm regras e valores "
                + "próprios por UF, e o mesmo eixo poria lado a lado números que não medem "
                + "a mesma coisa. Não há ranking padrão, e omitir a casa é 400. "
                + "Três ordenações, porque são perguntas diferentes: 'volume' é quanto se "
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

        grupo.MapGet("/parlamentares/{id:guid}/fornecedores", ObterPagamentosPorFornecedorAsync)
            .WithName("ObterPagamentosPorFornecedor")
            .WithSummary("Os maiores fornecedores do parlamentar, ano a ano.")
            .WithDescription(
                "Cada ano diz em quantos meses houve despesa; o que não tem os doze é parcial, "
                + "e a variação só é medida entre os dois últimos anos completos seguidos. "
                + "Fornecedor que não recebeu nos dois não tem variação — tem começo ou fim. "
                + "`quantos` vai de 1 a 30; o padrão é 10.");

        grupo.MapGet("/parlamentares/{id:guid}/socios-em-comum", ObterSociosEmComumAsync)
            .WithName("ObterSociosEmComum")
            .WithSummary("Fornecedores dele com sócio em comum com fornecedores de outros parlamentares.")
            .WithDescription(
                "Parlamentar → fornecedor → sócio em comum → outra empresa → outro parlamentar. "
                + "O sócio casa por nome e CPF mascarado, literalmente; nome que aparece com mais "
                + "de um CPF fica de fora. Sócio que alcança mais de 5 parlamentares é "
                + "infraestrutura e não liga ninguém. Cada ponta precisa de R$ 50 mil, e filial "
                + "não é outra empresa. O nome do sócio não é publicado — a ligação se confere "
                + "pelos dois CNPJs. Ter sócio em comum não é irregularidade.");

        grupo.MapGet("/achados-recentes", ListarAchadosRecentesAsync)
            .WithName("ListarAchadosRecentes")
            .WithSummary("As despesas com achado que entraram na base por último.")
            .WithDescription(
                "É o que o feed de alertas publica. `parlamentar` recorta para um só; sem ele, "
                + "vêm as de todos. A ordem é a de entrada na base, e `registradaEm` é a data do "
                + "alerta — não a da flag, que a varredura recria a cada passada. Até 100 itens.");

        grupo.MapGet("/despesas/{id:guid}", ObterDespesaAsync)
            .WithName("ObterDespesa")
            .WithSummary("Uma despesa e seus achados.");

        return rotas;
    }

    private static async Task<IResult> ObterPagamentosPorFornecedorAsync(
        IConsultasPublicas consultas,
        Guid id,
        int? quantos,
        CancellationToken cancelamento)
    {
        var escada = await consultas
            .ObterPagamentosPorFornecedorAsync(
                id, quantos ?? EscadaDePagamentos.FornecedoresPadrao, cancelamento)
            .ConfigureAwait(false);

        return escada is null ? Results.NotFound() : Results.Ok(escada);
    }

    private static async Task<IResult> ObterSociosEmComumAsync(
        IConsultasPublicas consultas,
        Guid id,
        CancellationToken cancelamento)
    {
        var resultado = await consultas.ObterSociosEmComumAsync(id, cancelamento).ConfigureAwait(false);

        return resultado is null ? Results.NotFound() : Results.Ok(resultado);
    }

    private static async Task<IResult> ListarAchadosRecentesAsync(
        IConsultasPublicas consultas,
        Guid? parlamentar,
        int? itens,
        CancellationToken cancelamento)
    {
        var achados = await consultas
            .ListarAchadosRecentesAsync(parlamentar, itens ?? 50, cancelamento)
            .ConfigureAwait(false);

        return achados is null ? Results.NotFound() : Results.Ok(achados);
    }

    private static async Task<IResult> RanquearAsync(
        IConsultasPublicas consultas,
        string? casa,
        string? ordenar,
        int? pagina,
        int? itens,
        CancellationToken cancelamento)
    {
        // `casa` NAO tem padrao, de proposito. Um padrao silencioso — qualquer um dos dois —
        // faria quem esquecesse o parametro receber uma casa so' achando que recebeu as
        // duas, ou as duas misturadas achando que recebeu uma. E' a mesma classe de erro do
        // idLegislatura da Camara, que responde 200 com lista vazia: o pedido incompleto
        // precisa falhar alto, nao devolver algo plausivel.
        if (!TentarLerCasa(casa, out var tipo))
        {
            return Results.Problem(
                title: "Casa não informada ou desconhecida",
                detail: $"Informe 'casa=deputados' ou 'casa=senadores'. Recebido: '{casa}'. "
                    + "Os rankings são separados porque as cotas são distintas: a CEAP e a "
                    + "CEAPS têm regras e valores próprios, e compará-las no mesmo eixo põe "
                    + "lado a lado números que não medem a mesma coisa.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TentarLerOrdenacao(ordenar, out var ordenacao))
        {
            return Results.Problem(
                title: "Ordenação desconhecida",
                detail: $"'{ordenar}' não é uma ordenação válida. Use 'volume', 'indice' ou "
                    + "'gravidade'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultado = await consultas
            .RanquearAsync(ordenacao, tipo, pagina ?? 1, itens ?? 50, cancelamento)
            .ConfigureAwait(false);

        return Results.Ok(resultado);
    }

    /// <summary>
    /// Lê a casa do pedido. Diferente de <see cref="TentarLerOrdenacao"/>, <b>vazio é
    /// inválido</b>: não há ranking padrão.
    /// </summary>
    private static bool TentarLerCasa(string? bruto, out TipoParlamentar casa)
    {
        casa = TipoParlamentar.DeputadoFederal;

        return bruto?.Trim().ToLowerInvariant() switch
        {
            "deputados" or "deputado" or "camara" or "câmara" => true,
            "senadores" or "senador" or "senado" => Marcar(TipoParlamentar.Senador, out casa),
            _ => false,
        };

        static bool Marcar(TipoParlamentar valor, out TipoParlamentar destino)
        {
            destino = valor;
            return true;
        }
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
