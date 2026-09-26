using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Regras;

/// <summary>
/// REG_TX_001 — muitas notas pequenas do mesmo fornecedor no mesmo mes, somando um valor
/// que, de uma vez so', teria outro tratamento.
/// </summary>
public sealed class RegraFracionamento : IRegraAnomalia
{
    public string Codigo => "REG_TX_001";

    public string Descricao => "Múltiplas notas do mesmo fornecedor no mesmo mês.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Compara despesas entre si, agrupadas por fornecedor: despesa sem documento do
    // fornecedor fica de fora do agrupamento, e a cobertura precisa dizer isso.
    public DadoExigido Exige =>
        DadoExigido.DocumentoDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        // Estorno fica de fora: a regra conta NOTAS emitidas, e uma devolucao nao e'
        // uma compra fatiada. Incluir estornos inflaria a contagem sem gasto correspondente.
        //
        // Passagem tambem (decisao de 26/09/2026, versao 6 do motor): a agencia fatura bilhete
        // a bilhete, e dez notas da mesma agencia no mes e' o funcionamento normal da
        // contratacao. No Senado eram 949 das 1.474 flags desta regra. Na Camara a passagem
        // quase nunca chegava aqui, porque vem sem documento do fornecedor.
        var grupos = contexto.Despesas
            .Where(despesa => !despesa.DocumentoFornecedor.EhVazio
                && !despesa.EhEstorno
                && !EhPassagem(despesa.TipoDespesa))
            .GroupBy(despesa => (despesa.Competencia, despesa.DocumentoFornecedor.Numero));

