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

    public static bool EhAlimentacaoOuHospedagem(string tipoDespesa) =>
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
                $"Alerta de compliance: despesa presencial de '{despesa.TipoDespesa}' no valor "
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
                $"Alerta de compliance: '{despesa.TipoDespesa}' de "
                + $"{despesa.ValorLiquido.ToString("C2", CulturaBrasileira.Padrao)} em "
                + $"{despesa.DataDocumento:dd/MM/yyyy}, durante o recesso parlamentar "
                + "previsto no art. 57 da Constituição.",
                grauDeConfianca: 1.0m);
        }
    }
}
