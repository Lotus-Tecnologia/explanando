using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Explanando.Application.Ingestao;

namespace Explanando.Infrastructure.Integracoes.Camara;

/// <summary>
/// Cliente da API de Dados Abertos da Camara dos Deputados.
///
/// Duas decisoes vieram de medicao, nao de suposicao (Etapa 01):
///
/// 1. <b>idLegislatura e' sempre enviado.</b> Sem ele a API responde 200 com lista vazia,
///    nao erro — e a coleta inteira grava zero despesas reportando sucesso.
/// 2. <b>Paginas de 100.</b> O servidor limita `itens` a 100; pedir mais nao adianta.
///
/// A resiliencia (retry, circuit breaker, timeout) fica no <c>HttpClient</c> injetado,
/// configurado em <c>ServicosDeIntegracao</c>. Este tipo nao conhece Polly.
///
/// A paginacao segue o link <c>rel="next"</c> do corpo, mas guarda os caminhos ja
/// visitados: uma resposta que aponte para uma pagina anterior prenderia a ingestao num
/// laco infinito, consumindo memoria ate derrubar o processo. Um servico externo nao
/// precisa ser malicioso para isso — basta estar com defeito.
/// </summary>
public sealed class ClienteCamara(HttpClient http, TimeSpan? esperaEntreTentativas = null)
    : IFonteDeParlamentares
{
    private const int ItensPorPagina = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        var caminho = $"deputados?itens={ItensPorPagina}&ordem=ASC&ordenarPor=nome";
        var visitados = new HashSet<string>(StringComparer.Ordinal);

        while (caminho is not null && visitados.Add(caminho))
        {
            var pagina = await ObterAsync<RespostaPaginada<DeputadoResumoJson>>(caminho, cancelamento)
                .ConfigureAwait(false);

            if (pagina is null || pagina.Dados.Count == 0)
            {
                yield break;
            }

            foreach (var deputado in pagina.Dados)
            {
                yield return new DeputadoDaFonte
                {
                    IdExterno = deputado.Id,
                    NomeCivil = deputado.Nome ?? string.Empty,
                    NomeParlamentar = deputado.Nome ?? string.Empty,
                    SiglaPartido = deputado.SiglaPartido ?? string.Empty,
                    Uf = deputado.SiglaUf ?? string.Empty,
                    FotoUrl = deputado.UrlFoto,
                    Legislatura = deputado.IdLegislatura,
                };
            }

            caminho = pagina.ProximaPagina;
        }
    }

    public async Task<DeputadoDaFonte?> ObterDetalheAsync(
        int idExterno,
        CancellationToken cancelamento = default)
    {
        var resposta = await ObterAsync<EnvelopeDe<DeputadoDetalheJson>>(
            $"deputados/{idExterno.ToString(CultureInfo.InvariantCulture)}",
            cancelamento).ConfigureAwait(false);

        if (resposta?.Dados is not { } detalhe)
        {
            return null;
        }

        var status = detalhe.UltimoStatus;

        return new DeputadoDaFonte
        {
            IdExterno = detalhe.Id,
            NomeCivil = detalhe.NomeCivil ?? string.Empty,
            NomeParlamentar = status?.NomeEleitoral ?? status?.Nome ?? detalhe.NomeCivil ?? string.Empty,
            SiglaPartido = status?.SiglaPartido ?? string.Empty,
            Uf = status?.SiglaUf ?? string.Empty,
            FotoUrl = status?.UrlFoto,
            Legislatura = status?.IdLegislatura,
            EmExercicio = status?.Situacao is null
                || status.Situacao.StartsWith("Exerc", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Quantas vezes reconsultar uma pagina que voltou vazia antes de aceitar o vazio.
    ///
    /// <para>Existe por medicao, nao por precaucao. Em 2026-08-21 a rota <c>/despesas</c>
    /// devolvia <c>200</c> com lista vazia de forma <b>intermitente</b>: doze consultas
    /// seguidas a' mesma URL deram cinco respostas com dado e sete vazias. Aceitar a
    /// primeira resposta vazia como verdade transformaria 58% dos deputados em "gasto
    /// zero".</para>
    ///
    /// <para>Com seis tentativas, a chance de todas cairem no lado ruim naquele cenario e'
    /// de 0,58^6 — cerca de 4%. E o que sobra desses 4% cai no alerta de sanidade e no
    /// checkpoint com alerta, que forcam nova passada.</para>
    /// </summary>
    private const int TentativasParaPaginaVazia = 6;

    /// <summary>
    /// Espera entre reconsultas da mesma pagina. Injetavel para que a suite nao gaste
    /// dezenas de segundos dormindo — o teste prova a logica, nao o relogio.
    /// </summary>
    private readonly TimeSpan _esperaEntreTentativas =
        esperaEntreTentativas ?? TimeSpan.FromSeconds(2);

    public async IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
        int idExterno,
        int legislatura,
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        var caminho =
            $"deputados/{idExterno.ToString(CultureInfo.InvariantCulture)}/despesas"
            + $"?idLegislatura={legislatura.ToString(CultureInfo.InvariantCulture)}"
            + $"&itens={ItensPorPagina}";

        var visitados = new HashSet<string>(StringComparer.Ordinal);
        int? totalDePaginas = null;
        var paginasLidas = 0;

        while (caminho is not null && visitados.Add(caminho))
        {
            var pagina = await ObterPaginaAsync(caminho, cancelamento).ConfigureAwait(false);

            if (pagina is null || pagina.Dados.Count == 0)
            {
                // Vazio depois das reconsultas. Na primeira pagina isso pode ser verdade —
                // ha' deputado sem despesa. Da segunda em diante nao pode: a origem acabou
                // de dizer que havia proxima pagina.
                break;
            }

            // So' a primeira pagina traz a contagem confiavel; as seguintes tambem trazem,
            // mas guardar a primeira basta e evita que uma resposta degradada no meio
            // rebaixe o total.
            totalDePaginas ??= pagina.TotalDePaginas;
            paginasLidas++;

            foreach (var despesa in pagina.Dados)
            {
                yield return Converter(despesa);
            }

            caminho = pagina.ProximaPagina;
        }

        // A conferencia que impede o truncamento silencioso. Estourar aqui e' de proposito:
        // a excecao sobe ate' ServicoDeColeta, que registra falha e NAO grava checkpoint,
        // entao o deputado inteiro e' retentado na proxima passada. Devolver dado pela
        // metade seria pior — a pagina publicaria um gasto menor que o real, sem que nada
        // indicasse que faltou pedaco.
        if (totalDePaginas is { } total && paginasLidas < total)
        {
            throw new InvalidOperationException(
                $"Paginacao incompleta para o deputado {idExterno}: a origem anunciou {total} "
                + $"paginas e entregou {paginasLidas}. Descartando o parcial para nao publicar "
                + "gasto truncado.");
        }
    }

    /// <summary>
    /// Busca uma pagina, reconsultando quando ela vem vazia.
    ///
    /// A retentativa fica aqui, e nao no pipeline do Polly, porque nao ha erro a capturar:
    /// a origem responde <c>200</c>. Resiliencia de transporte nao alcanca fonte que mente.
    /// </summary>
    private async Task<RespostaPaginada<DespesaJson>?> ObterPaginaAsync(
        string caminho,
        CancellationToken cancelamento)
    {
        RespostaPaginada<DespesaJson>? pagina = null;

        for (var tentativa = 1; tentativa <= TentativasParaPaginaVazia; tentativa++)
        {
            pagina = await ObterAsync<RespostaPaginada<DespesaJson>>(caminho, cancelamento)
                .ConfigureAwait(false);

            if (pagina is not null && pagina.Dados.Count > 0)
            {
                return pagina;
            }

            if (tentativa < TentativasParaPaginaVazia)
            {
                if (_esperaEntreTentativas > TimeSpan.Zero)
                {
                    await Task.Delay(_esperaEntreTentativas, cancelamento).ConfigureAwait(false);
                }
            }
        }

        return pagina;
    }

    private static DespesaDaFonte Converter(DespesaJson despesa) => new()
    {
        Ano = despesa.Ano,
        Mes = despesa.Mes,
        TipoDespesa = despesa.TipoDespesa ?? string.Empty,
        CodigoDocumento = despesa.CodDocumento ?? string.Empty,
        NumeroDocumento = despesa.NumDocumento,
        CodigoTipoDocumento = despesa.CodTipoDocumento,

        // Fixa em UTC em vez de deixar o parser adotar o fuso da maquina. A data entra no
        // HashUnico da despesa; um offset que varie por servidor duplicaria a mesma nota.
        DataDocumento = new DateTimeOffset(
            DateTime.SpecifyKind(despesa.DataDocumento, DateTimeKind.Utc)),

        ValorDocumento = despesa.ValorDocumento,
        ValorLiquido = despesa.ValorLiquido,
        ValorGlosa = despesa.ValorGlosa,
        UrlDocumento = despesa.UrlDocumento,
        NomeFornecedor = despesa.NomeFornecedor,
        CnpjCpfFornecedor = despesa.CnpjCpfFornecedor,
        CodigoLote = despesa.CodLote,
        Parcela = despesa.Parcela,
    };

    /// <summary>
    /// Devolve nulo em 404 e propaga qualquer outra falha. Engolir erro de servidor aqui
    /// viraria "deputado sem despesas" no banco, que e' indistinguivel de dado real.
    /// </summary>
    private async Task<T?> ObterAsync<T>(string caminho, CancellationToken cancelamento)
    {
        using var resposta = await http.GetAsync(caminho, cancelamento).ConfigureAwait(false);

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        resposta.EnsureSuccessStatusCode();

        await using var corpo = await resposta.Content.ReadAsStreamAsync(cancelamento)
            .ConfigureAwait(false);

        return await JsonSerializer.DeserializeAsync<T>(corpo, Json, cancelamento)
            .ConfigureAwait(false);
    }

    private sealed record EnvelopeDe<T>
    {
        public T? Dados { get; init; }
    }
}
