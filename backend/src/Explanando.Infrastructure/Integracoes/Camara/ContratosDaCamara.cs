using System.Text.Json.Serialization;

namespace Explanando.Infrastructure.Integracoes.Camara;

/// <summary>
/// Espelho fiel do JSON da API da Camara, campo a campo, como capturado em
/// <c>docs/contratos/</c>. Nada aqui e' interpretado — a traducao para o dominio acontece
/// depois, e so' o que o V1 usa atravessa a fronteira.
/// </summary>
internal sealed record RespostaPaginada<T>
{
    [JsonPropertyName("dados")]
    public List<T> Dados { get; init; } = [];

    [JsonPropertyName("links")]
    public List<LinkDaCamara> Links { get; init; } = [];

    /// <summary>Href do proximo lote, ou nulo quando esta e' a ultima pagina.</summary>
    public string? ProximaPagina => Links
        .Find(link => string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase))
        ?.Href;
}

internal sealed record LinkDaCamara
{
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    [JsonPropertyName("href")]
    public string? Href { get; init; }
}

internal sealed record DeputadoResumoJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    [JsonPropertyName("siglaPartido")]
    public string? SiglaPartido { get; init; }

    [JsonPropertyName("siglaUf")]
    public string? SiglaUf { get; init; }

    [JsonPropertyName("idLegislatura")]
    public int? IdLegislatura { get; init; }

    [JsonPropertyName("urlFoto")]
    public string? UrlFoto { get; init; }
}

/// <summary>
/// O detalhe traz tambem `cpf`, `dataNascimento`, `municipioNascimento` e `redeSocial`.
/// Deliberadamente sem propriedade para eles: o que nao e' desserializado nao pode vazar
/// para dentro do sistema por descuido.
/// </summary>
internal sealed record DeputadoDetalheJson
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("nomeCivil")]
    public string? NomeCivil { get; init; }

    [JsonPropertyName("ultimoStatus")]
    public UltimoStatusJson? UltimoStatus { get; init; }
}

internal sealed record UltimoStatusJson
{
    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    [JsonPropertyName("nomeEleitoral")]
    public string? NomeEleitoral { get; init; }

    [JsonPropertyName("siglaPartido")]
    public string? SiglaPartido { get; init; }

    [JsonPropertyName("siglaUf")]
    public string? SiglaUf { get; init; }

    [JsonPropertyName("idLegislatura")]
    public int? IdLegislatura { get; init; }

    [JsonPropertyName("urlFoto")]
    public string? UrlFoto { get; init; }

    [JsonPropertyName("situacao")]
    public string? Situacao { get; init; }
}

internal sealed record DespesaJson
{
    [JsonPropertyName("ano")]
    public int Ano { get; init; }

    [JsonPropertyName("mes")]
    public int Mes { get; init; }

    [JsonPropertyName("tipoDespesa")]
    public string? TipoDespesa { get; init; }

    [JsonPropertyName("codDocumento")]
    public string? CodDocumento { get; init; }

    [JsonPropertyName("numDocumento")]
    public string? NumDocumento { get; init; }

    [JsonPropertyName("codTipoDocumento")]
    public int CodTipoDocumento { get; init; }

    /// <summary>
    /// Chega como "2026-07-06T00:00:00", sem fuso. E' lido como <see cref="DateTime"/> e
    /// fixado em UTC no cliente — deixar o parser assumir o fuso da maquina faria a mesma
    /// nota gerar hashes diferentes em servidores diferentes, e a despesa entraria duas
    /// vezes no banco.
    /// </summary>
    [JsonPropertyName("dataDocumento")]
    public DateTime DataDocumento { get; init; }

    [JsonPropertyName("valorDocumento")]
    public decimal ValorDocumento { get; init; }

    [JsonPropertyName("valorLiquido")]
    public decimal ValorLiquido { get; init; }

    [JsonPropertyName("valorGlosa")]
    public decimal ValorGlosa { get; init; }

    [JsonPropertyName("urlDocumento")]
    public string? UrlDocumento { get; init; }

    [JsonPropertyName("nomeFornecedor")]
    public string? NomeFornecedor { get; init; }

    [JsonPropertyName("cnpjCpfFornecedor")]
    public string? CnpjCpfFornecedor { get; init; }

    [JsonPropertyName("codLote")]
    public long CodLote { get; init; }

    [JsonPropertyName("parcela")]
    public int Parcela { get; init; }
}
