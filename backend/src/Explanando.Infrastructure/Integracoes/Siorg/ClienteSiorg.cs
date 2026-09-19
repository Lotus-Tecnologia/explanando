using System.Globalization;
using Explanando.Application.Estado;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Siorg;

/// <summary>
/// Cliente do SIORG — a estrutura organizacional do Estado.
///
/// <para><b>Uma requisição por vez, e isso não é conservadorismo.</b> O serviço recusa
/// chamadas concorrentes do mesmo IP com <c>codigoErro 119</c> — "Este IP tem uma requisição
/// em andamento" — e responde <c>HTTP 200</c> ao fazê-lo. Medido do pior jeito: com seis
/// threads, 329 das 335 subárvores voltaram recusadas e o processo achou que tinha
/// terminado, com 2.334 unidades em nove segundos. A trava abaixo é o que impede esse
/// resultado de parecer uma coleta.</para>
///
/// <para>Consequência de projeto: a coleta é <b>sequencial por natureza da fonte</b>. Não
/// adianta paralelizar em camada nenhuma acima desta.</para>
/// </summary>
public sealed class ClienteSiorg(HttpClient http)
    : IFonteDaEstruturaDoEstado, IDisposable
{
    /// <summary>
    /// Quantas vezes insistir quando a recusa é temporária.
    ///
    /// <para>A trava já serializa as chamadas <b>deste</b> processo, mas o limite é por IP:
    /// outro processo na mesma máquina, ou o próprio navegador de alguém, disputa a mesma
    /// fila. A repetição cobre essa disputa.</para>
    /// </summary>
    private const int TentativasMaximas = 5;

    /// <summary>
    /// Pausa entre chamadas bem-sucedidas.
    ///
    /// <para>São 335 requisições contra um serviço público gratuito que já declarou não
    /// querer concorrência. O custo total da cortesia é ~84 segundos numa coleta que roda
    /// uma vez por mês.</para>
    ///
    /// <para>É <c>init</c> em vez de constante para que a suíte possa zerá-la: um teste que
    /// espera de verdade não prova nada além de paciência.</para>
    /// </summary>
    public TimeSpan IntervaloEntreChamadas { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Espera da primeira repetição; as seguintes crescem em múltiplos dela.</summary>
    public TimeSpan EsperaBase { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Quantas vezes foi preciso repetir por recusa temporária.
    ///
    /// <para>Fica exposto em vez de virar linha de log porque é <b>medida</b>, não ruído: se
    /// este número subir de dezenas para centenas, a fila do SIORG mudou e a cortesia entre
    /// chamadas precisa mudar junto. O comando do worker imprime isto no resumo.</para>
    /// </summary>
    public int RepeticoesPorRecusa { get; private set; }

    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    public async Task<IReadOnlyList<NovaUnidadeDoEstado>> ListarOrgaosAsync(
        CancellationToken cancelamento = default)
    {
        var resposta = await PedirAsync("orgao-entidade", ConversorDoSiorg.ConverterLista, cancelamento)
            .ConfigureAwait(false);

        if (resposta.Falha is not null)
        {
            // A lista de orgaos e' a raiz da varredura: sem ela nao ha' o que percorrer, e
            // seguir devolvendo vazio faria a coleta parecer completa com zero unidades.
            throw new InvalidOperationException(
                $"O SIORG recusou a lista de orgaos e entidades ({resposta.Falha}). "
                + "Sem ela nao ha' por onde comecar a varredura.");
        }

        return resposta.Unidades;
    }

    public async Task<SubarvoreDoEstado> ObterSubarvoreAsync(
        int codigoDoOrgao,
        CancellationToken cancelamento = default)
    {
        var caminho = string.Create(
            CultureInfo.InvariantCulture,
            $"estrutura-organizacional?codigoUnidade={codigoDoOrgao}");

        var resposta = await PedirAsync(caminho, ConversorDoSiorg.ConverterLista, cancelamento)
            .ConfigureAwait(false);

        return new SubarvoreDoEstado
        {
            CodigoDoOrgao = codigoDoOrgao,
            Unidades = resposta.Unidades,
            Falha = resposta.Falha,
        };
    }

    public async Task<NovaUnidadeDoEstado?> ObterUnidadeAsync(
        int codigo,
        CancellationToken cancelamento = default)
    {
        var caminho = string.Create(
            CultureInfo.InvariantCulture,
            $"unidade-organizacional/{codigo}");

        var resposta = await PedirAsync(caminho, ConversorDoSiorg.ConverterUnidade, cancelamento)
            .ConfigureAwait(false);

        return resposta.Unidades.Count > 0 ? resposta.Unidades[0] : null;
    }

    private async Task<RespostaDoSiorg> PedirAsync(
        string caminho,
        Func<string, RespostaDoSiorg> converter,
        CancellationToken cancelamento)
    {
        await _umaPorVez.WaitAsync(cancelamento).ConfigureAwait(false);

        try
        {
            RespostaDoSiorg resposta = new() { Falha = "nenhuma tentativa feita", VaiPassar = true };

            for (var tentativa = 1; tentativa <= TentativasMaximas; tentativa++)
            {
                resposta = await UmaTentativaAsync(caminho, converter, cancelamento)
                    .ConfigureAwait(false);

                if (resposta.Falha is null || !resposta.VaiPassar)
                {
                    break;
                }

                RepeticoesPorRecusa++;

                if (tentativa < TentativasMaximas)
                {
                    await Task.Delay(EsperaBase * tentativa, cancelamento).ConfigureAwait(false);
                }
            }

            await Task.Delay(IntervaloEntreChamadas, cancelamento).ConfigureAwait(false);

            return resposta;
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    private async Task<RespostaDoSiorg> UmaTentativaAsync(
        string caminho,
        Func<string, RespostaDoSiorg> converter,
        CancellationToken cancelamento)
    {
        try
        {
            using var resposta = await http
                .GetAsync(new Uri(caminho, UriKind.Relative), cancelamento)
                .ConfigureAwait(false);

            if (!resposta.IsSuccessStatusCode)
            {
                return new RespostaDoSiorg
                {
                    Falha = $"HTTP {(int)resposta.StatusCode}",
                    VaiPassar = (int)resposta.StatusCode >= 500,
                };
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancelamento).ConfigureAwait(false);

            return converter(corpo);
        }
        catch (HttpRequestException erro)
        {
            return new RespostaDoSiorg { Falha = erro.Message, VaiPassar = true };
        }
        catch (TaskCanceledException) when (!cancelamento.IsCancellationRequested)
        {
            return new RespostaDoSiorg { Falha = "tempo esgotado", VaiPassar = true };
        }
    }

    /// <summary>A trava e' descartavel; o <c>HttpClient</c> pertence a fabrica e fica.</summary>
    public void Dispose() => _umaPorVez.Dispose();
}
