using System.Text.Json.Serialization;
using Explanando.Application.Ingestao;

namespace Explanando.Infrastructure.Integracoes.Senado;

/// <summary>
/// As despesas de um senador dentro de um lote anual da CEAPS.
///
/// <para>A CEAPS entrega o ano inteiro de todos os senadores num array só, com
/// <c>codSenador</c> em cada registro. Este tipo é o que devolve a identidade que
/// <see cref="DespesaDaFonte"/> não carrega.</para>
/// </summary>
public sealed record DespesasDeUmSenador
{
    public required int CodSenador { get; init; }

    /// <summary>
    /// O nome como a CEAPS o grafou. É a rede de segurança para quem não está mais nas
    /// listas de exercício — suplente que assumiu e saiu.
    /// </summary>
    public string? NomeNaOrigem { get; init; }

    public required IReadOnlyList<DespesaDaFonte> Despesas { get; init; }
}

/// <summary>
/// Um lançamento da CEAPS, como a API administrativa do Senado entrega.
///
/// <para>Treze campos, verificados contra 2019, 2023, 2024, 2025 e 2026 — o esquema não mudou
/// uma vez. Ver <c>docs/contratos/senado.md</c>.</para>
///
/// <para>Tudo aqui é anulável de propósito, inclusive o que o levantamento mostrou vir sempre
/// preenchido. Um contrato de borda que exige campo obrigatório transforma mudança na origem
/// em exceção de desserialização, e aí a coleta inteira para por causa de um registro.</para>
/// </summary>
internal sealed record DespesaCeapsJson
{
    /// <summary>Identificador do lançamento. Vira o código do documento no domínio.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("codSenador")]
    public int CodSenador { get; init; }

    [JsonPropertyName("nomeSenador")]
    public string? NomeSenador { get; init; }

    [JsonPropertyName("ano")]
    public int Ano { get; init; }

    [JsonPropertyName("mes")]
    public int Mes { get; init; }

    /// <summary>Nulo em 76 dos 23.615 registros de 2025.</summary>
    [JsonPropertyName("tipoDespesa")]
    public string? TipoDespesa { get; init; }

    /// <summary>String legível, não código: "Nota Fiscal Eletrônica", "Boleto", "Recibo"…</summary>
    [JsonPropertyName("tipoDocumento")]
    public string? TipoDocumento { get; init; }

    /// <summary>98,6% CNPJ formatado. 1,3% CPF mascarado com 5 dígitos visíveis.</summary>
    [JsonPropertyName("cpfCnpj")]
    public string? CpfCnpj { get; init; }

    [JsonPropertyName("fornecedor")]
    public string? Fornecedor { get; init; }

    /// <summary>Número da nota. Só 84% é numérico — aparecem "08/2025" e "00000266".</summary>
    [JsonPropertyName("documento")]
    public string? Documento { get; init; }

    /// <summary>
    /// Data do documento. <b>Corrompida em 1,36% dos casos</b>, incluindo o ano 202.
    /// Ver <c>ConversorDoSenado</c>.
    /// </summary>
    [JsonPropertyName("data")]
    public DateTimeOffset? Data { get; init; }

    /// <summary>
    /// Descrição livre da despesa, em 38,8% dos registros. <b>A Câmara não tem equivalente.</b>
    ///
    /// Ainda não é ingerido: o domínio não tem onde guardá-lo, e a Camada 2, que é quem o
    /// consumiria, não existe. Fica mapeado aqui para que a decisão de descartá-lo seja
    /// visível em vez de acidental.
    /// </summary>
    [JsonPropertyName("detalhamento")]
    public string? Detalhamento { get; init; }

    /// <summary>O único valor. A CEAPS não publica valor de documento nem glosa.</summary>
    [JsonPropertyName("valorReembolsado")]
    public decimal ValorReembolsado { get; init; }
}
