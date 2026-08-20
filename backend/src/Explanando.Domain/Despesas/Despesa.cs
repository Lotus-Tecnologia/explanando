using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Explanando.Domain.Comum;

namespace Explanando.Domain.Despesas;

/// <summary>Dados de criacao de uma despesa, espelhando o contrato da API da Camara.</summary>
public sealed record NovaDespesa
{
    public required Guid ParlamentarId { get; init; }
    public required CpfCnpj DocumentoFornecedor { get; init; }
    public required string NomeFornecedor { get; init; }
    public required Competencia Competencia { get; init; }
    public required string TipoDespesa { get; init; }

    /// <summary>`codDocumento` da API — identificador do documento fiscal na Camara.</summary>
    public required string CodigoDocumento { get; init; }

    /// <summary>`numDocumento` — o numero impresso na nota. Base de REG_KYB_005.</summary>
    public required string NumeroDocumento { get; init; }

    /// <summary>
    /// `codTipoDocumento`: 0 = Nota Fiscal, 4 = Nota Fiscal Eletronica, demais valores
    /// sao recibo, bilhete aereo e afins. REG_KYB_005 procura numeracao sequencial baixa
    /// e so' faz sentido sobre documento fiscal — bilhete aereo tem numero de 13 digitos.
    /// </summary>
    public int CodigoTipoDocumento { get; init; }

    public required DateTimeOffset DataDocumento { get; init; }
    public required decimal ValorDocumento { get; init; }
    public required decimal ValorLiquido { get; init; }
    public decimal ValorGlosa { get; init; }
    public string? UrlDocumento { get; init; }
    public long CodigoLote { get; init; }
    public int Parcela { get; init; }
}

