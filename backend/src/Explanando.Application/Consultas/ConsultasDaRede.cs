using Explanando.Domain.Rede;

namespace Explanando.Application.Consultas;

/// <summary>
/// Um parlamentar no mapa de fornecedores.
///
/// <para>Traz retrato, partido e UF porque o mapa é de <b>pessoas</b> — é a vista que
/// responde "quem contrata junto com quem", e um grafo de identificadores não responde
/// isso a ninguém.</para>
/// </summary>
public sealed record ParlamentarNaRede
{
    public required Guid Id { get; init; }
    public required string Nome { get; init; }
    public required string Partido { get; init; }
    public required string Uf { get; init; }

    /// <summary>Por extenso: <c>Deputado</c> ou <c>Senador</c>.</summary>
    public required string Casa { get; init; }

    /// <summary>A chave estável da casa, para o desenho decidir a forma do nó.</summary>
    public required string ChaveDaCasa { get; init; }

    public string? FotoUrl { get; init; }

    /// <summary>Quantos fornecedores deste mapa ele divide com alguém.</summary>
    public required int Ligacoes { get; init; }

    /// <summary>Quantos outros parlamentares ele alcança por esses fornecedores.</summary>
    public required int Vizinhos { get; init; }

    /// <summary>
    /// O que ele gastou <b>com os fornecedores deste mapa</b> — e não o gasto dele.
    ///
    /// <para>O nome do campo diz isso porque o número, sozinho, seria lido como a cota
    /// inteira. É uma fatia pequena dela, de propósito.</para>
    /// </summary>
    public required decimal ValorNoMapa { get; init; }
}

/// <summary>
/// Uma aresta: o fornecedor, e os parlamentares que ele atende.
///
/// <para>A aresta é o <b>fornecedor</b>, e não o par de pessoas. Um fornecedor que atende
/// quatro gabinetes é uma coisa só, com um CNPJ e um valor; transformá-lo nos seis pares
/// possíveis multiplicaria um fato por seis e daria ao leitor a impressão de seis
/// achados.</para>
/// </summary>
public sealed record LigacaoDaRede
{
    public required string Documento { get; init; }
    public required string Fornecedor { get; init; }

    /// <summary>Quem ele atende. Sempre de dois a cinco — ver <see cref="CriteriosDaRede"/>.</summary>
    public required IReadOnlyList<Guid> Parlamentares { get; init; }

    public required decimal Valor { get; init; }
    public required int Notas { get; init; }

    /// <summary>
    /// O CNAE principal declarado, <b>cru</b>. Nulo quando não conhecemos o CNPJ.
    ///
    /// <para>Cru de propósito: não temos tabela de descrição de CNAE, e escrever
    /// "publicidade" a partir de um prefixo seria inventar a leitura do código. A tela
    /// mostra o número rotulado como número.</para>
    /// </summary>
    public string? CnaePrincipal { get; init; }

    public string? Municipio { get; init; }
    public string? Uf { get; init; }

    /// <summary>
    /// A data de abertura, quando conhecida — o que permite ao leitor ver sozinho o caso
    /// que a <c>REG_NET_004</c> persegue: empresa nova já atendendo vários gabinetes.
    /// </summary>
    public DateOnly? AbertaEm { get; init; }
}

/// <summary>
/// Dois parlamentares que dividem <b>mais de um</b> fornecedor.
///
/// <para>É a aresta que o desenho usa, e não a do fornecedor. A razão está medida em
/// <see cref="CriteriosDaRede.FornecedoresParaParDenso"/>: com um fornecedor em comum
/// bastando, 454 dos 463 parlamentares caem num componente só e o grafo deixa de
/// distinguir qualquer coisa.</para>
/// </summary>
public sealed record ParDaRede
{
    public required Guid De { get; init; }
    public required Guid Para { get; init; }

