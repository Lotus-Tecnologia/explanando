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
public sealed class ClienteCamara(HttpClient http) : IFonteDeParlamentares
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

        while (caminho is not null && visitados.Add(caminho))
        {
            var pagina = await ObterAsync<RespostaPaginada<DespesaJson>>(caminho, cancelamento)
                .ConfigureAwait(false);

            if (pagina is null || pagina.Dados.Count == 0)
            {
                yield break;
            }

            foreach (var despesa in pagina.Dados)
            {
                yield return Converter(despesa);
            }

            caminho = pagina.ProximaPagina;
        }
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
