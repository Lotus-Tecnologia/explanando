using Explanando.Domain.Auditoria;

namespace Explanando.Domain.Regras;

/// <summary>
/// Tipos de despesa que exigem presenca fisica no local.
///
/// A distincao importa: comprar software de uma empresa de outro estado e' rotina; abastecer
/// um veiculo la, nao. Sem esse recorte, REG_GEO_002 marcaria praticamente todo parlamentar
/// que contrata servico digital fora do estado.
/// </summary>
internal static class TiposPresenciais
{
    private static readonly string[] Marcadores =
    [
        "COMBUST",
        "MANUTEN",
        "LOCA",
        "ALIMENTA",
        "HOSPEDAGEM",
        "LOCOMO",
        "TÁXI",
        "TAXI",
    ];

    public static bool Inclui(string tipoDespesa) =>
        Array.Exists(
            Marcadores,
            marcador => tipoDespesa.Contains(marcador, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] MarcadoresDeEstadia = ["ALIMENTA", "HOSPEDAGEM", "LOCOMO"];

    /// <summary>
    /// O que torna a rubrica <b>ambígua</b> para efeito de estadia.
    ///
    /// <para>O casamento por marcador funciona enquanto a rubrica nomeia uma coisa só. A
    /// CEAPS do Senado tem uma que junta cinco: <i>"Locomoção, hospedagem, alimentação,
    /// combustíveis e lubrificantes"</i> — ela contém os três marcadores de estadia <b>e</b>
    /// combustível, e sozinha responde por 43% das despesas do Senado.</para>
    ///
    /// <para>Sem esta ressalva, um tanque de gasolina no recesso virava "alimentação ou
    /// hospedagem expressiva durante o recesso". Não é hipótese: <b>480 das 480</b> flags de
    /// <c>REG_GEO_003</c> no Senado saíam dessa rubrica, uma taxa 22 vezes a da Câmara.</para>
    ///
    /// <para>A rubrica agrupada da Câmara — <i>"LOCOMOÇÃO, ALIMENTAÇÃO E HOSPEDAGEM"</i> —
    /// <b>não</b> traz combustível, então continua conclusiva e nada muda para ela. Esta é a
    /// diferença exata entre as duas casas.</para>
    ///
    /// <para>Abster é o ADR-0007: a rubrica não permite afirmar que <i>esta</i> despesa foi
    /// estadia, e afirmar assim mesmo seria inventar a natureza do gasto.</para>
    /// </summary>
    private static readonly string[] MarcadoresQueTornamAmbiguo = ["COMBUST"];

    public static bool EhAlimentacaoOuHospedagem(string tipoDespesa) =>
        TemEstadia(tipoDespesa) && !EhRubricaAmbigua(tipoDespesa);

    /// <summary>
    /// A rubrica fala de estadia, mas junta com o que não é estadia — a do Senado. Não é
    /// ausência de achado: é despesa que a regra não tem como examinar, e a cobertura precisa
    /// dizer isso (DadoExigido.NaturezaDaDespesa).
    /// </summary>
    public static bool EhRubricaAmbigua(string tipoDespesa) =>
        TemEstadia(tipoDespesa)
        && Array.Exists(
            MarcadoresQueTornamAmbiguo,
            marcador => tipoDespesa.Contains(marcador, StringComparison.OrdinalIgnoreCase));

    private static bool TemEstadia(string tipoDespesa) =>
        Array.Exists(
            MarcadoresDeEstadia,
            marcador => tipoDespesa.Contains(marcador, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// REG_GEO_002 — gasto presencial num estado que nao e' a base eleitoral do parlamentar
/// nem o Distrito Federal.
///
/// Brasilia esta sempre excluida: o parlamentar trabalha la. A UF vem do <b>fornecedor</b>,
/// porque a despesa em si nao traz local — descoberta da Etapa 01.
/// </summary>
public sealed class RegraGastoForaDaBaseEleitoral : IRegraAnomalia
{
    public string Codigo => "REG_GEO_002";

    public string Descricao => "Gasto presencial fora da base eleitoral e de Brasília.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Sem UF do fornecedor nao ha distancia a medir.
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor | DadoExigido.EnderecoDoFornecedor;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var ufDoParlamentar = contexto.Parlamentar.Uf;

        foreach (var despesa in contexto.Despesas)
        {
            if (despesa.ValorLiquido < contexto.Parametros.ValorMinimoParaGastoForaDaBase
                || !TiposPresenciais.Inclui(despesa.TipoDespesa))
            {
                continue;
            }

            if (contexto.FornecedorDe(despesa)?.Endereco.Uf is not { Length: 2 } ufDoFornecedor)
            {
                continue;
            }

            if (string.Equals(ufDoFornecedor, ufDoParlamentar, StringComparison.Ordinal)
                || string.Equals(ufDoFornecedor, "DF", StringComparison.Ordinal))
            {
                continue;
            }

            yield return FlagAnomalia.Criar(
                despesa.Id,
                Codigo,
                Gravidade.Baixa,
                $"Despesa presencial de '{despesa.TipoDespesa}' no valor "
                + $"de {despesa.ValorLiquido.ToString("C2", CulturaBrasileira.Padrao)} paga a "
                + $"fornecedor sediado em {ufDoFornecedor}, fora da base eleitoral "
                + $"({ufDoParlamentar}) e de Brasília.",
                grauDeConfianca: 1.0m);
        }
    }
}

/// <summary>
/// REG_GEO_003 — alimentacao ou hospedagem de valor expressivo durante o recesso parlamentar.
///
/// Gravidade baixa de proposito: convocacao extraordinaria e agenda de base fazem o
/// parlamentar trabalhar em recesso legitimamente. O recesso torna o gasto <b>atipico</b>,
/// nao indevido.
/// </summary>
public sealed class RegraGastoEmRecesso : IRegraAnomalia
{
    public string Codigo => "REG_GEO_003";

    public string Descricao => "Alimentação ou hospedagem expressiva durante o recesso.";

    /// <summary>Ver IRegraAnomalia.Exige.</summary>
    // Calendario legislativo e data da nota — e saber se a despesa foi estadia, que a
    // rubrica agrupada do Senado nao diz (auditoria de 2026-09-25).
    public DadoExigido Exige =>
        DadoExigido.NaturezaDaDespesa;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (var despesa in contexto.Despesas)
        {
            if (despesa.ValorLiquido < contexto.Parametros.ValorMinimoParaGastoEmRecesso
                || !TiposPresenciais.EhAlimentacaoOuHospedagem(despesa.TipoDespesa)
                || !CalendarioLegislativo.EmRecesso(despesa.DataDocumento))
            {
                continue;
            }

            yield return FlagAnomalia.Criar(
                despesa.Id,
                Codigo,
                Gravidade.Baixa,
                $"Despesa de '{despesa.TipoDespesa}' de "
                + $"{despesa.ValorLiquido.ToString("C2", CulturaBrasileira.Padrao)} em "
                + $"{despesa.DataDocumento:dd/MM/yyyy}, durante o recesso parlamentar "
                + "previsto no art. 57 da Constituição.",
                grauDeConfianca: 1.0m);
        }
    }
}
