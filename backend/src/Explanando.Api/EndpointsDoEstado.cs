using Explanando.Application.Consultas;

namespace Explanando.Api;

/// <summary>
/// O grafo do Estado — a adaptação ao Brasil do <see href="https://graph.civlab.org/us"/>.
///
/// <para>Fica num arquivo próprio, e não junto dos endpoints de despesa, porque é outro
/// contexto: aqui não há parlamentar, não há cota e não há indicador. A pergunta é "que
/// órgãos existem, de quem cada um depende, sob que norma — e o que mudou".</para>
/// </summary>
public static class EndpointsDoEstado
{
    private const string Prefixo = "/api/v1/estado";

    public static IEndpointRouteBuilder MapEndpointsDoEstado(this IEndpointRouteBuilder rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        var grupo = rotas.MapGroup(Prefixo).WithTags("Estado").ComCacheDeLeitura();

        grupo.MapGet("/", ObterMapaAsync)
            .WithName("ObterMapaDoEstado")
            .WithSummary("O mapa da estrutura do Estado, no nível de órgãos e entidades.")
            .WithDescription(
                "Devolve os entes, órgãos e entidades — algo em torno de 340 nós — com a "
                + "contagem de unidades que pende de cada um. As unidades administrativas e "
                + "os colegiados NÃO vêm aqui: são mais de cem mil, e desenhá-las juntas "
                + "produz uma nuvem onde o Executivo inteiro some atrás dos conselhos. Use "
                + "`/estado/{codigo}/unidades` para descer um nível.");

        grupo.MapGet("/mudancas", ListarMudancasAsync)
            .WithName("ListarMudancasDoEstado")
            .WithSummary("O que mudou na estrutura, da mudança mais nova para a mais velha.")
            .WithDescription(
                "A fonte não publica histórico — os campos existem no esquema e chegam "
                + "vazios, e os parâmetros de versão são aceitos e ignorados. Cada linha "
                + "aqui foi DEDUZIDA comparando duas coletas. Consequência: a primeira "
                + "coleta é linha de base e não produz nenhuma linha, e lista vazia pode "
                + "significar 'ainda não comparamos' — `temHistorico`, no mapa, diz qual "
                + "dos dois casos é.");

        grupo.MapGet("/pessoal", ObterSerieAsync)
            .WithName("ObterSerieDePessoalDoEstado")
            .WithSummary("A série mensal do quadro de pessoal do Estado inteiro.")
            .WithDescription(
                "Da competência mais antiga para a mais nova. Soma TODOS os vínculos, "
                + "inclusive os de órgãos que a ponte entre os dois cadastros não alcança: "
                + "a pergunta 'o Executivo cresceu?' é sobre o Executivo, não sobre a parte "
                + "dele que coube no nosso grafo. O cadastro é o do Executivo civil — o "
                + "Judiciário e o Legislativo mantêm folhas próprias.");

        grupo.MapGet("/{codigo:int}/pessoal", ObterSerieDaUnidadeAsync)
            .WithName("ObterSerieDePessoalDaUnidade")
            .WithSummary("A série mensal do ramo de uma unidade.");

        grupo.MapGet("/{codigo:int}", ObterUnidadeAsync)
            .WithName("ObterUnidadeDoEstado")
            .WithSummary("Uma unidade da estrutura.");

        grupo.MapGet("/{codigo:int}/unidades", ObterSubarvoreAsync)
            .WithName("ObterSubarvoreDoEstado")
            .WithSummary("Os filhos diretos de uma unidade.")
            .WithDescription(
                "Um nível por vez, de propósito: alguns órgãos têm milhares de unidades "
                + "abaixo, e devolver a árvore inteira traria de volta o problema que o "
                + "mapa inicial resolve.");

        return rotas;
    }

    private static async Task<IResult> ObterMapaAsync(
        IConsultasDoEstado consultas,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterMapaAsync(cancelamento).ConfigureAwait(false));

    private static async Task<IResult> ObterSerieAsync(
        IConsultasDoEstado consultas,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterSerieDePessoalAsync(null, cancelamento).ConfigureAwait(false));

    private static async Task<IResult> ObterSerieDaUnidadeAsync(
        IConsultasDoEstado consultas,
        int codigo,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterSerieDePessoalAsync(codigo, cancelamento).ConfigureAwait(false));

    private static async Task<IResult> ObterUnidadeAsync(
        IConsultasDoEstado consultas,
        int codigo,
        CancellationToken cancelamento)
    {
        var unidade = await consultas.ObterUnidadeAsync(codigo, cancelamento).ConfigureAwait(false);

        return unidade is null ? Results.NotFound() : Results.Ok(unidade);
    }

    private static async Task<IResult> ObterSubarvoreAsync(
        IConsultasDoEstado consultas,
        int codigo,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterSubarvoreAsync(codigo, cancelamento).ConfigureAwait(false));

    private static async Task<IResult> ListarMudancasAsync(
        IConsultasDoEstado consultas,
        int? quantidade,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas
            .ListarMudancasAsync(quantidade ?? 50, cancelamento)
            .ConfigureAwait(false));
}
