using System.Globalization;
using System.Text.Json;
using Explanando.Application.Ingestao;

namespace Explanando.Infrastructure.Integracoes.Senado;

/// <summary>
/// Converte um lançamento da CEAPS no que a ingestão entende.
///
/// <para>É a borda onde os defeitos medidos do dado do Senado param de ser problema. Cada
/// decisão abaixo corresponde a uma armadilha contada em <c>docs/contratos/senado.md</c>, e
/// há um <c>Theory</c> sobre os quinze registros reais de <c>senado-ceaps-amostra.json</c> —
/// escolhidos justamente pelas armadilhas, não pelo caminho feliz.</para>
///
/// <para><b>Nada aqui rejeita registro.</b> Despesa recusada é gasto público que a página
/// deixa de mostrar, e a origem do Senado é suja o bastante para que rejeitar por rigor
/// custasse uma fatia real da base. O que não dá para afirmar vira ausência declarada, e o
/// motor se abstém — que é a disciplina do <c>ADR-0007</c>.</para>
/// </summary>
public static class ConversorDoSenado
{
    /// <summary>
    /// As mesmas opcoes que o cliente usa. Ficam aqui para que o teste atravesse a
    /// desserializacao de verdade — e nao so' a conversao —, o que faz um
    /// <c>[JsonPropertyName]</c> errado quebrar o teste em vez de virar campo nulo em
    /// producao.
    /// </summary>
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Converte o corpo de <c>despesas_ceaps/{ano}</c> — um array cru, sem envelope.
    ///
    /// Registro que nao desserializa vira <c>null</c> e e' descartado silenciosamente pelo
    /// filtro: um item malformado no meio de 23 mil nao pode derrubar o ano inteiro.
    /// </summary>
    public static IReadOnlyList<DespesaDaFonte> ConverterLote(string json)
    {
        var registros = JsonSerializer.Deserialize<DespesaCeapsJson[]>(json, Opcoes) ?? [];

        return [.. registros.Select(Converter)];
    }

    /// <summary>
    /// Marcador para tipo de despesa que a origem não informou (76 de 23.615 em 2025).
    ///
    /// Deixar vazio faria o catálogo guardar string em branco e a tela exibir um rótulo
    /// mudo. Assim o texto diz o que houve, e <c>REG_KYB_001</c> se abstém porque o tipo
    /// não está na tabela curada — que é o comportamento correto.
    /// </summary>
    public const string TipoAusente = "(tipo não informado pela origem)";

    /// <summary>
    /// Nenhum documento da CEAPS é anterior a isto. A CEAPS existe desde 2008; qualquer
    /// data antes da Constituição é corrupção, não histórico.
    /// </summary>
    private static readonly DateTimeOffset PisoDePlausibilidade =
        new(1988, 10, 5, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Os sete rótulos de documento do Senado, mapeados para o código numérico que a Câmara
    /// usa e que <c>Despesa.EhDocumentoFiscal</c> conhece.
    ///
    /// <para>Só as duas notas fiscais recebem 0 e 4. Cupom fiscal também é documento fiscal,
    /// mas <c>REG_KYB_005</c> procura <b>série sequencial do emitente</b>, e cupom não tem —
    /// incluí-lo geraria falso positivo na regra mais delicada do conjunto.</para>
    /// </summary>
    private const int NotaFiscal = 0;
    private const int NotaFiscalEletronica = 4;
    private const int OutroDocumento = 999;

    internal static DespesaDaFonte Converter(DespesaCeapsJson registro)
    {
        ArgumentNullException.ThrowIfNull(registro);

        var competencia = CompetenciaPlausivel(registro);

        return new DespesaDaFonte
        {
            Ano = competencia.Ano,
            Mes = competencia.Mes,
            TipoDespesa = string.IsNullOrWhiteSpace(registro.TipoDespesa)
                ? TipoAusente
                : registro.TipoDespesa.Trim(),

            // O id do lancamento e' o unico identificador estavel: `documento` repete entre
            // fornecedores (varias notas "1") e nao serve de chave.
            CodigoDocumento = registro.Id.ToString(CultureInfo.InvariantCulture),
            NumeroDocumento = registro.Documento,
            CodigoTipoDocumento = CodigoDe(registro.TipoDocumento),
            DataDocumento = DataPlausivel(registro, competencia),

            // A CEAPS publica um valor so'. Documento e liquido recebem o mesmo numero, e a
            // glosa fica NULA — nao zero. Ver ADR-0012 §2: zero afirmaria que houve
            // conferencia e nada foi cortado, o que ninguem disse.
            ValorDocumento = registro.ValorReembolsado,
            ValorLiquido = registro.ValorReembolsado,
            ValorGlosa = null,

            // O Senado nao publica comprovante em campo nenhum. Ver ADR-0012 §1.
            UrlDocumento = null,

            NomeFornecedor = registro.Fornecedor,

            // CPF mascarado ("457.***.***-00") e CNPJ de 12-13 digitos chegam crus. CpfCnpj
            // os classifica como invalidos, e invalido nao e' enriquecivel — que e' o
            // desfecho certo: preencher o zero a esquerda de um CNPJ curto inventaria um
            // documento que existe e pertence a outra empresa (testado: so' 1 dos 11 passa
            // no digito verificador depois do padding).
            CnpjCpfFornecedor = registro.CpfCnpj,
        };
    }

    private static int CodigoDe(string? tipoDocumento) =>
        tipoDocumento?.Trim().ToUpperInvariant() switch
        {
            "NOTA FISCAL ELETRÔNICA" => NotaFiscalEletronica,
            "NOTA FISCAL" => NotaFiscal,
            _ => OutroDocumento,
        };

    /// <summary>
    /// A competência declarada, ou a data do documento quando <c>ano</c>/<c>mes</c> vêm
    /// fora de faixa. É a mesma política do tradutor da Câmara.
    /// </summary>
    private static (int Ano, int Mes) CompetenciaPlausivel(DespesaCeapsJson registro)
    {
        if (registro.Ano >= 1988 && registro.Mes is >= 1 and <= 12)
        {
            return (registro.Ano, registro.Mes);
        }

        var data = registro.Data ?? PisoDePlausibilidade;
        return (data.Year, data.Month);
    }

    /// <summary>
    /// A data do documento, substituída pela competência quando é <b>impossível</b>.
    ///
    /// <para>Impossível é pouco: anterior à Constituição, ou ausente. O registro de 2025 com
    /// data <c>0202-02-24</c> cai aqui.</para>
    ///
    /// <para><b>Data apenas divergente não é corrigida.</b> Em 12,6% dos registros a data
    /// cai num mês diferente da competência, e em alguns num ano anterior — nota emitida em
    /// dezembro e reembolsada em março, reembolso tardio de 2023 numa competência de 2025.
    /// Isso é plausível, e "consertar" apagaria a informação de que a nota é antiga, que é
    /// justamente o tipo de coisa que interessa a quem audita.</para>
    /// </summary>
    private static DateTimeOffset DataPlausivel(
        DespesaCeapsJson registro,
        (int Ano, int Mes) competencia)
    {
        if (registro.Data is { } data && data >= PisoDePlausibilidade)
        {
            return data;
        }

        return new DateTimeOffset(competencia.Ano, competencia.Mes, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