    /// <summary>Quantos fornecedores os dois dividem. Sempre ≥ 2.</summary>
    public required int Fornecedores { get; init; }

    /// <summary>A soma dos fornecedores em comum — não o gasto de nenhum dos dois.</summary>
    public required decimal Valor { get; init; }

    /// <summary>
    /// Os dois são do mesmo estado?
    ///
    /// <para>Vai no contrato, e não é deduzido na tela, porque é <b>a</b> variável que
    /// explica o desenho: 91% dos pares densos são do mesmo estado. Um par de estados
    /// diferentes é o caso incomum, e o mapa precisa poder destacá-lo.</para>
    /// </summary>
    public required bool MesmaUf { get; init; }

    public required bool MesmoPartido { get; init; }
}

/// <summary>
/// O mapa inteiro, com as ressalvas que ele não pode ser lido sem.
/// </summary>
public sealed record MapaDaRede
{
    public required IReadOnlyList<ParlamentarNaRede> Parlamentares { get; init; }
    public required IReadOnlyList<LigacaoDaRede> Ligacoes { get; init; }

    /// <summary>
    /// Os pares densos — a aresta do desenho. Ver <see cref="ParDaRede"/>.
    /// </summary>
    public required IReadOnlyList<ParDaRede> Pares { get; init; }

    /// <summary>
    /// Quantos pares dividem <b>um</b> fornecedor só, e por isso ficam fora do desenho.
    ///
    /// <para>Vai para a tela: sem este número, o leitor não tem como saber que o grafo é
    /// um recorte, nem o tamanho do que ficou de fora.</para>
    /// </summary>
    public required int ParesComUmFornecedorSo { get; init; }

    /// <summary>Quantos fornecedores a base tem ao todo, para o corte ter escala.</summary>
    public required int FornecedoresNaBase { get; init; }

    /// <summary>Quantos atendem mais de um parlamentar — a maioria fica de fora.</summary>
    public required int FornecedoresCompartilhados { get; init; }

    /// <summary>
    /// O mais compartilhado da base, com a contagem. Vai para a tela: é a prova de
    /// que o corte existe, e não uma alegação nossa.
    /// </summary>
    public string? MaiorFornecedorCompartilhado { get; init; }

    public int ParlamentaresAtendidosPeloMaior { get; init; }

    /// <summary>
    /// A ressalva viaja <b>dentro do mapa</b>, e não como um texto solto na tela.
    ///
    /// <para>É deliberado: quem consumir esta API por fora recebe o aviso junto com os
    /// dados, e não tem como desenhar o grafo sem ter visto a frase.</para>
    /// </summary>
    public string Ressalva { get; } = CriteriosDaRede.Ressalva;

    /// <inheritdoc cref="CriteriosDaRede.PorQueEsteCorte"/>
    public string PorQueEsteCorte { get; } = CriteriosDaRede.PorQueEsteCorte;

    /// <inheritdoc cref="CriteriosDaRede.OQueADensidadeMostra"/>
    public string OQueADensidadeMostra { get; } = CriteriosDaRede.OQueADensidadeMostra;
}

/// <summary>
/// A vista de pessoas do mapa do Estado — o equivalente honesto ao <i>Power map</i> da
/// referência (<c>docs/referencias/civlab.md</c>).
///
/// <para><b>O que mudou em relação ao original, e por quê.</b> Lá a aresta é "citados nos
/// mesmos artigos", que mede a imprensa. Aqui é "dividem um fornecedor pouco
/// compartilhado e de valor alto", que é um fato da nossa base, com CNPJ, nota e
/// valor.</para>
/// </summary>
public interface IConsultasDaRede
{
    Task<MapaDaRede> ObterMapaAsync(CancellationToken cancelamento = default);

    /// <summary>As ligações de um parlamentar. Vazio quando ele não está no mapa.</summary>
    Task<IReadOnlyList<LigacaoDaRede>> ObterLigacoesAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);
}
