namespace Explanando.Domain.Regras;

/// <summary>
/// O que cada regra procura, dito para quem não escreveu a regra.
///
/// <para><b>Por que não reaproveitar <see cref="IRegraAnomalia.Descricao"/>.</b> Aquela frase
/// nasceu para documentação e telemetria, e fala a língua de quem implementou: "notas com
/// numeração inicial, sugerindo cliente pioneiro", "gasto mensal próximo do teto da cota da
/// UF". Serve ao log e ao ADR; não serve ao cidadão que abriu a página do próprio deputado.
/// Manter as duas separadas custa uma tabela e um teste de deriva, e evita que melhorar o
/// texto da tela mexa no que a telemetria agrega.</para>
///
/// <para><b>Por que existe.</b> O bloco de cobertura do perfil usava o código como rótulo da
/// linha — onze <c>REG_*</c> em toda página de parlamentar. O código continua no JSON, porque
/// para o banco em due diligence e para a redação investigativa ele é identificador estável
/// de metodologia. Sai da página, fica no contrato.</para>
///
/// <para><b>Sob o ADR-0004.</b> Ao virar texto que o produto afirma sobre pessoa nomeada numa
/// página indexada, esta redação deixa de ser comentário e passa a valer a mesma regra das
/// descrições de flag: descreve o achado, não imputa conduta.</para>
/// </summary>
public static class CatalogoDeRegras
{
    private static readonly Dictionary<string, string> PorCodigo = new(StringComparer.Ordinal)
    {
        ["REG_KYB_001"] =
            "A atividade registrada da empresa não corresponde ao tipo do gasto — como uma "
            + "empresa de informática emitindo nota de combustível.",

        ["REG_KYB_002"] =
            "A nota foi emitida depois de a empresa passar a constar em situação irregular "
            + "na Receita Federal.",

        ["REG_KYB_003"] =
            "A empresa tinha pouco tempo de aberta quando recebeu um valor alto.",

        ["REG_KYB_004"] =
            "A empresa é MEI e recebeu acima do teto anual que essa categoria permite.",

        ["REG_KYB_005"] =
            "As notas têm numeração inicial, o que indica que o parlamentar esteve entre os "
            + "primeiros clientes da empresa.",

        ["REG_TX_001"] =
            "Várias notas do mesmo fornecedor no mesmo mês, em vez de uma só.",

        ["REG_TX_002"] =
            "O gasto do mês ficou perto do limite mensal da cota do estado.",

        ["REG_TX_003"] =
            "O mesmo valor, até os centavos, repetido por meses seguidos.",

        ["REG_TX_004"] =
            "O valor ficou muito acima da média que este parlamentar costuma gastar neste "
            + "tipo de despesa.",

        ["REG_GEO_002"] =
            "Gasto presencial feito fora do estado que o parlamentar representa e fora de "
            + "Brasília.",

        ["REG_GEO_003"] =
            "Gasto expressivo com alimentação ou hospedagem durante o recesso parlamentar.",

        ["REG_NET_004"] =
            "Uma mesma empresa, recém-aberta, concentrou pagamentos de vários parlamentares "
            + "do mesmo partido.",
    };

    /// <summary>Os códigos que o catálogo sabe descrever.</summary>
    public static IReadOnlyCollection<string> Codigos => PorCodigo.Keys;

    /// <summary>
    /// A redação de leitor, ou <c>null</c> quando o código é desconhecido.
    ///
    /// <para>Nulo de propósito: devolver uma frase genérica faria a tela exibir texto que
    /// ninguém redigiu. Quem desenha decide o que fazer com a ausência — e a decisão da
    /// borda é <b>cair para o código</b>, não omitir a linha. O bloco de cobertura existe
    /// para ser completo: esconder uma regra porque falta a redação dela diria que a regra
    /// não foi considerada, que é a afirmação mais cara do produto inteiro.</para>
    /// </summary>
    public static string? DescricaoDe(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo, out var descricao)
            ? descricao
            : null;
}
