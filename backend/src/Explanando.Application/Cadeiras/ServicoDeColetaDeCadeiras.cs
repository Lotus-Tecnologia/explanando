using Explanando.Domain.Cadeiras;

namespace Explanando.Application.Cadeiras;

/// <summary>O que a coleta de cadeiras produziu, fonte a fonte.</summary>
public sealed record ResumoDaColetaDeCadeiras
{
    public required IReadOnlyList<ResultadoPorFonte> PorFonte { get; init; }

    public int Gravadas => PorFonte.Sum(r => r.Gravadas);

    /// <summary>Quantas fontes falharam. Zero é o caminho feliz.</summary>
    public int Falhas => PorFonte.Count(r => r.Falha is not null);
}

public sealed record ResultadoPorFonte
{
    public required string Fonte { get; init; }
    public int Gravadas { get; init; }
    public string? Falha { get; init; }
}

/// <summary>
/// Roda todas as fontes de cadeiras e grava o que cada uma trouxe.
///
/// <para><b>Uma fonte que falha não derruba as outras, e é a razão de este serviço
/// existir.</b> O Executivo vem de duas páginas HTML do Planalto, o Legislativo de duas
/// APIs. São disponibilidades independentes, e tratá-las como uma coleta só faria o mapa
/// inteiro sumir porque o portal da Presidência estava em manutenção.</para>
///
/// <para>A gravação é <b>por fonte</b>, substituindo o que aquela fonte trouxe da última
/// vez. Isso dá a propriedade que importa: a coleta de hoje do Congresso convive com a
/// coleta de ontem do Planalto, e o mapa fica velho num setor em vez de ficar vazio.</para>
/// </summary>
public sealed class ServicoDeColetaDeCadeiras(
    IEnumerable<IFonteDeCadeiras> fontes,
    IRepositorioDeCadeiras repositorio,
    TimeProvider relogio)
{
    /// <summary>
    /// Coleta as fontes que estão vencidas, e só elas.
    ///
    /// <para><b>O vencimento é por coletor.</b> Uma fonte que gravou ontem não é
    /// incomodada; uma que nunca rodou, ou que falhou e ficou para trás, é tentada de
    /// novo a cada passada. É o que impede a tabela de envelhecer em silêncio sem
    /// transformar o serviço num martelo sobre portais que já responderam.</para>
    /// </summary>
    public async Task<ResumoDaColetaDeCadeiras> ColetarSeVencidoAsync(
        TimeSpan validade,
        CancellationToken cancelamento = default)
    {
        var ultimas = await repositorio.UltimaColetaAsync(cancelamento).ConfigureAwait(false);
        var limite = relogio.GetUtcNow() - validade;

        var vencidas = fontes
            .Where(fonte => !ultimas.TryGetValue(fonte.Nome, out var quando) || quando < limite)
            .ToList();

        return await ColetarAsync(vencidas, cancelamento).ConfigureAwait(false);
    }

    public Task<ResumoDaColetaDeCadeiras> ColetarAsync(CancellationToken cancelamento = default) =>
        ColetarAsync(fontes, cancelamento);

    private async Task<ResumoDaColetaDeCadeiras> ColetarAsync(
        IEnumerable<IFonteDeCadeiras> aColetar,
        CancellationToken cancelamento)
    {
        var agora = relogio.GetUtcNow();
        var resultados = new List<ResultadoPorFonte>();

        foreach (var fonte in aColetar)
        {
            var coleta = await fonte.LerAsync(cancelamento).ConfigureAwait(false);

            if (!coleta.DeuCerto)
            {
                resultados.Add(new ResultadoPorFonte { Fonte = fonte.Nome, Falha = coleta.Falha });
                continue;
            }

            // Coleta que deu certo e veio vazia TAMBEM nao apaga o que ja' existe. Uma
            // pagina que responde 200 com o corpo trocado e' indistinguivel de um governo
            // sem ministros, e entre as duas leituras a segunda e' a absurda.
            if (coleta.Cadeiras.Count == 0)
            {
                resultados.Add(new ResultadoPorFonte
                {
                    Fonte = fonte.Nome,
                    Falha =
                        "A fonte respondeu sem erro e sem nenhuma cadeira. O que já estava "
                        + "gravado foi mantido: um Estado sem nenhum ocupante é menos "
                        + "provável que uma página que mudou de forma.",
                });

                continue;
            }

            var cadeiras = coleta.Cadeiras
                .Select(nova => CadeiraDeComando.Criar(nova, fonte.Nome, agora))
                .ToList();

            await repositorio
                .SubstituirAsync(fonte.Nome, cadeiras, cancelamento)
                .ConfigureAwait(false);

            resultados.Add(new ResultadoPorFonte { Fonte = fonte.Nome, Gravadas = cadeiras.Count });
        }

        return new ResumoDaColetaDeCadeiras { PorFonte = resultados };
    }
}
