using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Camara;

/// <summary>
/// As cadeiras de comando da Câmara dos Deputados.
///
/// <para><b>Aqui há API, e por isso este coletor é o oposto do Planalto:</b> nada de ler
/// HTML, e a fonte já traz o título de cada cadeira — "Presidente", "1º Secretário",
/// "2º Vice-Presidente". O que o Executivo obriga a deduzir do texto, o Congresso entrega
/// pronto.</para>
///
/// <para>Três chamadas de lista e uma por comissão, ~33 no total. A Mesa é achada pela
/// <b>sigla</b> dentro do tipo "Comissão Diretora", e não pelo identificador: <c>4</c> é o
/// número de hoje, e um número mágico num cadastro de terceiros é a espécie de coisa que
/// muda sem aviso e some sem erro.</para>
/// </summary>
public sealed class ClienteDeCadeirasDaCamara(HttpClient http) : IFonteDeCadeiras
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const int TipoComissaoDiretora = 1;
    private const int TipoComissaoPermanente = 2;

    private const string Origem = "https://dadosabertos.camara.leg.br/api/v2/";

    /// <summary>
    /// O único título que interessa numa comissão: o Presidente dela.
    ///
    /// <para><b>Nem os vices.</b> A primeira versão aceitava qualquer título que contivesse
    /// "Presidente", e a coleta rendeu 111 cadeiras na Câmara — 30 presidências e noventa
    /// vice-presidências. É o mesmo problema dos 232 vice-líderes do Senado: um mapa de
    /// <i>comando</i> com noventa vices não é um mapa de comando, é o organograma da
    /// Casa. Os 50 titulares e 46 suplentes de cada comissão já estavam de fora.</para>
    ///
    /// <para>A Mesa é tratada à parte e aí os vices entram — lá o colegiado <b>é</b> o
    /// órgão de direção, e a 1ª Vice-Presidência da Câmara é cargo de comando de verdade.</para>
    /// </summary>
    private static bool PresideAComissao(string? titulo) =>
        string.Equals(titulo, "Presidente", StringComparison.OrdinalIgnoreCase);

    public string Nome => "camara";

    public async Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default)
    {
        var cadeiras = new List<NovaCadeira>();

        var diretoras = await ListarOrgaosAsync(TipoComissaoDiretora, cancelamento)
            .ConfigureAwait(false);

        if (diretoras is null)
        {
            return Falhou("A API da Câmara não respondeu à lista de comissões diretoras.");
        }

        var mesa = diretoras.FirstOrDefault(orgao =>
            string.Equals(orgao.Sigla, "MESA", StringComparison.OrdinalIgnoreCase));

        if (mesa is null)
        {
            return Falhou(
                "A Mesa Diretora não apareceu entre as comissões diretoras da Câmara. "
                + "A sigla MESA pode ter mudado; sem ela o mapa do Legislativo perde a "
                + "presidência da Casa, que é a cadeira mais importante dele.");
        }

        var daMesa = await ListarMembrosAsync(mesa.Id, cancelamento).ConfigureAwait(false);

        if (daMesa is null)
        {
            return Falhou("A API da Câmara não respondeu aos membros da Mesa Diretora.");
        }

        foreach (var membro in daMesa.Where(m => !string.IsNullOrWhiteSpace(m.Titulo)))
        {
            // O Presidente da Camara sobe para a CHEFIA; o resto da Mesa e' cupula. Sao
            // camadas diferentes porque a diferenca e' real: um preside a Casa, os outros
            // compoem o colegiado que a dirige.
            var presidente = string.Equals(membro.Titulo, "Presidente", StringComparison.OrdinalIgnoreCase);

            cadeiras.Add(new NovaCadeira
            {
                Poder = Poder.Legislativo,
                Camada = presidente ? CamadaDeComando.Chefia : CamadaDeComando.Cupula,
                Cargo = presidente
                    ? "Presidente da Câmara dos Deputados"
                    : $"{membro.Titulo} da Câmara dos Deputados",
                Ocupante = membro.Nome,
                Orgao = mesa.Nome,
                Partido = membro.SiglaPartido,
                Uf = membro.SiglaUf,
                FotoUrl = membro.UrlFoto,
                Fonte = $"{Origem}orgaos/{Texto(mesa.Id)}/membros",
                Origem = OrigemDaCadeira.Automatica,
            });
        }

        var permanentes = await ListarOrgaosAsync(TipoComissaoPermanente, cancelamento)
            .ConfigureAwait(false);

        if (permanentes is null)
        {
            return Falhou("A API da Câmara não respondeu à lista de comissões permanentes.");
        }

        foreach (var comissao in permanentes)
        {
            var membros = await ListarMembrosAsync(comissao.Id, cancelamento).ConfigureAwait(false);

            // Uma comissao que nao respondeu e' uma comissao a menos, e nao a coleta
            // inteira perdida: as outras 29 continuam validas.
            if (membros is null)
            {
                continue;
            }

            foreach (var membro in membros.Where(m => PresideAComissao(m.Titulo)))
            {
                cadeiras.Add(new NovaCadeira
                {
                    Poder = Poder.Legislativo,
                    Camada = CamadaDeComando.Representacao,
                    Cargo = $"Presidente da {comissao.Sigla}",
                    Ocupante = membro.Nome,
                    Orgao = comissao.Nome,
                    Partido = membro.SiglaPartido,
                    Uf = membro.SiglaUf,
                    FotoUrl = membro.UrlFoto,
                    Fonte = $"{Origem}orgaos/{Texto(comissao.Id)}/membros",
                    Origem = OrigemDaCadeira.Automatica,
                });
            }
        }

        return new ColetaDeCadeiras { Fonte = Nome, Cadeiras = cadeiras };
    }

    private ColetaDeCadeiras Falhou(string motivo) =>
        new() { Fonte = Nome, Falha = motivo };

    private async Task<List<OrgaoDaCamara>?> ListarOrgaosAsync(
        int tipo,
        CancellationToken cancelamento)
    {
        var caminho = $"orgaos?codTipoOrgao={Texto(tipo)}&itens=100&ordem=ASC&ordenarPor=id";

        return (await LerAsync<OrgaoDaCamara>(caminho, cancelamento).ConfigureAwait(false));
    }

    private async Task<List<MembroDeOrgao>?> ListarMembrosAsync(
        int orgao,
        CancellationToken cancelamento) =>
        await LerAsync<MembroDeOrgao>(
                $"orgaos/{Texto(orgao)}/membros?itens=100",
                cancelamento)
            .ConfigureAwait(false);

    private async Task<List<T>?> LerAsync<T>(string caminho, CancellationToken cancelamento)
    {
        try
        {
            var envelope = await http
                .GetFromJsonAsync<Envelope<T>>(caminho, Json, cancelamento)
                .ConfigureAwait(false);

            return envelope?.Dados;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Texto(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    private sealed record Envelope<T>([property: JsonPropertyName("dados")] List<T> Dados);

    private sealed record OrgaoDaCamara(int Id, string? Sigla, string? Nome);

    private sealed record MembroDeOrgao(
        string? Nome,
        string? Titulo,
        string? SiglaPartido,
        string? SiglaUf,
        string? UrlFoto);
}
