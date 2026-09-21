using Explanando.Application.Consultas;

namespace Explanando.Api;

/// <summary>
/// O mapa de poder — quem ocupa as cadeiras de comando do Estado.
///
/// <para>É a vista de pessoas que se alterna com o grafo de instituições, e por isso
/// devolve os <b>quatro poderes de uma vez</b>: são ~100 cadeiras, cabem numa resposta, e
/// é isso que permite ao desenho trocar de poder no clique sem voltar ao servidor.</para>
/// </summary>
public static class EndpointsDasCadeiras
{
    private const string Prefixo = "/api/v1/cadeiras";

    public static IEndpointRouteBuilder MapEndpointsDasCadeiras(this IEndpointRouteBuilder rotas)
    {
        ArgumentNullException.ThrowIfNull(rotas);

        var grupo = rotas.MapGroup(Prefixo).WithTags("Cadeiras").ComCacheDeLeitura();

        grupo.MapGet("/", ObterMapaAsync)
            .WithName("ObterMapaDePoder")
            .WithSummary("Quem ocupa as cadeiras de comando, por poder e por camada.")
            .WithDescription(
                "Os quatro poderes vêm sempre, inclusive os vazios: omitir um poder sem "
                + "ocupantes faria parecer que ele não tem comando, quando o que existe é "
                + "uma fonte que não publica. Nesses casos `porQueFalta` explica. "
                + "CADA CADEIRA CARREGA `fonte` E `verificadoEm`, e o `verificadoDesde` de "
                + "um poder é a verificação mais ANTIGA do conjunto — o pior caso, que é o "
                + "que o leitor precisa saber. `curada: true` marca a linha transcrita à "
                + "mão de um portal que não é legível por máquina.");

        return rotas;
    }

    private static async Task<IResult> ObterMapaAsync(
        IConsultasDeCadeiras consultas,
        CancellationToken cancelamento) =>
        Results.Ok(await consultas.ObterMapaAsync(cancelamento).ConfigureAwait(false));
}