        foreach (var grupo in grupos)
        {
            var total = grupo.Sum(despesa => despesa.ValorLiquido);

            if (grupo.Count() < parametros.MinimoDeNotasParaFracionamento
                || total < parametros.SomaMinimaParaFracionamento)
            {
                continue;
            }

            var maior = grupo.MaxBy(despesa => despesa.ValorLiquido)!;

            yield return FlagAnomalia.Criar(
                maior.Id,
                Codigo,
                Gravidade.Media,
                $"Atipicidade de volume financeiro: {grupo.Count()} notas de "
                + $"'{maior.NomeFornecedor}' em {grupo.Key.Competencia}, somando "
                + $"{total.ToString("C2", CulturaBrasileira.Padrao)}.",
                grauDeConfianca: 1.0m);
        }
    }

    private static bool EhPassagem(string tipoDespesa) =>
        tipoDespesa.Contains("PASSAGE", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// REG_TX_002 — o gasto do mes encosta no teto da cota da UF do parlamentar.
///
/// Se abstem quando o periodo nao esta coberto pela tabela de limites: as despesas de 2023
/// a 2025 ainda nao tem teto configurado, e compara-las ao teto de 2026 seria inventar
/// referencia.
/// </summary>
public sealed class RegraGastoNoTetoDaCota : IRegraAnomalia
{
    public string Codigo => "REG_TX_002";

    public string Descricao => "Gasto mensal próximo do teto da cota da UF.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // So' 17% das despesas da base real caem num periodo com teto configurado. A tabela
    // da CEAP cobre 2026 em diante; 2023 a 2025 ainda nao.
    public DadoExigido Exige =>
        DadoExigido.TetoDaCota;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (var mes in contexto.PorCompetencia())
        {
            var referencia = new DateOnly(mes.Key.Ano, mes.Key.Mes, 1);

            if (contexto.LimiteVigenteEm(referencia) is not { } limite)
            {
                continue;
            }

            var total = mes.Sum(despesa => despesa.ValorLiquido);
            var gatilho = limite.ValorMensal * contexto.Parametros.FracaoDoTetoParaAlerta;

            if (total < gatilho)
            {
                continue;
            }

            var maior = mes.MaxBy(despesa => despesa.ValorLiquido)!;
            var percentual = total / limite.ValorMensal * 100m;

            yield return FlagAnomalia.Criar(
                maior.Id,
                Codigo,
                Gravidade.Baixa,
                $"Atipicidade de volume financeiro: o gasto de {mes.Key} somou "
                + $"{total.ToString("C2", CulturaBrasileira.Padrao)}, "
                + $"{percentual:F1}% do teto mensal da cota de {limite.Uf} "
                + $"({limite.ValorMensal.ToString("C2", CulturaBrasileira.Padrao)}).",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_TX_003 — mesmo fornecedor, mesmo valor ao centavo, meses seguidos.
///
/// Valor repetido e' comum em assinatura e mensalidade; o piso de valor existe para que a
/// regra nao marque a conta de telefone de todo mundo.
/// </summary>
public sealed class RegraPadraoMensalIdentico : IRegraAnomalia
{
    public string Codigo => "REG_TX_003";

    public string Descricao => "Valor idêntico ao centavo por meses consecutivos.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Serie historica do proprio parlamentar, por fornecedor — sem o documento do
    // fornecedor nao ha serie a montar.
    public DadoExigido Exige =>
        DadoExigido.DocumentoDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        var grupos = contexto.Despesas
            .Where(despesa => !despesa.DocumentoFornecedor.EhVazio
                && despesa.ValorLiquido >= parametros.ValorMinimoParaPadraoMensal)
            .GroupBy(despesa => (despesa.DocumentoFornecedor.Numero, despesa.ValorLiquido));

        foreach (var grupo in grupos)
        {
            var meses = grupo
                .Select(despesa => despesa.Competencia)
                .Distinct()
                .OrderBy(competencia => competencia)
                .ToList();

            var sequencia = MaiorSequenciaConsecutiva(meses);

            if (sequencia < parametros.MesesConsecutivosIdenticos)
            {
                continue;
            }

            var maisRecente = grupo.MaxBy(despesa => despesa.Competencia)!;

            yield return FlagAnomalia.Criar(
                maisRecente.Id,
                Codigo,
                Gravidade.Baixa,
                $"Atipicidade de volume financeiro: pagamentos de exatamente "
                + $"{grupo.Key.ValorLiquido.ToString("C2", CulturaBrasileira.Padrao)} a "
                + $"'{maisRecente.NomeFornecedor}' em {sequencia} meses consecutivos, "
                + "sem variação de centavos.",
                grauDeConfianca: 1.0m);
        }
    }

    private static int MaiorSequenciaConsecutiva(List<Competencia> meses)
    {
        if (meses.Count == 0)
        {
            return 0;
        }

        var maior = 1;
        var atual = 1;

        for (var i = 1; i < meses.Count; i++)
        {
            atual = meses[i].MesesDesde(meses[i - 1]) == 1 ? atual + 1 : 1;
            maior = Math.Max(maior, atual);
        }

        return maior;
    }
}

/// <summary>
/// REG_TX_004 — o gasto de um tipo de despesa salta muito acima da media dos meses
/// anteriores.
///
/// Exige historico minimo: media de um mes so' nao e' media. E tem piso absoluto, porque
/// triplicar cinquenta reais nao e' pico, e' ruido.
/// </summary>
public sealed class RegraPicoDeConsumo : IRegraAnomalia
{
    public string Codigo => "REG_TX_004";

    public string Descricao => "Gasto de um tipo de despesa muito acima da média histórica.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Serie historica do proprio parlamentar.
    public DadoExigido Exige =>
        DadoExigido.Nenhum;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        var porTipo = contexto.Despesas.GroupBy(
            despesa => despesa.TipoDespesa,
            StringComparer.Ordinal);

        foreach (var tipo in porTipo)
        {
            var serie = tipo
                .GroupBy(despesa => despesa.Competencia)
                .Select(mes => (
                    Competencia: mes.Key,
                    Total: mes.Sum(despesa => despesa.ValorLiquido),
                    Maior: mes.MaxBy(despesa => despesa.ValorLiquido)!))
                .OrderBy(ponto => ponto.Competencia)
                .ToList();

            for (var i = parametros.MinimoDeMesesDeHistorico; i < serie.Count; i++)
            {
                var atual = serie[i];

                if (atual.Total < parametros.ValorMinimoParaPico)
                {
                    continue;
                }

                var anteriores = serie.Take(i).ToList();
                var media = anteriores.Average(ponto => ponto.Total);

                if (media <= 0m || atual.Total < media * parametros.FatorDePico)
                {
                    continue;
                }

                yield return FlagAnomalia.Criar(
                    atual.Maior.Id,
                    Codigo,
                    Gravidade.Media,
                    $"Atipicidade de volume financeiro: o gasto com '{tipo.Key}' em "
                    + $"{atual.Competencia} foi {atual.Total.ToString("C2", CulturaBrasileira.Padrao)}, "
                    + $"contra média de {media.ToString("C2", CulturaBrasileira.Padrao)} nos "
                    + $"{anteriores.Count} meses anteriores.",
                    grauDeConfianca: 1.0m);
            }
        }
    }
}
