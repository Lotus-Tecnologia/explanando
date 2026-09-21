using System.Net.Http.Json;
using System.Text.Json;
using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Senado;

/// <summary>
/// As lideranças do Congresso, numa chamada só.
///
/// <para><b>É o Senado que publica as lideranças das duas Casas.</b> O endpoint devolve
/// 286 designações vigentes — Senado, Câmara e Congresso Nacional —, o que torna este
/// coletor mais barato e mais completo do que perguntar partido por partido à API da
/// Câmara. É também por isso que a fonte se chama "liderancas", e não "senado": dividir
/// por Casa faria dois coletores disputarem as mesmas linhas.</para>
///
/// <para><b>Só os líderes entram, e é aí que está a decisão.</b> Dos 286 registros, 232
/// são vice-líderes — 185 só na Câmara. Um mapa de <i>comando</i> com 232 vice-líderes
/// não é um mapa de comando: é a lista de filiação partidária desenhada em círculos.
/// Ficam os 47 líderes, que são quem de fato negocia em nome da bancada.</para>
/// </summary>
public sealed class ClienteDeLiderancas(HttpClient http) : IFonteDeCadeiras
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string Caminho = "composicao/lideranca.json";

    private const string Origem = "https://legis.senado.leg.br/dadosabertos/composicao/lideranca.json";

    /// <summary>
    /// O piso de sanidade. Hoje são 47 líderes.
    ///
    /// <para>Se vier menos que isto, ou a designação foi toda revogada — o que não
    /// acontece — ou o formato mudou. Publicar meia dúzia de líderes faria o mapa do
    /// Legislativo parecer um Congresso sem bancadas.</para>
    /// </summary>
    private const int MinimoDeLideres = 15;

    public string Nome => "liderancas";

    public async Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default)
    {
        List<Lideranca>? registros;

        try
        {
            registros = await http
                .GetFromJsonAsync<List<Lideranca>>(Caminho, Json, cancelamento)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException erro)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha = $"A API do Senado não respondeu às lideranças: {erro.Message}",
            };
        }
        catch (JsonException erro)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha = $"As lideranças vieram num formato que não reconheço: {erro.Message}",
            };
        }

        if (registros is null)
        {
            return new ColetaDeCadeiras { Fonte = Nome, Falha = "As lideranças vieram vazias." };
        }

        var lideres = registros
            .Where(r => string.Equals(r.SiglaTipoLideranca, "L", StringComparison.Ordinal))
            .Where(r => !string.IsNullOrWhiteSpace(r.NomeParlamentar))
            .ToList();

        if (lideres.Count < MinimoDeLideres)
        {
            return new ColetaDeCadeiras
            {
                Fonte = Nome,
                Falha =
                    $"Vieram {lideres.Count} líderes, abaixo do piso de {MinimoDeLideres}. "
                    + "O campo que separa líder de vice-líder provavelmente mudou.",
            };
        }

        return new ColetaDeCadeiras
        {
            Fonte = Nome,
            Cadeiras =
            [
                .. lideres.Select(r => new NovaCadeira
                {
                    Poder = Poder.Legislativo,
                    Camada = CamadaDeComando.Comando,

                    // A descricao da UNIDADE e' o que diz de quem ele e' lider — "Lideranca
                    // do Governo no Congresso Nacional", "Lideranca de Partido no Senado".
                    // Sem ela, vinte e dois "Lider do Senado Federal" seriam indistinguiveis.
                    Cargo = MontarCargo(r),
                    Ocupante = r.NomeParlamentar,
                    Orgao = r.DescricaoTipoUnidadeLideranca,
                    Partido = r.SiglaPartidoFiliacao,
                    Fonte = Origem,
                    Origem = OrigemDaCadeira.Automatica,
                }),
            ],
        };
    }

    private static string MontarCargo(Lideranca registro)
    {
        var tipo = registro.DescricaoTipoLideranca ?? "Líder";
        var unidade = registro.DescricaoTipoUnidadeLideranca;

        if (string.IsNullOrWhiteSpace(unidade))
        {
            return tipo;
        }

        var partido = registro.SiglaPartidoFiliacao;

        return string.IsNullOrWhiteSpace(partido)
            ? $"{tipo} — {unidade}"
            : $"{tipo} — {partido}";
    }

    private sealed record Lideranca(
        string? Casa,
        string? NomeParlamentar,
        string? SiglaTipoLideranca,
        string? DescricaoTipoLideranca,
        string? DescricaoTipoUnidadeLideranca,
        string? SiglaPartidoFiliacao);
}
