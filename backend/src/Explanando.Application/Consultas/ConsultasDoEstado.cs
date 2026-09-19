using Explanando.Domain.Estado;

namespace Explanando.Application.Consultas;

/// <summary>Um nó do grafo, como a tela precisa dele.</summary>
public sealed record NoDoEstado
{
    public required int Codigo { get; init; }

    /// <summary>Quem manda nesta unidade na estrutura real. Nulo só na raiz.</summary>
    public int? CodigoPai { get; init; }

    /// <summary>
    /// O ancestral mais próximo que <b>também está no mapa</b>.
    ///
    /// <para>Existe porque o mapa mostra só entes, órgãos e entidades, e alguns órgãos
    /// penduram de uma unidade administrativa — medido: os três comandos militares estão
    /// entre o Ministério da Defesa e os órgãos abaixo deles. Sem este campo, oito órgãos
    /// virariam raízes soltas e sumiriam do desenho.</para>
    ///
    /// <para>É um campo separado, e não um <c>CodigoPai</c> reescrito, porque os dois
    /// significam coisas diferentes: um é a hierarquia real, o outro é a hierarquia
    /// <b>desta vista</b>. Sobrepor os dois faria a página de uma unidade mentir sobre a
    /// quem ela responde.</para>
    /// </summary>
    public int? CodigoPaiNoMapa { get; init; }
    public required string Nome { get; init; }
    public string? Sigla { get; init; }

    /// <summary>Por extenso, no vocabulário do leitor — nunca o número do enum.</summary>
    public required string Tipo { get; init; }
    public string? Poder { get; init; }
    public string? Natureza { get; init; }

    /// <summary>
    /// "Lei ou decreto" contra "ato interno".
    ///
    /// <para>Não é metadado: separa o órgão que precisa do Congresso para desaparecer do
    /// que some com uma assinatura. Nulo quando a fonte não informa.</para>
    /// </summary>
    public string? BaseLegal { get; init; }

    /// <summary>
    /// Quantas unidades pendem abaixo deste nó, em qualquer profundidade.
    ///
    /// <para>É o que dá escala ao desenho. Sem ele, o Ministério da Saúde e um conselho de
    /// três pessoas aparecem do mesmo tamanho, e o mapa mente sobre o peso de cada parte.</para>
    /// </summary>
    public required int UnidadesSubordinadas { get; init; }

    /// <summary>Falso quando a unidade deixou de constar na fonte. Ver ADR-0007.</summary>
    public required bool Ativa { get; init; }
}

/// <summary>Quantas unidades existem em cada corte.</summary>
public sealed record ContagemDoEstado
{
    public required string Rotulo { get; init; }
    public required int Unidades { get; init; }
}

/// <summary>Uma linha da história da estrutura.</summary>
public sealed record MudancaDoEstado
{
    public required int CodigoUnidade { get; init; }
    public required string Unidade { get; init; }

    /// <summary>A frase pronta para leitura. Ver <see cref="MudancaEstrutural.Descrever"/>.</summary>
    public required string Descricao { get; init; }

    public string? De { get; init; }
    public string? Para { get; init; }
    public required DateTimeOffset DetectadaEm { get; init; }
}

/// <summary>
/// O mapa do Estado no nível que se lê.
///
/// <para><b>Só os órgãos, entidades e entes vêm no mapa inicial</b>, e isso é decisão de
/// produto medida, não economia de banda: a base tem mais de cem mil unidades, e desenhar
/// todas produz uma nuvem cinza onde o Executivo inteiro some atrás dos conselhos. O
/// detalhe de cada órgão é buscado quando alguém o abre.</para>
/// </summary>
public sealed record MapaDoEstado
{
    public IReadOnlyList<NoDoEstado> Nos { get; init; } = [];

    /// <summary>Todas as unidades da base, incluindo as que não estão no mapa.</summary>
    public required int TotalDeUnidades { get; init; }

    public IReadOnlyList<ContagemDoEstado> PorPoder { get; init; } = [];
    public IReadOnlyList<ContagemDoEstado> PorTipo { get; init; } = [];
    public IReadOnlyList<ContagemDoEstado> PorBaseLegal { get; init; } = [];

    /// <summary>Quando a fonte foi consultada pela última vez.</summary>
    public DateTimeOffset? UltimaColeta { get; init; }

    /// <summary>
    /// Quantas coletas já existem.
    ///
    /// <para>A tela precisa saber: com uma só, não há história para mostrar, e a ausência
    /// de mudanças significa "ainda não comparamos", não "nada mudou". São coisas opostas,
    /// e confundi-las é exatamente o erro que o ADR-0009 proíbe com os zeros.</para>
    /// </summary>
    public required bool TemHistorico { get; init; }
}

/// <summary>Leituras do grafo do Estado.</summary>
public interface IConsultasDoEstado
{
    Task<MapaDoEstado> ObterMapaAsync(CancellationToken cancelamento = default);

    /// <summary>Tudo que pende de uma unidade. Vazio quando ela não existe ou é folha.</summary>
    Task<IReadOnlyList<NoDoEstado>> ObterSubarvoreAsync(
        int codigo,
        CancellationToken cancelamento = default);

    /// <summary>A unidade, ou nulo.</summary>
    Task<NoDoEstado?> ObterUnidadeAsync(int codigo, CancellationToken cancelamento = default);

    /// <summary>As mudanças mais recentes, da mais nova para a mais velha.</summary>
    Task<IReadOnlyList<MudancaDoEstado>> ListarMudancasAsync(
        int quantidade,
        CancellationToken cancelamento = default);
}
