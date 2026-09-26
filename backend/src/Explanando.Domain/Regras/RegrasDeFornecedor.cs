using System.Globalization;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Regras;

/// <summary>
/// REG_KYB_001 — o ramo declarado do fornecedor nao combina com o tipo da despesa.
///
/// Se abstem quando o fornecedor nao foi resolvido, quando ele nao tem CNAE legivel ou
/// quando o tipo de despesa esta fora da tabela curada. Ver ADR-0007.
/// </summary>
public sealed class RegraCnaeDivergente : IRegraAnomalia
{
    public string Codigo => "REG_KYB_001";

    public string Descricao => "Ramo de atividade do fornecedor destoa do tipo de despesa.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Precisa do ramo do fornecedor E do tipo na tabela curada: falta um dos dois e a
    // regra nao tem com o que comparar.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor | DadoExigido.CnaeDoFornecedor
        | DadoExigido.TipoNaTabelaCurada;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (var despesa in contexto.Despesas)
        {
            if (contexto.FornecedorDe(despesa) is not { } fornecedor)
            {
                continue;
            }

            var cnaes = new List<string?> { fornecedor.CnaePrincipal };
            cnaes.AddRange(fornecedor.CnaesSecundarios);

            if (CompatibilidadeCnae.Avaliar(despesa.TipoDespesa, cnaes)
                != ResultadoDeCompatibilidade.Incompativel)
            {
                continue;
            }

            yield return FlagAnomalia.Criar(
                despesa.Id,
                Codigo,
                Gravidade.Media,
                $"A despesa de '{despesa.TipoDespesa}' foi paga a "
                + $"'{fornecedor.RazaoSocial}', cujo ramo registrado na Receita Federal "
                + $"(CNAE {fornecedor.CnaePrincipal}) pertence a outra atividade econômica.",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_KYB_002 — nota emitida depois de o CNPJ ter sido baixado, suspenso, declarado
/// inapto ou nulo.
///
/// So' fala quando a data da nota e' <b>posterior</b> ao evento cadastral: empresa que
/// fechou depois emitiu notas legitimas antes disso.
/// </summary>
public sealed class RegraSituacaoCadastralIrregular : IRegraAnomalia
{
    public string Codigo => "REG_KYB_002";

    public string Descricao => "Nota emitida após irregularidade cadastral do fornecedor.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Sem situacao cadastral conhecida nao ha o que comparar com a data da nota.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor | DadoExigido.SituacaoDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (var despesa in contexto.Despesas)
        {
            if (contexto.FornecedorDe(despesa) is not { } fornecedor)
            {
                continue;
            }

            if (!fornecedor.EstavaIrregularEm(despesa.DataDocumento))
            {
                continue;
            }

            yield return FlagAnomalia.Criar(
                despesa.Id,
                Codigo,
                Gravidade.Alta,
                $"Inconsistência com a regra da Câmara: a nota de "
                + $"{despesa.DataDocumento:dd/MM/yyyy} foi emitida por '{fornecedor.RazaoSocial}', "
                + $"cuja situação cadastral na Receita Federal consta como "
                + $"{fornecedor.SituacaoCadastral} desde "
                + $"{fornecedor.DataSituacaoCadastral:dd/MM/yyyy}.",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_KYB_003 — empresa aberta ha pouco tempo ja faturando valores expressivos.
///
/// Empresa nova nao e' irregular; o sinal esta na combinacao com valor alto. Se abstem
/// quando a fonte nao informa a data de abertura.
/// </summary>
public sealed class RegraEmpresaRecemNascida : IRegraAnomalia
{
    public string Codigo => "REG_KYB_003";

    public string Descricao => "Fornecedor recém-aberto com faturamento expressivo.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // A idade da empresa e' a regra inteira.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor | DadoExigido.AberturaDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        foreach (var despesa in contexto.Despesas)
        {
            if (contexto.FornecedorDe(despesa) is not { } fornecedor)
            {
                continue;
            }

            if (despesa.ValorLiquido < parametros.ValorMinimoParaEmpresaRecemNascida)
            {
                continue;
            }

            // Nulo quando a fonte nao informou a abertura: a regra se abstem.
            if (fornecedor.DiasDeAtividadeAte(despesa.DataDocumento) is not { } dias)
            {
                continue;
            }

            if (dias < 0 || dias >= parametros.DiasParaEmpresaRecemNascida)
            {
                continue;
            }

            yield return FlagAnomalia.Criar(
                despesa.Id,
                Codigo,
                Gravidade.Media,
                $"O fornecedor '{fornecedor.RazaoSocial}' tinha {dias} dias de "
                + $"atividade quando emitiu nota de "
                + $"{despesa.ValorLiquido.ToString("C2", CulturaBrasileira.Padrao)}.",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_KYB_004 — fornecedor optante pelo MEI recebeu, de um unico parlamentar, mais que
/// o teto anual de faturamento da categoria.
///
/// O teto vem de lei, nao de calibragem. E como o calculo considera apenas o que <b>este</b>
/// parlamentar pagou, ultrapassa-lo ja e' suficiente por si — o faturamento real do MEI so'
/// pode ser maior.
/// </summary>
public sealed class RegraMeiAcimaDoTeto : IRegraAnomalia
{
    public string Codigo => "REG_KYB_004";

    public string Descricao => "Fornecedor MEI recebeu acima do teto legal da categoria.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // A que mais sofre: 65% dos fornecedores da base real nao tem a opcao pelo MEI
    // informada, e o silencio dela nunca significou ausencia de achado.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor | DadoExigido.OpcaoPeloMei;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var porFornecedorEAno = contexto.Despesas
            .Where(despesa => contexto.FornecedorDe(despesa)?.EhMei == true)
            .GroupBy(despesa => (despesa.DocumentoFornecedor.Numero, despesa.Competencia.Ano));

        foreach (var grupo in porFornecedorEAno)
        {
            var total = grupo.Sum(despesa => despesa.ValorLiquido);

            if (total <= contexto.Parametros.TetoAnualDoMei)
            {
                continue;
            }

            var maior = grupo.MaxBy(despesa => despesa.ValorLiquido)!;
            var fornecedor = contexto.FornecedorDe(maior)!;

            yield return FlagAnomalia.Criar(
                maior.Id,
                Codigo,
                Gravidade.Alta,
                $"Inconsistência financeira: '{fornecedor.RazaoSocial}' é optante pelo MEI e "
                + $"recebeu {total.ToString("C2", CulturaBrasileira.Padrao)} apenas deste "
                + $"parlamentar em {grupo.Key.Ano}, acima do teto anual da categoria "
                + $"({contexto.Parametros.TetoAnualDoMei.ToString("C2", CulturaBrasileira.Padrao)}).",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_KYB_005 — as notas do fornecedor para este parlamentar tem numeracao inicial,
/// sugerindo que ele esta entre os primeiros clientes da empresa.
///
/// <b>So' considera documento fiscal.</b> A Etapa 03 mostrou que bilhete aereo traz numero
/// de 13 digitos e recibo nao segue serie sequencial do emitente — incluir esses documentos
/// produziria ruido justamente na regra mais delicada do conjunto.
/// </summary>
public sealed class RegraNotaFiscalInicial : IRegraAnomalia
{
    public string Codigo => "REG_KYB_005";

    public string Descricao => "Notas com numeração inicial, sugerindo cliente pioneiro.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // O numero da nota vem na propria despesa, mas a regra so' olha fornecedor que esta
    // no cadastro (FornecedorDe nao nulo) — e isso e' dado externo.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        var porFornecedor = contexto.Despesas
            .Where(despesa => despesa.EhDocumentoFiscal
                && !despesa.EhEstorno
                && contexto.FornecedorDe(despesa) is not null)
            .GroupBy(despesa => despesa.DocumentoFornecedor.Numero, StringComparer.Ordinal);

        foreach (var grupo in porFornecedor)
        {
            var iniciais = grupo
                .Select(despesa => (Despesa: despesa, Numero: NumeroDaNota(despesa)))
                .Where(item => item.Numero is > 0 and int numero
                    && numero <= parametros.NumeroDeNotaConsideradoInicial)
                .ToList();

            if (iniciais.Count < parametros.MinimoDeNotasIniciais)
            {
                continue;
            }

            var maisBaixa = iniciais.MinBy(item => item.Numero)!;
            var fornecedor = contexto.FornecedorDe(maisBaixa.Despesa)!;
            var numeros = string.Join(", ", iniciais.OrderBy(i => i.Numero).Select(i => i.Numero));

            yield return FlagAnomalia.Criar(
                maisBaixa.Despesa.Id,
                Codigo,
                Gravidade.Media,
                $"{iniciais.Count} notas fiscais de "
                + $"'{fornecedor.RazaoSocial}' para este parlamentar têm numeração inicial "
                + $"({numeros}), compatível com um dos primeiros clientes da empresa.",
                grauDeConfianca: 1.0m);
        }
    }

    /// <summary>
    /// Nulo quando o numero nao e' inteiro simples. Nota sem numero recebe um marcador
    /// textual na ingestao, justamente para nao ser lida como "numero muito baixo".
    /// </summary>
    private static int? NumeroDaNota(Despesa despesa) =>
        int.TryParse(despesa.NumeroDocumento, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : null;
}

/// <summary>Formatacao monetaria em pt-BR, independente do locale da maquina.</summary>
internal static class CulturaBrasileira
{
    /// <summary>
    /// Construida a mao porque a solution roda com <c>InvariantGlobalization</c>: pedir
    /// <c>CultureInfo.GetCultureInfo("pt-BR")</c> devolveria a cultura invariante e os
    /// valores sairiam como "$1,234.56" no texto exibido ao publico.
    /// </summary>
    public static NumberFormatInfo Padrao { get; } = new()
    {
        CurrencySymbol = "R$",
        CurrencyDecimalSeparator = ",",
        CurrencyGroupSeparator = ".",
        CurrencyPositivePattern = 2,
        CurrencyNegativePattern = 9,
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };
}
