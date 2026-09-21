using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Senado;

/// <summary>
/// A presidência do Senado, que só aparece pela porta dos fundos.
///
/// <para><b>O Senado não publica a Mesa dele.</b> Foram sondados
/// <c>composicao/mesa</c>, <c>composicao/mesa/atual</c>, <c>composicao/mesa/SF</c> e
/// <c>composicao/comissao/MESA</c> — todos 404 —, e a lista geral de colegiados traz uma
/// única Mesa: a do <b>Congresso Nacional</b>, código 1616.</para>
///
/// <para>E é o bastante, porque quem preside o Congresso Nacional é, por determinação
/// constitucional, o Presidente do Senado Federal. Sem este coletor a camada "Presidência
/// das Casas" tinha <b>uma</b> cadeira — só a Câmara —, e o mapa do Legislativo mostrava
/// meio Congresso.</para>
///
/// <para><b>Só o presidente entra, e isso é escolha.</b> A Mesa do Congresso é mista:
/// dois dos seus membros (Altineu Côrtes, Carlos Veras) já estão no mapa pela Mesa da
/// Câmara. Trazer os sete duplicaria pessoas na mesma tela por ocuparem dois cargos
/// reais — verdade que confunde mais do que informa. A Mesa do Senado propriamente dita
/// continua faltando, e faltará enquanto o Senado não a publicar.</para>
/// </summary>
public sealed class ClienteDaMesaDoCongresso(HttpClient http) : IFonteDeCadeiras
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// O código do colegiado, achado na lista geral.
    ///
    /// <para>É um número mágico e não dá para fugir dele: o endpoint de composição exige
    /// o código, e a lista geral não tem filtro por tipo "Mesa". A defesa é o
    /// <see cref="ColetaDeCadeiras.Falha"/> — se 1616 deixar de ser a Mesa, a coleta
    /// falha em vez de gravar a presidência de outro colegiado qualquer.</para>
    /// </summary>
    private const int ColegiadoDaMesaDoCongresso = 1616;

    private const string Origem =
        "https://legis.senado.leg.br/dadosabertos/comissao/1616.json";

    public string Nome => "mesa-cn";

    public async Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default)
    {
        Resposta? resposta;

        try
        {
            resposta = await http
                .GetFromJsonAsync<Resposta>($"comissao/{ColegiadoDaMesaDoCongresso}.json", Json, cancelamento)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException erro)
        {
            return Falhou($"A API do Senado não respondeu à Mesa do Congresso: {erro.Message}");
        }
        catch (JsonException erro)
        {
            return Falhou($"A Mesa do Congresso veio num formato que não reconheço: {erro.Message}");
        }

        var colegiado = resposta?.ComissoesCongressoNacional?.Colegiados?.Colegiado?.FirstOrDefault();

        if (colegiado is null)
        {
            return Falhou("A Mesa do Congresso veio vazia.");
        }

        // A defesa contra o numero magico: se 1616 virar outro colegiado, isto pega.
        if (colegiado.NomeColegiado?.Contains("Mesa", StringComparison.OrdinalIgnoreCase) != true)
        {
            return Falhou(
                $"O colegiado {ColegiadoDaMesaDoCongresso} agora se chama "
                + $"'{colegiado.NomeColegiado}', e não é mais a Mesa. Gravar a presidência "
                + "dele poria a pessoa errada na chefia do Legislativo.");
        }

        var presidente = colegiado.Cargos?.Cargo?.FirstOrDefault(c =>
            string.Equals(c.TipoCargo, "PRESIDENTE", StringComparison.OrdinalIgnoreCase));

        if (presidente?.NomeParlamentar is null)
        {
            return Falhou("A Mesa do Congresso veio sem presidente.");
        }

        return new ColetaDeCadeiras
        {
            Fonte = Nome,
            Cadeiras =
            [
                new NovaCadeira
                {
                    Poder = Poder.Legislativo,
                    Camada = CamadaDeComando.Chefia,
                    Cargo = "Presidente do Senado Federal e do Congresso Nacional",
                    Ocupante = SemTratamento(presidente.NomeParlamentar),
                    Orgao = colegiado.NomeColegiado,
                    Partido = SiglaDaBancada(presidente.Bancada),
                    Uf = UfDaBancada(presidente.Bancada),
                    Fonte = Origem,
                    Origem = OrigemDaCadeira.Automatica,
                },
            ],
        };
    }

    private ColetaDeCadeiras Falhou(string motivo) => new() { Fonte = Nome, Falha = motivo };

    /// <summary>
    /// Tira o "Senador" / "Deputado" da frente do nome.
    ///
    /// <para>O tratamento é do cargo, e o cargo já está na coluna dele. Deixá-lo no nome
    /// faria a tela escrever "Senador Davi Alcolumbre" embaixo de "Presidente do Senado
    /// Federal", que é a mesma informação duas vezes.</para>
    /// </summary>
    private static string SemTratamento(string nome)
    {
        foreach (var tratamento in (string[])["Senadora ", "Senador ", "Deputada ", "Deputado "])
        {
            if (nome.StartsWith(tratamento, StringComparison.OrdinalIgnoreCase))
            {
                return nome[tratamento.Length..].Trim();
            }
        }

        return nome.Trim();
    }

    /// <summary>A bancada chega como "(UNIÃO-AP)". Partido e UF saem daí, ou de lugar nenhum.</summary>
    private static string? SiglaDaBancada(string? bancada) => PartesDaBancada(bancada)?.Partido;

    private static string? UfDaBancada(string? bancada) => PartesDaBancada(bancada)?.Uf;

    private static (string Partido, string Uf)? PartesDaBancada(string? bancada)
    {
        if (string.IsNullOrWhiteSpace(bancada))
        {
            return null;
        }

        var limpo = bancada.Trim().Trim('(', ')');
        var traco = limpo.LastIndexOf('-');

        return traco <= 0 || traco == limpo.Length - 1
            ? null
            : (limpo[..traco], limpo[(traco + 1)..]);
    }

    private sealed record Resposta(
        [property: JsonPropertyName("ComissoesCongressoNacional")] Envelope? ComissoesCongressoNacional);

    private sealed record Envelope(Colegiados? Colegiados);

    private sealed record Colegiados(List<Colegiado>? Colegiado);

    private sealed record Colegiado(string? NomeColegiado, Cargos? Cargos);

    private sealed record Cargos(List<Cargo>? Cargo);

    private sealed record Cargo(string? TipoCargo, string? NomeParlamentar, string? Bancada);
}
