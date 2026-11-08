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
    /// <summary>
    /// Parte cortada pela casa legislativa. <b>Nulo quando a origem não informa</b> — não
    /// zero. Ver a nota da propriedade correspondente em <see cref="Despesa"/>.
    /// </summary>
    public decimal? ValorGlosa { get; init; }
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
        CodigoDocumento = string.Empty;
        NumeroDocumento = string.Empty;
        HashUnico = [];
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

    /// <summary>Código do tipo no <see cref="CatalogoDeTiposDeDespesa"/>. Zero = fora do catálogo.</summary>
    public short CodigoTipoDespesa { get; private set; }

    /// <summary>
    /// O nome por extenso, guardado <b>apenas</b> quando o tipo não está no catálogo.
    ///
    /// Nulo em praticamente toda linha. É o que garante que um tipo novo criado por Ato da
    /// Mesa no meio de uma coleta de doze horas não custe uma única despesa.
    /// </summary>
    public string? TipoDespesaNaoCatalogado { get; private set; }

    /// <summary>
    /// O tipo por extenso, venha do catálogo ou da coluna de exceção. É o que as regras e a
    /// tela leem — nenhuma delas sabe que existe um código por baixo.
    /// </summary>
    public string TipoDespesa =>
        CatalogoDeTiposDeDespesa.NomeDe(CodigoTipoDespesa)
        ?? TipoDespesaNaoCatalogado
        ?? string.Empty;

    public string CodigoDocumento { get; private set; }

    public string NumeroDocumento { get; private set; }

    /// <summary>0 = Nota Fiscal, 4 = Nota Fiscal Eletronica. Ver <see cref="EhDocumentoFiscal"/>.</summary>
    public int CodigoTipoDocumento { get; private set; }

    public DateTimeOffset DataDocumento { get; private set; }

    /// <summary>Valor pedido na nota.</summary>
    public decimal ValorDocumento { get; private set; }

    /// <summary>Valor efetivamente reembolsado pelo erario.</summary>
    public decimal ValorLiquido { get; private set; }

    /// <summary>
    /// Parte cortada pela casa legislativa. Sinal proprio: aparece em ~1 de 100 despesas
    /// da Camara.
    ///
    /// <para><b>Nulo significa "a origem nao informa", e nao "nao houve corte".</b> A
    /// CEAPS do Senado publica um valor unico: guardar zero ali diria que a despesa passou
    /// por malha fina e foi integralmente aprovada, que e' afirmacao que ninguem fez. E'
    /// a mesma disciplina de <c>SemBaseParaAvaliar</c> no motor — ausencia de dado nunca
    /// vira conclusao.</para>
    /// </summary>
    public decimal? ValorGlosa { get; private set; }

    /// <summary>
    /// Se a Camara publicou o arquivo do documento.
    ///
    /// <para>Falso em 52% das notas de tipo 0 — nao e' falha de coleta, e' ausencia na
    /// origem. Guardar o booleano em vez da URL inteira economiza ~44 MB projetados, e o
    /// endereco se reconstroi com <see cref="UrlDeDocumento"/>.</para>
    /// </summary>
    public bool TemDocumentoPublicado { get; private set; }

    /// <summary>
    /// Ano da pasta do arquivo, <b>somente quando diverge</b> do ano de
    /// <see cref="DataDocumento"/> — 2,3% dos casos reais.
    ///
    /// Nulo significa "o mesmo ano da nota". Guardar a excecao em vez de derivar por regra
    /// e' deliberado: a regra acertaria 99,95%, e o resto viraria link quebrado numa pagina
    /// com nome e foto.
    /// </summary>
    public short? AnoDoArquivo { get; private set; }

    public long CodigoLote { get; private set; }

    public int Parcela { get; private set; }

    /// <summary>
    /// Identidade natural da despesa, em 16 bytes: os primeiros 128 bits do SHA-256.
    ///
    /// <para>Era <c>char(64)</c> hexadecimal. O indice unico sobre ela era o maior da base
    /// — 3.560 kB sozinho, mais da metade de todos os indices de <c>despesas</c>. Truncar
    /// para 128 bits leva a probabilidade de colisao em 732.000 registros para a ordem de
    /// 10^-27; o hexadecimal gastava o dobro do espaco para guardar a mesma informacao
    /// duas vezes.</para>
    /// </summary>
    public byte[] HashUnico { get; private set; }

    /// <summary>Houve corte. Falso tambem quando a origem nao informa — ver <see cref="GlosaConhecida"/>.</summary>
    public bool TemGlosa => ValorGlosa > 0m;

    /// <summary>
    /// Se a origem informa a glosa. Falso para o Senado inteiro.
    ///
    /// A interface consulta isto antes de exibir a quebra "valor declarado x glosa": mostrar
    /// R$ 0,00 onde a informacao nao existe e' pior que omitir a linha.
    /// </summary>
    public bool GlosaConhecida => ValorGlosa is not null;

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

    /// <summary>
    /// O passo do valor como a Camara o publica: seis digitos significativos. Acima de
    /// R$ 10 mil o centavo nao existe na origem; acima de R$ 100 mil, nem o decimo.
    /// </summary>
    public static decimal PrecisaoDaOrigem(decimal valor)
    {
        var precisao = 0.01m;

        for (var limite = 10_000m; Math.Abs(valor) >= limite; limite *= 10m)
        {
            precisao *= 10m;
        }

        return precisao;
    }

    public static Despesa Criar(NovaDespesa dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        // Nulo e' "a origem nao informa" e passa direto: nao ha o que validar sobre um
        // numero que ninguem publicou.
        var glosaConfiavel = dados.ValorGlosa;

        if (dados.ValorGlosa is { } glosa)
        {
            // Valor negativo e' estorno e e' dado legitimo — ver EhEstorno. So' a glosa
            // permanece obrigatoriamente nao negativa: ela e' um corte, nao um lancamento.
            ArgumentOutOfRangeException.ThrowIfNegative(glosa);

            var glosaEsperada = dados.ValorDocumento - dados.ValorLiquido;

            // A consistencia da glosa so' vale para lancamento comum. Num estorno os dois
            // valores sao negativos e a aritmetica de corte nao se aplica.
            var ehLancamentoComum = dados.ValorDocumento >= 0m && dados.ValorLiquido >= 0m;

            // A tolerancia segue a precisao que a origem publica (ver PrecisaoDaOrigem).
            // Fora dela a glosa nao fecha e entra como desconhecida: rejeitar tirava a
            // despesa inteira do volume (146 em 78 deputados, levantamento de 08/11/2026).
            var tolerancia = (PrecisaoDaOrigem(dados.ValorDocumento)
                + PrecisaoDaOrigem(dados.ValorLiquido)) / 2m + 0.01m;

            if (ehLancamentoComum && glosa != 0m
                && Math.Abs(glosaEsperada - glosa) > tolerancia)
            {
                glosaConfiavel = null;
            }
        }

        var despesa = new Despesa
        {
            ParlamentarId = dados.ParlamentarId,
            DocumentoFornecedor = dados.DocumentoFornecedor,
            // O nome do MEI traz o CPF do dono; entra mascarado (ver CpfEmTexto).
            NomeFornecedor = CpfEmTexto.Mascarar(dados.NomeFornecedor.Trim()),
            Competencia = dados.Competencia,
            CodigoDocumento = dados.CodigoDocumento.Trim(),
            NumeroDocumento = dados.NumeroDocumento.Trim(),
            CodigoTipoDocumento = dados.CodigoTipoDocumento,
            DataDocumento = dados.DataDocumento,
            ValorDocumento = dados.ValorDocumento,
            ValorLiquido = dados.ValorLiquido,
            ValorGlosa = glosaConfiavel,
            CodigoLote = dados.CodigoLote,
            Parcela = dados.Parcela,
        };

        var tipo = dados.TipoDespesa.Trim();
        despesa.CodigoTipoDespesa = CatalogoDeTiposDeDespesa.CodigoDe(tipo);

        // So' guarda o texto quando o catalogo nao conhece o tipo. Ver a nota da classe do
        // catalogo: recusar o desconhecido custaria despesa, e despesa perdida e' gasto
        // publico que a pagina deixa de mostrar.
        if (despesa.CodigoTipoDespesa == CatalogoDeTiposDeDespesa.NaoCatalogado)
        {
            despesa.TipoDespesaNaoCatalogado = tipo;
        }

        // A URL nao e' guardada: dela se extrai o que nao existe em nenhum outro lugar.
        var analise = UrlDeDocumento.Analisar(dados.UrlDocumento);
        despesa.TemDocumentoPublicado = analise.TemDocumento;

        if (analise.AnoDoArquivo is { } ano && ano != dados.DataDocumento.Year)
        {
            despesa.AnoDoArquivo = (short)ano;
        }

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
    /// <summary>
    /// A15 (08/11/2026): a Camara publicava a passagem SIGEPA sem documento do fornecedor e
    /// passou a publicar o CNPJ da companhia. Fora do hash nessa rubrica, a passagem ja
    /// conhecida continua conhecida — as 184.280 da base foram gravadas com documento vazio,
    /// entao o hash delas nao muda.
    /// </summary>
    private static readonly short CodigoDaPassagemSigepa =
        CatalogoDeTiposDeDespesa.CodigoDe("PASSAGEM AÉREA - SIGEPA");

    private static byte[] CalcularHash(Despesa despesa)
    {
        var documento = despesa.CodigoTipoDespesa == CodigoDaPassagemSigepa
            ? string.Empty
            : despesa.DocumentoFornecedor.Numero;

        var partes = string.Join(
            '|',
            despesa.ParlamentarId.ToString("N", CultureInfo.InvariantCulture),
            documento,
            despesa.CodigoDocumento,
            despesa.NumeroDocumento,
            despesa.Competencia.ToString(),
            despesa.DataDocumento.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            despesa.ValorLiquido.ToString("F2", CultureInfo.InvariantCulture),
            despesa.CodigoLote.ToString(CultureInfo.InvariantCulture),
            despesa.Parcela.ToString(CultureInfo.InvariantCulture));

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(partes));
        return digest[..TamanhoDoHash];
    }

    /// <summary>128 bits. Ver a nota de <see cref="HashUnico"/>.</summary>
    public const int TamanhoDoHash = 16;

    /// <summary>
    /// O endereco publico do documento, reconstruido a partir da carteira do parlamentar.
    /// Nulo quando a Camara nao publicou o arquivo ou quando falta a carteira.
    /// </summary>
    public string? UrlPublica(int? numeroCarteira) =>
        TemDocumentoPublicado
            ? UrlDeDocumento.Derivar(
                CodigoTipoDocumento,
                CodigoDocumento,
                numeroCarteira,
                AnoDoArquivo ?? DataDocumento.Year)
            : null;
}
