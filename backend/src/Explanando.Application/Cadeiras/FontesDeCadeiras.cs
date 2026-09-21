using Explanando.Domain.Cadeiras;

namespace Explanando.Application.Cadeiras;

/// <summary>
/// O que uma fonte de cadeiras devolveu, ou a falha que impediu.
///
/// <para><b>A falha é um valor, como em <c>SubarvoreDoEstado</c>, e pelo mesmo motivo.</b>
/// Cada poder vem de uma fonte diferente — o Executivo do portal da Presidência, o
/// Legislativo de duas APIs, o Judiciário de uma tabela curada. Se a do Executivo cair, o
/// mapa de poder do Legislativo continua correto, e apagar as cadeiras do Executivo
/// porque o portal ficou fora do ar seria transformar indisponibilidade em "não há
/// ministros".</para>
/// </summary>
public sealed record ColetaDeCadeiras
{
    /// <summary>Quem coletou. É a chave de substituição no repositório.</summary>
    public required string Fonte { get; init; }

    public IReadOnlyList<NovaCadeira> Cadeiras { get; init; } = [];

    /// <summary>Nulo quando deu certo.</summary>
    public string? Falha { get; init; }

    public bool DeuCerto => Falha is null;
}

/// <summary>
/// Uma fonte de cadeiras de comando.
///
/// <para>Uma por origem, e não uma por poder: o Senado publica as lideranças das
/// <b>duas</b> casas num endpoint só, e forçar isso a caber em "fonte do Senado" faria o
/// coletor da Câmara e o do Senado disputarem as mesmas linhas.</para>
/// </summary>
public interface IFonteDeCadeiras
{
    /// <summary>Um nome curto e estável, que vira a chave de substituição.</summary>
    string Nome { get; }

    Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default);
}

/// <summary>A escrita das cadeiras.</summary>
public interface IRepositorioDeCadeiras
{
    /// <summary>
    /// Troca <b>todas</b> as cadeiras de uma fonte pelas que acabaram de ser lidas.
    ///
    /// <para>Substituição, e não mesclagem, porque a fonte é a verdade sobre si mesma: um
    /// ministério extinto some da página do Planalto, e mesclar o deixaria no nosso mapa
    /// para sempre. O recorte é por fonte justamente para uma queda do Planalto não
    /// derrubar as cadeiras do Congresso.</para>
    /// </summary>
    Task SubstituirAsync(
        string fonte,
        IReadOnlyList<CadeiraDeComando> cadeiras,
        CancellationToken cancelamento = default);

    /// <summary>
    /// Quando cada coletor gravou pela última vez.
    ///
    /// <para><b>Por coletor, e não um carimbo só.</b> Se o Planalto falha há três semanas
    /// enquanto a Câmara grava toda noite, um carimbo global estaria sempre fresco e o
    /// Executivo nunca seria tentado de novo — a tela mostraria um ministério parado com
    /// cara de atual. Coletor que falta do dicionário nunca rodou, e está vencido por
    /// definição.</para>
    /// </summary>
    Task<IReadOnlyDictionary<string, DateTimeOffset>> UltimaColetaAsync(
        CancellationToken cancelamento = default);
}
