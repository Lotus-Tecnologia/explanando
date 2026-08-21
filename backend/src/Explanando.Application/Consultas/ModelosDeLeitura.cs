using Explanando.Domain.Auditoria;
using Explanando.Domain.Regras;

namespace Explanando.Application.Consultas;

/// <summary>
/// Como o ranking é ordenado.
///
/// <para>São duas ordenações porque são dois compradores com necessidades opostas
/// (<c>ADR-0009</c>), e porque a composição do índice mostrou que o total engana: na base
/// real, um parlamentar com 14 achados de gravidade alta aparece <b>abaixo</b> de outro com
/// zero, já que médias e baixas somam.</para>
///
/// <para>Um analista de <i>due diligence</i> quer os achados graves no topo; o leitor do
/// ranking público quer saber quem gastou mais. A escolha fica com quem consome, e não com
/// um palpite nosso sobre a preferência do mercado.</para>
/// </summary>
public enum OrdenacaoDoRanking
{
    /// <summary>Soma em reais, com estornos abatidos. O número do B2C.</summary>
    Volume = 0,

    /// <summary>Densidade de indicadores por 100 despesas. O número do B2B.</summary>
    Indice = 1,

    /// <summary>Densidade <b>só das flags de gravidade alta</b>. A fila de triagem.</summary>
    Gravidade = 2,
}

/// <summary>Uma linha do ranking.</summary>
public sealed record ItemDoRanking
{
    public required Guid Id { get; init; }
    public required int IdExterno { get; init; }
    public required string NomeParlamentar { get; init; }
    public required string SiglaPartido { get; init; }
    public required string Uf { get; init; }
    public string? FotoUrl { get; init; }

    /// <summary>O número do B2C. Sempre publicável — não depende de amostra.</summary>
    public decimal VolumeTotalGasto { get; init; }

    public int DespesasAvaliadas { get; init; }

    /// <summary>
    /// O número do B2B. <b>Nulo quando a amostra é insuficiente</b> — e nulo é diferente de
    /// zero: zero diria que a densidade foi medida e deu nada.
    /// </summary>
    public decimal? IndiceDeRisco { get; init; }

    /// <summary>Densidade só das altas. Nula pelo mesmo motivo do índice.</summary>
    public decimal? IndiceDeAltas { get; init; }

    public int FlagsAltas { get; init; }
    public int FlagsMedias { get; init; }
    public int FlagsBaixas { get; init; }

    /// <summary>
    /// Falso para 15 dos 513 na base real. Abaixo do piso do <c>ADR-0009</c> a densidade é
    /// ruído, e o parlamentar sai do ranking de risco — mas continua no de volume.
    /// </summary>
    public bool AmostraSuficiente { get; init; }
}

/// <summary>Uma página de resultados, com o que a borda precisa para paginar.</summary>
public sealed record Pagina<T>
{
    public required IReadOnlyList<T> Itens { get; init; }
    public required int Total { get; init; }
    public required int NumeroDaPagina { get; init; }
    public required int ItensPorPagina { get; init; }

    public int TotalDePaginas => ItensPorPagina == 0
        ? 0
        : (int)Math.Ceiling((double)Total / ItensPorPagina);
}

/// <summary>Um ponto da série mensal de gasto.</summary>
public sealed record MesDoParlamentar
{
    public required int Ano { get; init; }
    public required int Mes { get; init; }
    public decimal TotalLiquido { get; init; }
    public int QuantidadeDeDespesas { get; init; }

    /// <summary>
    /// Nulo quando a origem não informa a glosa — o Senado inteiro cai aqui. Ver
    /// <c>ADR-0012</c> §2: zero afirmaria que houve conferência e nada foi cortado.
    /// </summary>
    public decimal? TotalGlosa { get; init; }

    /// <summary>
    /// Falso depois da poda. A interface não pode oferecer "ver as notas deste mês" quando
    /// o detalhe já não existe.
    /// </summary>
    public bool DetalheDisponivel { get; init; }
}

/// <summary>
/// O que uma regra conseguiu avaliar, como a borda precisa ler.
///
/// <para><see cref="SilencioEhConclusivo"/> é o campo que o <c>ADR-0013</c> §2 obriga a
/// interface a consultar antes de desenhar: falso significa exibir a tag de <b>não
/// avaliado</b>, nunca a de aprovado.</para>
/// </summary>
public sealed record CoberturaDeRegra
{
    public required string Codigo { get; init; }
    public required SituacaoDaRegra Situacao { get; init; }
    public int DespesasComDado { get; init; }
    public int DespesasSemDado { get; init; }
    public int FlagsEmitidas { get; init; }
    public bool SilencioEhConclusivo { get; init; }

    /// <summary>Em uma frase, por que a regra não pôde avaliar. Nulo quando avaliou tudo.</summary>
    public string? MotivoDaAbstencao { get; init; }
}

/// <summary>O perfil completo, como a página o consome.</summary>
public sealed record PerfilDoParlamentar
{
    public required Guid Id { get; init; }
    public required int IdExterno { get; init; }
    public required string NomeParlamentar { get; init; }
    public required string NomeCivil { get; init; }
    public required string SiglaPartido { get; init; }
    public required string Uf { get; init; }
    public required string Casa { get; init; }
    public string? FotoUrl { get; init; }

    public decimal VolumeTotalGasto { get; init; }
    public int DespesasAvaliadas { get; init; }
    public decimal? IndiceDeRisco { get; init; }
    public decimal? IndiceDeAltas { get; init; }
    public bool AmostraSuficiente { get; init; }