/// <summary>
/// Uma nota paga pela Cota para o Exercicio da Atividade Parlamentar (CEAP).
///
/// Modelada a partir do contrato real (docs/contratos/), nao da especificacao original —
/// que previa um unico campo <c>Valor</c> e nao previa o numero da nota fiscal.
/// </summary>
public sealed class Despesa : Entidade
{
    private Despesa()
    {
        // EF Core.
        DocumentoFornecedor = CpfCnpj.Criar(null);
        NomeFornecedor = string.Empty;
        TipoDespesa = string.Empty;
        CodigoDocumento = string.Empty;
        NumeroDocumento = string.Empty;
        HashUnico = string.Empty;
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>
    /// Nulo quando o fornecedor nao e' resolvivel — caso do CNPJ sentinela
    /// "00000000000001", presente em 2 de cada 100 despesas reais. A despesa
    /// e' persistida mesmo assim: descarta-la esconderia gasto publico.
    /// </summary>
    public Guid? FornecedorId { get; private set; }

    public CpfCnpj DocumentoFornecedor { get; private set; }

    public string NomeFornecedor { get; private set; }

    public Competencia Competencia { get; private set; }

    public string TipoDespesa { get; private set; }

    public string CodigoDocumento { get; private set; }

    public string NumeroDocumento { get; private set; }

    /// <summary>0 = Nota Fiscal, 4 = Nota Fiscal Eletronica. Ver <see cref="EhDocumentoFiscal"/>.</summary>
    public int CodigoTipoDocumento { get; private set; }

    public DateTimeOffset DataDocumento { get; private set; }

    /// <summary>Valor pedido na nota.</summary>
    public decimal ValorDocumento { get; private set; }

    /// <summary>Valor efetivamente reembolsado pelo erario.</summary>
    public decimal ValorLiquido { get; private set; }

    /// <summary>Parte cortada pela Camara. Sinal proprio: aparece em ~1 de 100 despesas.</summary>
    public decimal ValorGlosa { get; private set; }

    public string? UrlDocumento { get; private set; }

    public long CodigoLote { get; private set; }

    public int Parcela { get; private set; }

    /// <summary>SHA-256 hexadecimal (64 caracteres) que garante idempotencia da ingestao.</summary>
    public string HashUnico { get; private set; }

    public bool TemGlosa => ValorGlosa > 0m;

    /// <summary>
    /// Estorno: devolucao de valor a cota, lancada como despesa de valor negativo.
    ///
    /// <para>Descoberto na primeira coleta real, que recusou 1.223 registros por "valor
    /// negativo" — 4,3% de tudo. Sao majoritariamente cancelamentos de passagem aerea
    /// (SIGEPA), com o mesmo formato de uma despesa comum e sinal invertido.</para>
    ///
    /// <para>Rejeita-los seria pior que perder registro: <b>inflaria o gasto</b>. Um
    /// parlamentar que comprou passagem de R$ 1.846 e cancelou apareceria tendo gasto os
    /// R$ 1.846, e o ranking publico o colocaria acima de quem realmente gastou aquilo.</para>
    /// </summary>
    public bool EhEstorno => ValorLiquido < 0m;

    /// <summary>
    /// Se o documento e' nota fiscal (comum ou eletronica). Recibo, bilhete aereo e
    /// declaracao tem numeracao que nao segue serie sequencial do emitente, entao
    /// REG_KYB_005 precisa ignora-los.
    /// </summary>
    public bool EhDocumentoFiscal => CodigoTipoDocumento is 0 or 4;

    /// <summary>
    /// Base do score de risco. Usa o liquido, nao o documento: contar o valor pedido
    /// inflaria o ranking de quem teve despesa <b>barrada</b> pela propria Camara.
    /// </summary>
    public decimal ValorParaScore => ValorLiquido;

    public static Despesa Criar(NovaDespesa dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        // Valor negativo e' estorno e e' dado legitimo — ver EhEstorno. So' a glosa
        // permanece obrigatoriamente nao negativa: ela e' um corte, nao um lancamento.
        ArgumentOutOfRangeException.ThrowIfNegative(dados.ValorGlosa);

        var glosaEsperada = dados.ValorDocumento - dados.ValorLiquido;

        // A consistencia da glosa so' vale para lancamento comum. Num estorno os dois
        // valores sao negativos e a aritmetica de corte nao se aplica.
        var ehLancamentoComum = dados.ValorDocumento >= 0m && dados.ValorLiquido >= 0m;

        if (ehLancamentoComum && dados.ValorGlosa != 0m
            && Math.Abs(glosaEsperada - dados.ValorGlosa) > 0.01m)
        {
            throw new ArgumentException(
                $"Valor de glosa inconsistente: documento {dados.ValorDocumento} menos liquido "
                + $"{dados.ValorLiquido} da {glosaEsperada}, mas a origem informou "
                + $"{dados.ValorGlosa}.",
                nameof(dados));
        }

        var despesa = new Despesa
        {
            ParlamentarId = dados.ParlamentarId,
            DocumentoFornecedor = dados.DocumentoFornecedor,
            NomeFornecedor = dados.NomeFornecedor.Trim(),
            Competencia = dados.Competencia,
            TipoDespesa = dados.TipoDespesa.Trim(),
            CodigoDocumento = dados.CodigoDocumento.Trim(),
            NumeroDocumento = dados.NumeroDocumento.Trim(),
            CodigoTipoDocumento = dados.CodigoTipoDocumento,
            DataDocumento = dados.DataDocumento,
            ValorDocumento = dados.ValorDocumento,
            ValorLiquido = dados.ValorLiquido,
            ValorGlosa = dados.ValorGlosa,
            UrlDocumento = dados.UrlDocumento,
            CodigoLote = dados.CodigoLote,
            Parcela = dados.Parcela,
        };

        despesa.HashUnico = CalcularHash(despesa);
        return despesa;
    }

    /// <summary>Liga a despesa ao fornecedor depois que ele foi resolvido na ingestao.</summary>
    public void AssociarFornecedor(Guid fornecedorId)
    {
        if (!DocumentoFornecedor.PodeSerEnriquecido)
        {
            throw new InvalidOperationException(
                $"Despesa {Id} tem documento '{DocumentoFornecedor.Numero}', que nao identifica "
                + "fornecedor; associar seria inventar vinculo.");
        }

        FornecedorId = fornecedorId;
    }

    /// <summary>
    /// Identidade natural da despesa. Inclui apenas o que a Camara <b>nao corrige</b>:
    /// nome de fornecedor e URL do documento mudam sem que a despesa seja outra, e se
    /// entrassem aqui uma correcao na origem viraria duplicata no banco.
    /// </summary>
    private static string CalcularHash(Despesa despesa)
    {
        var partes = string.Join(
            '|',
            despesa.ParlamentarId.ToString("N", CultureInfo.InvariantCulture),
            despesa.DocumentoFornecedor.Numero,
            despesa.CodigoDocumento,
            despesa.NumeroDocumento,
            despesa.Competencia.ToString(),
            despesa.DataDocumento.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            despesa.ValorLiquido.ToString("F2", CultureInfo.InvariantCulture),
            despesa.CodigoLote.ToString(CultureInfo.InvariantCulture),
            despesa.Parcela.ToString(CultureInfo.InvariantCulture));

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(partes));
        return Convert.ToHexStringLower(digest);
    }
}
