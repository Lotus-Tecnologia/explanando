namespace Explanando.Api;

/// <summary>
/// Os cabeçalhos que autorizam a CDN a guardar a resposta.
///
/// <para>O <c>ADR-0001</c> aposta o custo do projeto numa frase: <b>é a CDN, e não o Redis,
/// que absorve o pico viral</b>. Isso só é verdade se a resposta disser que pode ser
/// guardada — sem <c>Cache-Control</c>, a Cloudflare repassa cada acesso ao Cloud Run, e
/// cada um deles chega ao Postgres.</para>
///
/// <para>São três janelas, e a diferença entre elas é deliberada:</para>
///
/// <list type="bullet">
/// <item><c>max-age</c> curto no navegador, porque o leitor que recarrega quer ver o dado
/// atual, e é ele quem percebe a diferença.</item>
/// <item><c>s-maxage</c> igual à janela do <c>HybridCache</c> do ranking. Manter os dois no
/// mesmo número evita que a defasagem se some: dado com até cinco minutos, não dez.</item>
/// <item><c>stale-while-revalidate</c> largo, que é a peça que faz o pico ser absorvido de
/// verdade — a CDN serve a cópia velha na hora e atualiza atrás, em vez de segurar mil
/// leitores esperando uma consulta.</item>
/// </list>
/// </summary>
internal static class CacheDeLeitura
{
    /// <summary>No navegador: curto, porque quem recarrega quer o dado atual.</summary>
    private const int SegundosNoNavegador = 60;

    /// <summary>Na CDN: o mesmo do cache do ranking, para as defasagens não se somarem.</summary>
    private const int SegundosNaCdn = 300;

    /// <summary>Quanto tempo a CDN pode servir a cópia velha enquanto renova.</summary>
    private const int SegundosServindoVelho = 600;

    // Montada uma vez: interpolação com constante numérica não é expressão constante em C#,
    // e a alternativa seria repetir os números como texto — que é como eles saem de sincronia.
    private static readonly string Publica = string.Create(
        System.Globalization.CultureInfo.InvariantCulture,
        $"public, max-age={SegundosNoNavegador}, s-maxage={SegundosNaCdn}, "
        + $"stale-while-revalidate={SegundosServindoVelho}");

    /// <summary>
    /// Só o <c>200</c> é cacheável.
    ///
    /// <para>Um <c>404</c> guardado por cinco minutos sobrevive ao parlamentar passar a
    /// existir, e um <c>400</c> guardado ensina a CDN a repetir a recusa para quem já
    /// corrigiu o filtro. Nos dois casos o erro dura mais que a causa dele.</para>
    /// </summary>
    private const string Nenhum = "no-store";

    public static RouteGroupBuilder ComCacheDeLeitura(this RouteGroupBuilder grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        grupo.AddEndpointFilter(async (contexto, proximo) =>
        {
            // O cabeçalho é decidido em OnStarting, e não aqui, porque só ali o código de
            // status já está resolvido — inclusive o 400 que um filtro anterior devolveu.
            contexto.HttpContext.Response.OnStarting(static estado =>
            {
                var resposta = (HttpResponse)estado;

                resposta.Headers.CacheControl =
                    resposta.StatusCode == StatusCodes.Status200OK ? Publica : Nenhum;

                return Task.CompletedTask;
            },
            contexto.HttpContext.Response);

            return await proximo(contexto).ConfigureAwait(false);
        });

        return grupo;
    }
}
