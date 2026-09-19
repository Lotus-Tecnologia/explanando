using System.Text.Json.Serialization;

namespace Explanando.Infrastructure.Integracoes.Siorg;

/// <summary>
/// O envelope que <b>toda</b> resposta do SIORG carrega.
///
/// <para><b>É a peça mais importante do contrato</b>, e não um cabeçalho decorativo: o
/// serviço responde <c>HTTP 200</c> mesmo quando recusa a consulta, e a recusa só aparece em
/// <c>codigoErro</c>. Um cliente que olhasse o status HTTP registraria "sucesso, zero
/// unidades" — e uma coleta assim, lida como completa, concluiria que órgãos desapareceram.
/// </para>
/// </summary>
internal sealed record ServicoJson
{
    [JsonPropertyName("codigoErro")]
    public int CodigoErro { get; init; }

    [JsonPropertyName("mensagem")]
    public string? Mensagem { get; init; }

    [JsonPropertyName("versaoServico")]
    public string? VersaoServico { get; init; }
}

/// <summary>Resposta de lista: <c>/orgao-entidade</c> e <c>/estrutura-organizacional</c>.</summary>
internal sealed record ListaDeUnidadesJson
{
    [JsonPropertyName("servico")]
    public ServicoJson? Servico { get; init; }

    [JsonPropertyName("unidades")]
    public IReadOnlyList<UnidadeJson>? Unidades { get; init; }
}

/// <summary>Resposta de unidade única: <c>/unidade-organizacional/{codigo}</c>.</summary>
internal sealed record UnidadeUnicaJson
{
    [JsonPropertyName("servico")]
    public ServicoJson? Servico { get; init; }

    [JsonPropertyName("unidade")]
    public UnidadeJson? Unidade { get; init; }
}

/// <summary>
/// Uma unidade como o SIORG a descreve.
///
/// <para>Tudo anulável de propósito: a raiz devolve <c>codigoPoder</c> e
/// <c>codigoEsfera</c> como string vazia, os entes devolvem natureza jurídica nula, e um
/// contrato de borda que exigisse campo transformaria mudança na origem em exceção de
/// desserialização no meio de uma coleta de vinte mil unidades.</para>
/// </summary>
internal sealed record UnidadeJson
{
    [JsonPropertyName("codigoUnidade")]
    public string? CodigoUnidade { get; init; }

    [JsonPropertyName("codigoUnidadePai")]
    public string? CodigoUnidadePai { get; init; }

    [JsonPropertyName("codigoOrgaoEntidade")]
    public string? CodigoOrgaoEntidade { get; init; }

    [JsonPropertyName("codigoTipoUnidade")]
    public string? CodigoTipoUnidade { get; init; }

    [JsonPropertyName("nome")]
    public string? Nome { get; init; }

    [JsonPropertyName("sigla")]
    public string? Sigla { get; init; }

    [JsonPropertyName("codigoEsfera")]
    public string? CodigoEsfera { get; init; }

    [JsonPropertyName("codigoPoder")]
    public string? CodigoPoder { get; init; }

    [JsonPropertyName("codigoNaturezaJuridica")]
    public string? CodigoNaturezaJuridica { get; init; }

    [JsonPropertyName("nivelNormatizacao")]
    public string? NivelNormatizacao { get; init; }

    [JsonPropertyName("versaoConsulta")]
    public string? VersaoConsulta { get; init; }
}
