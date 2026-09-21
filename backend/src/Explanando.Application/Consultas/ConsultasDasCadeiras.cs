using Explanando.Domain.Cadeiras;

namespace Explanando.Application.Consultas;

/// <summary>Uma cadeira, como o mapa de poder precisa dela.</summary>
public sealed record CadeiraDoMapa
{
    public required string Cargo { get; init; }

    /// <summary>Nulo é cadeira vaga — ver <see cref="Vaga"/>.</summary>
    public string? Ocupante { get; init; }

    public string? Orgao { get; init; }
    public string? Partido { get; init; }
    public string? Uf { get; init; }
    public string? FotoUrl { get; init; }

    /// <summary>O nó do grafo, quando conhecido. É o que liga as duas vistas.</summary>
    public int? CodigoSiorg { get; init; }

    /// <summary>A URL de onde isto foi lido. Vai para a tela, não só para o log.</summary>
    public required string Fonte { get; init; }

    public required DateTimeOffset VerificadoEm { get; init; }

    public required bool Vaga { get; init; }

    /// <summary>
    /// Foi transcrita à mão?
    ///
    /// <para>O leitor tem direito de saber se está olhando uma linha que uma API atualiza
    /// sozinha ou uma que alguém copiou de um portal e pode ter envelhecido.</para>
    /// </summary>
    public required bool Curada { get; init; }
}

/// <summary>Uma faixa horizontal do mapa de poder.</summary>
public sealed record CamadaDoMapa
{
    /// <summary>A chave estável: <c>chefia</c>, <c>cupula</c>, <c>comando</c>…</summary>
    public required string Chave { get; init; }

    /// <summary>O nome no vocabulário deste poder. "Ministros de Estado", "Mesa Diretora".</summary>
    public required string Rotulo { get; init; }

    /// <summary>De cima para baixo no desenho. 0 é a chefia.</summary>
    public required int Ordem { get; init; }

    public required IReadOnlyList<CadeiraDoMapa> Cadeiras { get; init; }
}

/// <summary>
/// O mapa de poder de um poder.
///
/// <para><b>Os quatro poderes vêm sempre, inclusive os vazios.</b> Omitir o Judiciário
/// porque não temos os ministros faria o mapa parecer que o Judiciário não tem comando;
/// mandá-lo vazio com <see cref="PorQueFalta"/> preenchido diz a verdade, que é "existe e
/// não conseguimos".</para>
/// </summary>
public sealed record MapaDePoder
{
    public required string ChaveDoPoder { get; init; }

    /// <summary>Por extenso: "Executivo", "Legislativo"…</summary>
    public required string Poder { get; init; }

    public required IReadOnlyList<CamadaDoMapa> Camadas { get; init; }

    public required int TotalDeCadeiras { get; init; }

    /// <summary>Quantas cadeiras estão vagas. Zero é um fato; nulo não existe aqui.</summary>
    public required int Vagas { get; init; }

    /// <summary>
    /// A verificação mais <b>antiga</b> do conjunto, e não a mais nova.
    ///
    /// <para>É a que importa: o leitor precisa saber o pior caso da tela que está vendo,
    /// não o melhor. Nulo quando não há cadeira nenhuma.</para>
    /// </summary>
    public DateTimeOffset? VerificadoDesde { get; init; }

    /// <summary>Nulo quando há cadeiras. Preenchido explica a ausência.</summary>
    public string? PorQueFalta { get; init; }
}

/// <summary>
/// O mapa de poder inteiro — os quatro poderes de uma vez.
///
/// <para><b>Tudo numa resposta só, de propósito.</b> São ~100 cadeiras: cabem numa
/// chamada, e é isso que permite ao desenho trocar de poder no clique, sem ida ao
/// servidor. A referência faz o mesmo, e é o que dá a sensação de que o mapa é um objeto
/// e não uma sequência de páginas.</para>
/// </summary>
public sealed record MapaDePoderCompleto
{
    public required IReadOnlyList<MapaDePoder> Poderes { get; init; }

    /// <summary>A ressalva da curadoria, para a tela não precisar inventá-la.</summary>
    public string RessalvaDaCuradoria { get; } = VocabularioDasCadeiras.RessalvaDaCuradoria;
}

public interface IConsultasDeCadeiras
{
    Task<MapaDePoderCompleto> ObterMapaAsync(CancellationToken cancelamento = default);
}
