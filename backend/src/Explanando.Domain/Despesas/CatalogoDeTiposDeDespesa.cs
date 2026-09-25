using System.Collections.Frozen;

namespace Explanando.Domain.Despesas;

/// <summary>
/// Códigos estáveis para os tipos de despesa da CEAP.
///
/// <para>O nome do tipo vinha repetido em cada linha de <c>despesas</c>: 34,6 bytes por
/// registro, ~25 MB projetados para 513 deputados, para guardar dezesseis frases seiscentas
/// mil vezes. Aqui ele vira um <c>smallint</c>.</para>
///
/// <para><b>Os códigos são permanentes.</b> Estão gravados no banco; renumerar trocaria o
/// tipo de todas as despesas já ingeridas. Entrada nova vai sempre no fim.</para>
///
/// <para><b>Tipo desconhecido não quebra nada</b> — e essa é a diferença entre isto e um
/// <c>enum</c>. A lista da CEAP é definida por Ato da Mesa e muda; um catálogo que recusasse
/// o que não conhece rejeitaria despesas no meio de uma coleta de doze horas, e cada
/// rejeição seria gasto público que a página deixa de mostrar. O que não está aqui é
/// guardado com o nome por extenso, exatamente como hoje: custa o mesmo que custava e não
/// perde nada.</para>
///
/// <para>Os dezesseis primeiros são da CEAP da Câmara, vistos em 5.843 despesas reais, com
/// a grafia exata que a API entrega — inclusive o ponto final de uns e a vírgula deslocada de
/// outro. A tabela curada de CNAE conhece outros oito tipos da Câmara, mas só na forma
/// normalizada, sem acento; reconstruir a grafia original deles seria invenção, então esperam
/// aparecer numa coleta.</para>
///
/// <para>Do dezessete em diante vem a CEAPS do Senado: <b>sete</b> categorias, verificadas em
/// cinco anos de dado (2019, 2023, 2024, 2025 e 2026) sem uma única mudança de esquema. As
/// duas casas dividem o mesmo catálogo porque o código é só um identificador — separá-las
/// obrigaria toda leitura a descobrir a casa antes de resolver o nome, e a despesa não carrega
/// a casa: ela está no parlamentar.</para>
/// </summary>
public static class CatalogoDeTiposDeDespesa
{
    /// <summary>Código de quem não está no catálogo. O nome fica guardado por extenso.</summary>
    public const short NaoCatalogado = 0;

    /// <summary>
    /// Índice = código. A posição zero é o buraco reservado a <see cref="NaoCatalogado"/>.
    /// <b>Nunca reordenar</b>: os índices estão persistidos.
    /// </summary>
    private static readonly string[] PorCodigo =
    [
        string.Empty,
        "COMBUSTÍVEIS E LUBRIFICANTES.",
        "PASSAGEM AÉREA - SIGEPA",
        "SERVIÇO DE TÁXI, PEDÁGIO E ESTACIONAMENTO",
        "MANUTENÇÃO DE ESCRITÓRIO DE APOIO À ATIVIDADE PARLAMENTAR",
        "DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.",
        "FORNECIMENTO DE ALIMENTAÇÃO DO PARLAMENTAR",
        "LOCAÇÃO OU FRETAMENTO DE VEÍCULOS AUTOMOTORES",
        "TELEFONIA",
        "HOSPEDAGEM ,EXCETO DO PARLAMENTAR NO DISTRITO FEDERAL.",
        "ASSINATURA DE PUBLICAÇÕES",
        "PASSAGEM AÉREA - RPA",
        "LOCAÇÃO OU FRETAMENTO DE AERONAVES",
        "PASSAGEM AÉREA - REEMBOLSO",
        "PASSAGENS TERRESTRES, MARÍTIMAS OU FLUVIAIS",
        "AQUISIÇÃO DE TOKENS E CERTIFICADOS DIGITAIS",
        "SERVIÇOS POSTAIS",

        // --- CEAPS, do Senado (codigos 17 em diante) ---
        //
        // Entram na MESMA lista de proposito. O codigo e' so' um identificador; separar em
        // dois catalogos obrigaria toda leitura a saber de que casa a despesa veio antes de
        // resolver o nome, e Despesa nao carrega a casa — ela esta no parlamentar.
        //
        // Sao sete, verificados em cinco anos (2019, 2023, 2024, 2025, 2026): o esquema nao
        // mudou nenhuma vez. A grafia esta como a origem entrega, exceto o espaco final de
        // "...equipamentos. ", que o Trim de Despesa.Criar absorve antes da busca.
        "Locomoção, hospedagem, alimentação, combustíveis e lubrificantes",
        "Aluguel de imóveis para escritório político, compreendendo despesas concernentes a eles.",
        "Passagens aéreas, aquáticas e terrestres nacionais",
        "Contratação de consultorias, assessorias, pesquisas, trabalhos técnicos e outros serviços de apoio ao exercício do mandato parlamentar",
        "Divulgação da atividade parlamentar",
        "Aquisição de material de consumo para uso no escritório político, inclusive aquisição ou locação de software, despesas postais, aquisição de publicações, locação de móveis e de equipamentos.",
        "Serviços de Segurança Privada",

        // --- CEAP da Camara, os quatro que faltavam (codigos 24 em diante) ---
        //
        // A nota da classe dizia que oito tipos da Camara eram conhecidos apenas na forma
        // NORMALIZADA, pela tabela de CNAE, e que reconstruir a grafia original deles seria
        // invencao. A coleta completa de 2026-08-21 trouxe QUATRO com a grafia exata da
        // origem — inclusive os pontos finais, que nao teriam sido adivinhados. Ver a
        // Etapa 04c §3.5, que registra as ocorrencias de cada um.
        //
        // Os outros quatro continuam esperando aparecer. Ate' la', seguem gravados por
        // extenso em TipoDespesaNaoCatalogado, que e' o comportamento correto e nao uma
        // falha.
        "SERVIÇO DE SEGURANÇA PRESTADO POR EMPRESA ESPECIALIZADA.",
        "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.",
        "PARTICIPAÇÃO EM CURSO, PALESTRA OU EVENTO SIMILAR",
        "LOCAÇÃO OU FRETAMENTO DE EMBARCAÇÕES",
    ];

    private static readonly FrozenDictionary<string, short> PorNome =
        PorCodigo
            .Select((nome, indice) => (nome, codigo: (short)indice))
            .Where(par => par.codigo != NaoCatalogado)
            .ToFrozenDictionary(par => par.nome, par => par.codigo, StringComparer.Ordinal);

    /// <summary>Quantos tipos o catálogo conhece, sem contar o buraco do código zero.</summary>
    public static int Quantidade => PorCodigo.Length - 1;

    /// <summary>Os nomes catalogados, para inspeção e teste.</summary>
    public static IReadOnlyList<string> Nomes => [.. PorCodigo.Skip(1)];

    /// <summary>
    /// O código do tipo, ou <see cref="NaoCatalogado"/> quando ele não está na lista.
    /// Nunca lança: ver a nota da classe.
    /// </summary>
    public static short CodigoDe(string? nome) =>
        nome is not null && PorNome.TryGetValue(nome, out var codigo) ? codigo : NaoCatalogado;

    /// <summary>
    /// O nome do tipo, ou <c>null</c> para <see cref="NaoCatalogado"/> e para código fora
    /// da faixa — que só existiria se alguém tivesse removido uma entrada da lista.
    /// </summary>
    public static string? NomeDe(short codigo) =>
        codigo > NaoCatalogado && codigo < PorCodigo.Length ? PorCodigo[codigo] : null;
}
