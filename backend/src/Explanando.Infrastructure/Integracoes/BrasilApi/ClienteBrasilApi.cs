using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;

namespace Explanando.Infrastructure.Integracoes.BrasilApi;

/// <summary>
/// Cliente da BrasilAPI para cadastro de pessoa juridica (CNPJ, CNAE, situacao e QSA).
///
/// Duas regras de convivencia com um servico comunitario e gratuito:
///
/// 1. <b>Nao pergunta o que ja se sabe invalido.</b> 2 em cada 100 despesas trazem o CNPJ
///    sentinela "00000000000001", que a fonte rejeita com 400. Filtrar antes evita queimar
///    requisicao e poluir o log de erro em 2% de toda a ingestao.
/// 2. <b>Ausencia nao e' erro.</b> 404 e 400 devolvem nulo — CNPJ fora do cadastro e' rotina.
///    Ja 5xx e 429 sao propagados: quem decide reexecutar e' a politica de resiliencia.
/// </summary>
public sealed class ClienteBrasilApi(HttpClient http) : IFonteDeCadastroEmpresarial
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<EmpresaDaFonte?> ConsultarAsync(
        string cnpj,
        CancellationToken cancelamento = default)
    {
        var documento = CpfCnpj.Criar(cnpj);

        if (!documento.PodeSerEnriquecido)
        {
            return null;
        }

        using var resposta = await http
            .GetAsync($"cnpj/v1/{documento.Numero}", cancelamento)
            .ConfigureAwait(false);

        if (resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }

        resposta.EnsureSuccessStatusCode();

        await using var corpo = await resposta.Content.ReadAsStreamAsync(cancelamento)
            .ConfigureAwait(false);

        var empresa = await JsonSerializer.DeserializeAsync<EmpresaJson>(corpo, Json, cancelamento)
            .ConfigureAwait(false);

        return empresa is null ? null : Converter(empresa, documento.Numero);
    }

    private static EmpresaDaFonte Converter(EmpresaJson empresa, string cnpj) => new()
    {
        Cnpj = cnpj,
        RazaoSocial = empresa.RazaoSocial ?? string.Empty,
        NomeFantasia = empresa.NomeFantasia,
        CnaePrincipal = empresa.CnaeFiscal?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        CnaesSecundarios = empresa.CnaesSecundarios
            .Where(cnae => cnae.Codigo is not null)
            .Select(cnae => cnae.Codigo!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList(),
        DescricaoSituacaoCadastral = empresa.DescricaoSituacaoCadastral,
        DataSituacaoCadastral = empresa.DataSituacaoCadastral,
        DataInicioAtividade = empresa.DataInicioAtividade,
        OptantePeloMei = empresa.OpcaoPeloMei,
        Porte = empresa.Porte,
        Logradouro = empresa.Logradouro,
        Numero = empresa.Numero,
        Bairro = empresa.Bairro,
        Municipio = empresa.Municipio,
        CodigoMunicipioIbge = empresa.CodigoMunicipioIbge,
        Uf = empresa.Uf,
        Cep = empresa.Cep,
        QuadroSocietario = empresa.Qsa
            .Where(socio => !string.IsNullOrWhiteSpace(socio.NomeSocio))
            .Select(socio => new SocioDaFonte
            {
                NomeSocio = socio.NomeSocio!,
                CpfMascarado = socio.CnpjCpfDoSocio,
                Qualificacao = socio.QualificacaoSocio,
                DataEntradaSociedade = socio.DataEntradaSociedade,
            })
            .ToList(),
    };

    /// <summary>
    /// Espelho parcial do JSON da BrasilAPI. A resposta real tem 48 campos na raiz; aqui
    /// estao apenas os que alguma regra do motor consome. `faixa_etaria` do socio fica de
    /// fora de proposito: o que nao e' desserializado nao vaza para dentro do sistema.
    /// </summary>
    private sealed record EmpresaJson
    {
        [JsonPropertyName("razao_social")]
        public string? RazaoSocial { get; init; }

        [JsonPropertyName("nome_fantasia")]
        public string? NomeFantasia { get; init; }

        [JsonPropertyName("cnae_fiscal")]
        public long? CnaeFiscal { get; init; }

        [JsonPropertyName("cnaes_secundarios")]
        public List<CnaeJson> CnaesSecundarios { get; init; } = [];

        [JsonPropertyName("descricao_situacao_cadastral")]
        public string? DescricaoSituacaoCadastral { get; init; }

        [JsonPropertyName("data_situacao_cadastral")]
        public DateOnly? DataSituacaoCadastral { get; init; }

        [JsonPropertyName("data_inicio_atividade")]
        public DateOnly? DataInicioAtividade { get; init; }

        [JsonPropertyName("opcao_pelo_mei")]
        public bool? OpcaoPeloMei { get; init; }

        [JsonPropertyName("porte")]
        public string? Porte { get; init; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; init; }

        [JsonPropertyName("numero")]
        public string? Numero { get; init; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; init; }

        [JsonPropertyName("municipio")]
        public string? Municipio { get; init; }

        [JsonPropertyName("codigo_municipio_ibge")]
        public int? CodigoMunicipioIbge { get; init; }

        [JsonPropertyName("uf")]
        public string? Uf { get; init; }

        [JsonPropertyName("cep")]
        public string? Cep { get; init; }

        [JsonPropertyName("qsa")]
        public List<SocioJson> Qsa { get; init; } = [];
    }

    private sealed record CnaeJson
    {
        [JsonPropertyName("codigo")]
        public long? Codigo { get; init; }
    }

    private sealed record SocioJson
    {
        [JsonPropertyName("nome_socio")]
        public string? NomeSocio { get; init; }

        /// <summary>Chega mascarado: "***456789**", 6 dos 11 digitos.</summary>
        [JsonPropertyName("cnpj_cpf_do_socio")]
        public string? CnpjCpfDoSocio { get; init; }

        [JsonPropertyName("qualificacao_socio")]
        public string? QualificacaoSocio { get; init; }

        [JsonPropertyName("data_entrada_sociedade")]
        public DateOnly? DataEntradaSociedade { get; init; }
    }
}
