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

    /// <summary>
    /// A casa, por extenso. Redundante com o filtro do pedido — e é assim de propósito.
    ///
    /// <para>Uma linha de ranking que circula fora do contexto do pedido, num card
    /// compartilhado ou numa exportação do painel Pro, precisa dizer sozinha de que casa
    /// ela fala. Deputado e senador têm cotas distintas, com valores próprios por UF, e
    /// misturá-los num mesmo eixo compara coisas diferentes.</para>
    /// </summary>
    public required string Casa { get; init; }

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

    /// <summary>
    /// O que a regra procura, na redação de leitor do <see cref="CatalogoDeRegras"/>.
    ///
    /// <para>Vem daqui, e não de uma tabela no frontend, pelo mesmo motivo que
    /// <see cref="MotivoDaAbstencao"/>: cópia na borda envelhece em silêncio, e cada
    /// consumidor acabaria redigindo a sua. <see cref="Codigo"/> continua no contrato — é
    /// identificador estável de metodologia para quem integra —, mas a página exibe esta
    /// frase.</para>
    ///
    /// <para>Nulo apenas se o código não estiver no catálogo, o que um teste de deriva
    /// impede. A borda trata o nulo não desenhando a linha.</para>
    /// </summary>
    public string? Descricao { get; init; }

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

    /// <summary>
    /// De onde vem o índice, regra a regra, da que mais pesa para a que menos pesa.
    ///
    /// <para><b>O índice é uma densidade, não uma soma de pontos</b>: peso dos achados
    /// dividido pelas despesas avaliadas. A forma honesta de decompô-lo é a fatia do peso —
    /// "62% vem desta regra" —, e não "+30 por concentração", que seria inventar uma
    /// aritmética que o motor não faz.</para>
    ///
    /// <para>Vem mesmo quando a amostra é insuficiente: os achados existem e a página os
    /// lista. O que falta nesse caso é o índice, e a borda explica a ausência.</para>
    /// </summary>
    public IReadOnlyList<ParcelaDoIndice> Composicao { get; init; } = [];

    /// <summary>
    /// Quantos fornecedores distintos, pelo documento — CNPJ ou CPF. É a medida de
    /// concentração mais simples que existe, e a que o comparador põe lado a lado.
    /// </summary>
    public int FornecedoresDistintos { get; init; }

    /// <summary>Quantas regras produziram resultado interpretável.</summary>
    public int RegrasConclusivas => Cobertura.Count(regra => regra.SilencioEhConclusivo);

    public int RegrasSemDado => Cobertura.Count(regra => !regra.SilencioEhConclusivo);
}

/// <summary>
/// A parte do índice que vem de uma regra.
///
/// <para><see cref="Fatia"/> vai sem arredondar: arredondar cada uma no servidor faz a soma
/// dar 99% ou 101%, e quem desenha arredonda o conjunto de uma vez só.</para>
/// </summary>
public sealed record ParcelaDoIndice
{
    public required string Codigo { get; init; }

    /// <summary>A redação de leitor do <see cref="CatalogoDeRegras"/>.</summary>
    public string? Descricao { get; init; }

    /// <summary>Quantos achados desta regra.</summary>
    public int Quantidade { get; init; }

    /// <summary>Soma de gravidade × grau de confiança dos achados desta regra.</summary>
    public decimal Peso { get; init; }

    /// <summary>A parte do peso total que vem daqui, de 0 a 1.</summary>
    public decimal Fatia { get; init; }

    /// <summary>
    /// Quanto desta regra entra no índice, na mesma unidade dele: pontos por 100 despesas.
    /// A soma das parcelas é o índice. Nulo quando o parlamentar está abaixo do piso
    /// amostral e não tem índice publicado (ADR-0009).
    /// </summary>
    public decimal? PontosNoIndice { get; init; }

    public int Altas { get; init; }

    public int Medias { get; init; }

    public int Baixas { get; init; }
}

/// <summary>
/// A resposta oficial do gabinete a uma despesa, publicada sem edição.
///
/// <para>Só as publicadas saem: a retirada pelo próprio gabinete some da página. O e-mail
/// que comprovou a autoria fica no banco, para auditoria, e não é publicado.</para>
/// </summary>
public sealed record RespostaDoGabinete
{
    public required string Texto { get; init; }

    public string? UrlDoAnexo { get; init; }

    public required DateTimeOffset EnviadaEm { get; init; }
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

    /// <summary>
    /// O que o gabinete respondeu sobre esta despesa. Vai junto do achado, e não numa
    /// página à parte: o contraditório só funciona se quem lê o achado lê a resposta.
    ///
    /// <para>Ligado pela despesa, e não pela flag: as flags são recriadas a cada varredura,
    /// e a ligação por flag se perderia na primeira passada seguinte.</para>
    /// </summary>
    public IReadOnlyList<RespostaDoGabinete> Respostas { get; init; } = [];

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

/// <summary>
/// Uma despesa com achado, como o feed de alertas a publica.
///
/// <para><b>A data do alerta é <see cref="RegistradaEm"/></b> — quando a despesa entrou na
/// base —, e não a da flag. A varredura recria as flags a cada passada; datar o alerta por
/// elas faria toda despesa antiga "virar notícia" de novo a cada ciclo. Datado pela entrada,
/// o achado de uma regra alterada sobre despesa antiga fica com a data antiga, lá embaixo, e
/// o que sobe é despesa nova — a única que é notícia sobre o parlamentar.</para>
/// </summary>
public sealed record AchadoRecente
{
    public required Guid DespesaId { get; init; }

    /// <summary>Quando a despesa entrou na base, lido do UUID v7. Nulo se o id não for v7.</summary>
    public DateTimeOffset? RegistradaEm { get; init; }

    public required ParlamentarDaDespesa Parlamentar { get; init; }

    public required string TipoDespesa { get; init; }

    public string? NomeFornecedor { get; init; }

    public required string DocumentoFornecedor { get; init; }

    public decimal ValorLiquido { get; init; }

    public required DateTimeOffset DataDocumento { get; init; }

    public required int Ano { get; init; }

    public required int Mes { get; init; }

    /// <summary>Os achados da despesa, do mais relevante para o menos.</summary>
    public IReadOnlyList<RegraDoAchado> Regras { get; init; } = [];
}

/// <summary>Um achado dentro do alerta: a regra dita para leitor, e o detalhe do caso.</summary>
public sealed record RegraDoAchado
{
    public required string Codigo { get; init; }

    public required Gravidade Gravidade { get; init; }

    /// <summary>O que a regra procura, na redação do <see cref="CatalogoDeRegras"/>.</summary>
    public string? DescricaoDaRegra { get; init; }

    /// <summary>A frase do achado sobre esta despesa, como a varredura a escreveu.</summary>
    public required string Detalhe { get; init; }
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