    public int FlagsAltas { get; init; }
    public int FlagsMedias { get; init; }
    public int FlagsBaixas { get; init; }

    public DateTimeOffset? DataUltimaVarredura { get; init; }
    public int VersaoDoMotor { get; init; }

    public IReadOnlyList<MesDoParlamentar> SerieMensal { get; init; } = [];

    /// <summary>
    /// A cobertura, regra a regra. Vai <b>ao lado</b> dos achados, não em rodapé: publicar
    /// as flags sem ela deixaria o leitor supor que o silêncio significa aprovação.
    /// </summary>
    public IReadOnlyList<CoberturaDeRegra> Cobertura { get; init; } = [];

    /// <summary>
    /// Os tipos de despesa que este parlamentar de fato teve, para montar a barra de
    /// filtros.
    ///
    /// <para>Vem daqui, e não de uma lista fixa na interface, por dois motivos. O catálogo
    /// muda por Ato da Mesa e uma cópia no frontend envelheceria em silêncio; e uma opção
    /// de filtro que devolve lista vazia faz o leitor concluir que não houve gasto, quando
    /// o que houve foi opção mal montada.</para>
    /// </summary>
    public IReadOnlyList<string> TiposDeDespesa { get; init; } = [];

    /// <summary>Quantas regras produziram resultado interpretável.</summary>
    public int RegrasConclusivas => Cobertura.Count(regra => regra.SilencioEhConclusivo);

    public int RegrasSemDado => Cobertura.Count(regra => !regra.SilencioEhConclusivo);
}

/// <summary>Um achado, como a tela o exibe.</summary>
public sealed record FlagDaDespesa
{
    public required string CodigoRegra { get; init; }
    public required Gravidade Gravidade { get; init; }
    public required string Descricao { get; init; }
    public decimal GrauDeConfianca { get; init; }
    public int VersaoDoMotor { get; init; }
}

/// <summary>Uma despesa na listagem.</summary>
public sealed record ItemDeDespesa
{
    public required Guid Id { get; init; }
    public required int Ano { get; init; }
    public required int Mes { get; init; }
    public required string TipoDespesa { get; init; }
    public required DateTimeOffset DataDocumento { get; init; }
    public string? NomeFornecedor { get; init; }

    /// <summary>CNPJ inteiro; CPF sempre mascarado. Ver <see cref="Domain.Comum.CpfCnpj.Formatado"/>.</summary>
    public required string DocumentoFornecedor { get; init; }

    public decimal ValorLiquido { get; init; }
    public decimal ValorDocumento { get; init; }

    /// <summary>Nulo quando a origem não informa. A UI omite a quebra de valores.</summary>
    public decimal? ValorGlosa { get; init; }

    /// <summary>Falso para o Senado inteiro e para 30% das notas da Câmara.</summary>
    public bool TemDocumentoPublicado { get; init; }

    /// <summary>
    /// O endereço do comprovante, reconstruído. Nulo quando a origem não publicou —
    /// e nesse caso a interface exibe a tag do <c>ADR-0012</c> §1, não um link quebrado.
    /// </summary>
    public string? UrlDocumento { get; init; }

    public bool EhEstorno { get; init; }

    public IReadOnlyList<FlagDaDespesa> Flags { get; init; } = [];

    /// <summary>A gravidade mais alta entre as flags, para a interface pintar a linha.</summary>
    public Gravidade? MaiorGravidade => Flags.Count == 0 ? null : Flags.Max(f => f.Gravidade);
}

/// <summary>Quem gastou, o mínimo que a página de uma despesa precisa dizer.</summary>
public sealed record ParlamentarDaDespesa
{
    public required Guid Id { get; init; }
    public required string NomeParlamentar { get; init; }
    public required string SiglaPartido { get; init; }
    public required string Uf { get; init; }

    /// <summary>
    /// A casa. Não é enfeite: o <c>ADR-0012</c> §1 exige que a ausência de comprovante seja
    /// atribuída ao Senado <b>nominalmente</b>, e sem este campo a interface só consegue
    /// dizer "a fonte" — genérico, e genérico devolve a suspeita de falha para a plataforma.
    /// </summary>
    public required string Casa { get; init; }

    public string? FotoUrl { get; init; }
}

/// <summary>
/// Uma despesa com o parlamentar a que ela pertence.
///
/// <para>É o que o endereço público de uma despesa precisa. Sozinha, a despesa mostra um
/// valor e um código de regra pairando no ar — e o <c>ADR-0004</c> se apoia no contrário:
/// o achado é atribuído a uma regra nomeada, sobre um gasto de alguém, com o documento
/// oficial ao alcance do clique.</para>
/// </summary>
public sealed record DespesaDetalhada
{
    public required ItemDeDespesa Despesa { get; init; }
    public required ParlamentarDaDespesa Parlamentar { get; init; }
}

/// <summary>Filtros da listagem de despesas.</summary>
public sealed record FiltroDeDespesas
{
    public int? Ano { get; init; }
    public int? Mes { get; init; }
    public string? TipoDespesa { get; init; }

    /// <summary>Verdadeiro traz só as despesas com achado; falso, só as sem; nulo, todas.</summary>
    public bool? ComFlag { get; init; }

    public int Pagina { get; init; } = 1;
    public int ItensPorPagina { get; init; } = 50;
}
