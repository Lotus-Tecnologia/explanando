using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Explanando.Application.Ingestao;
using Explanando.Domain.Parlamentares;

namespace Explanando.Infrastructure.Integracoes.Senado;

/// <summary>
/// Cliente do Senado Federal, implementando a mesma porta que a Câmara.
///
/// <para><b>As duas casas entregam a despesa de formas opostas, e é isso que esta classe
/// concilia.</b> A Câmara serve por deputado, paginado — a identidade está na chamada. A
/// CEAPS serve <b>o ano inteiro de todos os senadores num array só</b>, sem paginação: 2025
/// são 23.799 lançamentos e 10 MB em 0,7 s. Buscar "as despesas do senador X" contra a
/// origem não existe como operação.</para>
///
/// <para>Então o cliente baixa os anos <b>uma vez</b>, agrupa por <c>codSenador</c> e serve
/// fatias. É o que permite ao <c>ServicoDeColeta</c> rodar sem alteração nenhuma — com os
/// mesmos checkpoints, o mesmo freio de sanidade e a mesma retomada que salvaram a coleta da
/// Câmara numa queda de energia.</para>
///
/// <para><b>São dois serviços, não um</b> (<c>docs/contratos/senado.md</c> §1). A CEAPS vem
/// do administrativo; o cadastro de quem é cada código vem do legislativo. Por isso as URLs
/// são absolutas em vez de um <c>BaseAddress</c>.</para>
///
/// <para><b>O cadastro precisa de duas legislaturas.</b> Só a atual perderia nome e UF de
/// quem assumiu como suplente e saiu — 2025 teve 82 senadores para 81 cadeiras. Medido na
/// janela 2023-2026: a lista atual cobre 101 dos 118 que gastaram; somando a legislatura
/// anterior, cobre <b>118 de 118</b>, todos com UF, partido e foto.</para>
/// </summary>
public sealed class ClienteDoSenado(HttpClient http) : IFonteDeParlamentares, IDisposable
{
    private const string Ceaps =
        "https://adm.senado.gov.br/adm-dadosabertos/api/v1/senadores/despesas_ceaps/";

    private const string Cadastro =
        "https://legis.senado.leg.br/dadosabertos/senador/lista/legislatura/";

    /// <summary>
    /// A legislatura 57 começou em 2023, e cada uma dura quatro anos. Serve para traduzir o
    /// <c>--legislatura</c> do worker na faixa de anos que a CEAPS entende, já que ela é
    /// indexada por ano e não sabe o que é legislatura.
    /// </summary>
    private const int LegislaturaDeReferencia = 57;
    private const int AnoInicialDaReferencia = 2023;
    private const int AnosPorLegislatura = 4;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _trava = new(1, 1);
    private Dictionary<int, DespesasDeUmSenador>? _porSenador;
    private int _legislaturaCarregada;

    /// <summary>Os anos que uma legislatura cobre, sem passar do ano corrente.</summary>
    public static IReadOnlyList<int> AnosDaLegislatura(int legislatura, int anoCorrente)
    {
        var inicio = AnoInicialDaReferencia
            - ((LegislaturaDeReferencia - legislatura) * AnosPorLegislatura);

        var fim = Math.Min(inicio + AnosPorLegislatura - 1, anoCorrente);

        return fim < inicio ? [] : [.. Enumerable.Range(inicio, fim - inicio + 1)];
    }

    /// <summary>
    /// Os senadores que <b>de fato gastaram</b> na janela, e não as cadeiras da casa.
    ///
    /// <para>Devolver o cadastro inteiro criaria parlamentar para quem nunca lançou despesa —
    /// 245 registros na legislatura 57 contra 118 que gastaram —, e cada um deles apareceria
    /// no ranking com volume zero. Zero por não ter gasto e zero por não ter sido coletado
    /// são indistinguíveis no ranking, que é a classe de erro que o produto inteiro evita.</para>
    /// </summary>
    public async IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        var legislatura = _legislaturaCarregada is 0 ? LegislaturaDeReferencia : _legislaturaCarregada;

        var despesas = await GarantirCarregadoAsync(legislatura, cancelamento).ConfigureAwait(false);
        var cadastro = await ObterCadastroAsync(legislatura, cancelamento).ConfigureAwait(false);

        foreach (var (codigo, lote) in despesas.OrderBy(par => par.Key))
        {
            yield return Montar(codigo, lote, cadastro, legislatura);
        }
    }

    public async Task<DeputadoDaFonte?> ObterDetalheAsync(
        int idExterno,
        CancellationToken cancelamento = default)
    {
        var legislatura = _legislaturaCarregada is 0 ? LegislaturaDeReferencia : _legislaturaCarregada;

        var despesas = await GarantirCarregadoAsync(legislatura, cancelamento).ConfigureAwait(false);

        if (!despesas.TryGetValue(idExterno, out var lote))
        {
            return null;
        }

        var cadastro = await ObterCadastroAsync(legislatura, cancelamento).ConfigureAwait(false);

        return Montar(idExterno, lote, cadastro, legislatura);
    }

    /// <summary>
    /// A fatia já baixada, sem tocar na rede.
    ///
    /// <para>Senador sem lote devolve vazio em silêncio — e isso não vira alerta de sanidade
    /// porque <see cref="ListarDeputadosAsync"/> só entrega quem tem despesa.</para>
    /// </summary>
    public async IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
        int idExterno,
        int legislatura,
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        var despesas = await GarantirCarregadoAsync(legislatura, cancelamento).ConfigureAwait(false);

        if (!despesas.TryGetValue(idExterno, out var lote))
        {
            yield break;
        }

        foreach (var despesa in lote.Despesas)
        {
            yield return despesa;
        }
    }

    // ------------------------------------------------------------------
    //  Carga
    // ------------------------------------------------------------------

    private async Task<Dictionary<int, DespesasDeUmSenador>> GarantirCarregadoAsync(
        int legislatura,
        CancellationToken cancelamento)
    {
        if (_porSenador is not null && _legislaturaCarregada == legislatura)
        {
            return _porSenador;
        }

        await _trava.WaitAsync(cancelamento).ConfigureAwait(false);

        try
        {
            if (_porSenador is not null && _legislaturaCarregada == legislatura)
            {
                return _porSenador;
            }

            var acumulado = new Dictionary<int, DespesasDeUmSenador>();

            foreach (var ano in AnosDaLegislatura(legislatura, DateTimeOffset.UtcNow.Year))
            {
                var corpo = await BaixarAsync(
                    Ceaps + ano.ToString(CultureInfo.InvariantCulture), cancelamento)
                    .ConfigureAwait(false);

                if (corpo is null)
                {
                    continue;
                }

                foreach (var lote in ConversorDoSenado.ConverterLotePorSenador(corpo))
                {
                    acumulado[lote.CodSenador] = acumulado.TryGetValue(lote.CodSenador, out var antes)
                        ? antes with
                        {
                            NomeNaOrigem = antes.NomeNaOrigem ?? lote.NomeNaOrigem,
                            Despesas = [.. antes.Despesas, .. lote.Despesas],
                        }
                        : lote;
                }
            }

            _legislaturaCarregada = legislatura;
            _porSenador = acumulado;

            return acumulado;
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>
    /// O cadastro da legislatura pedida <b>e da anterior</b>. Ver a nota da classe: só a
    /// atual perderia quem assumiu e saiu.
    /// </summary>
    private async Task<Dictionary<int, SenadorNoCadastro>> ObterCadastroAsync(
        int legislatura,
        CancellationToken cancelamento)
    {
        var porCodigo = new Dictionary<int, SenadorNoCadastro>();

        // A mais antiga primeiro: a mais recente sobrescreve, porque partido e foto mudam.
        foreach (var numero in new[] { legislatura - 1, legislatura })
        {
            var corpo = await BaixarAsync(
                $"{Cadastro}{numero.ToString(CultureInfo.InvariantCulture)}.json", cancelamento)
                .ConfigureAwait(false);

            if (corpo is null)
            {
                continue;
            }

            foreach (var senador in Interpretar(corpo))
            {
                porCodigo[senador.Codigo] = senador;
            }
        }

        return porCodigo;
    }

    private static IEnumerable<SenadorNoCadastro> Interpretar(string corpo)
    {
        ListaDeParlamentaresJson? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<ListaDeParlamentaresJson>(corpo, Json);
        }
        catch (JsonException)
        {
            // Cadastro malformado nao pode derrubar a coleta: sem ele a despesa ainda entra,
            // com o nome que a propria CEAPS deu.
            yield break;
        }

        var lista = envelope?.ListaParlamentarLegislatura?.Parlamentares?.Parlamentar ?? [];

        foreach (var parlamentar in lista)
        {
            var identificacao = parlamentar.IdentificacaoParlamentar;

            if (identificacao?.CodigoParlamentar is not { Length: > 0 } bruto
                || !int.TryParse(bruto, CultureInfo.InvariantCulture, out var codigo))
            {
                continue;
            }

            // A UF so' vem na identificacao para 84 dos 245; no mandato, vem sempre. Medido.
            var uf = Primeiro(identificacao.UfParlamentar)
                ?? parlamentar.Mandatos?.Mandato
                    ?.Select(mandato => Primeiro(mandato.UfParlamentar))
                    .FirstOrDefault(valor => valor is not null);

            yield return new SenadorNoCadastro
            {
                Codigo = codigo,
                NomeParlamentar = Primeiro(identificacao.NomeParlamentar),
                NomeCompleto = Primeiro(identificacao.NomeCompletoParlamentar),
                Partido = Primeiro(identificacao.SiglaPartidoParlamentar),
                Uf = uf,
                FotoUrl = ComEsquemaSeguro(Primeiro(identificacao.UrlFotoParlamentar)),
            };
        }
    }

    private static string? Primeiro(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// O retrato em <c>https</c>, porque o Senado o publica em <c>http</c>.
    ///
    /// <para>Verificado: o cadastro devolve
    /// <c>http://www.senado.leg.br/senadores/img/fotos-oficiais/senadorNNNN.jpg</c>. Numa
    /// página servida por HTTPS o navegador bloqueia isso como conteúdo misto, e o retrato
    /// some <b>sem erro visível</b> — a pior forma de falhar, porque ninguém percebe.</para>
    ///
    /// <para>Elevar o esquema é seguro: medido, o endereço seguro responde 301 para
    /// <c>legis.senado.leg.br/senadores/fotos-oficiais/NNNN</c> e entrega a mesma imagem.</para>
    ///
    /// <para>Só para o host do Senado. Reescrever URL de host arbitrário não é conta nossa —
    /// e a Câmara já publica em <c>https</c>.</para>
    /// </summary>
    private static string? ComEsquemaSeguro(string? url) =>
        url is not null && url.StartsWith("http://www.senado.leg.br/", StringComparison.OrdinalIgnoreCase)
            ? string.Concat("https://", url.AsSpan("http://".Length))
            : url;

    private async Task<string?> BaixarAsync(string url, CancellationToken cancelamento)
    {
        using var resposta = await http.GetAsync(new Uri(url), cancelamento).ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode)
        {
            return null;
        }

        return await resposta.Content.ReadAsStringAsync(cancelamento).ConfigureAwait(false);
    }

    /// <summary>
    /// O cadastro manda no nome, na UF e no partido; a CEAPS é a rede de segurança.
    ///
    /// <para><b>UF vazia não é erro a corrigir aqui.</b> Ela faz <c>REG_GEO_002</c> se abster
    /// para essa pessoa, que é o desfecho certo do ADR-0007: inventar a base eleitoral de
    /// alguém para preencher um campo produziria achado geográfico sobre um lugar que
    /// ninguém afirmou.</para>
    /// </summary>
    private static DeputadoDaFonte Montar(
        int codigo,
        DespesasDeUmSenador lote,
        IReadOnlyDictionary<int, SenadorNoCadastro> cadastro,
        int legislatura)
    {
        cadastro.TryGetValue(codigo, out var ficha);

        var nome = ficha?.NomeParlamentar ?? lote.NomeNaOrigem ?? $"Senador {codigo}";

        return new DeputadoDaFonte
        {
            IdExterno = codigo,
            NomeParlamentar = nome,
            NomeCivil = ficha?.NomeCompleto ?? nome,
            SiglaPartido = ficha?.Partido ?? string.Empty,
            Uf = ficha?.Uf ?? string.Empty,
            FotoUrl = ficha?.FotoUrl,
            Legislatura = legislatura,
            Tipo = TipoParlamentar.Senador,
        };
    }

    /// <summary>
    /// A trava que serializa a carga e' descartavel, entao o cliente tambem precisa ser —
    /// e' o que o CA1001 cobra. O <c>HttpClient</c> injetado NAO se descarta aqui: ele
    /// pertence a fabrica, e fecha-lo derrubaria o pool de conexoes compartilhado.
    /// </summary>
    public void Dispose() => _trava.Dispose();

    private sealed record SenadorNoCadastro
    {
        public required int Codigo { get; init; }
        public string? NomeParlamentar { get; init; }
        public string? NomeCompleto { get; init; }
        public string? Partido { get; init; }
        public string? Uf { get; init; }
        public string? FotoUrl { get; init; }
    }

    // ------------------------------------------------------------------
    //  O envelope do servico legislativo
    //
    //  Aninhado em quatro niveis e com os numeros vindo como STRING
    //  ("CodigoParlamentar": "5918"). Tudo anulavel: um contrato de borda que exige campo
    //  transforma mudanca na origem em excecao de desserializacao.
    // ------------------------------------------------------------------

    private sealed record ListaDeParlamentaresJson
    {
        [JsonPropertyName("ListaParlamentarLegislatura")]
        public ListaJson? ListaParlamentarLegislatura { get; init; }
    }

    private sealed record ListaJson
    {
        [JsonPropertyName("Parlamentares")]
        public ParlamentaresJson? Parlamentares { get; init; }
    }

    private sealed record ParlamentaresJson
    {
        [JsonPropertyName("Parlamentar")]
        public IReadOnlyList<ParlamentarJson>? Parlamentar { get; init; }
    }

    private sealed record ParlamentarJson
    {
        [JsonPropertyName("IdentificacaoParlamentar")]
        public IdentificacaoJson? IdentificacaoParlamentar { get; init; }

        [JsonPropertyName("Mandatos")]
        public MandatosJson? Mandatos { get; init; }
    }

    private sealed record MandatosJson
    {
        [JsonPropertyName("Mandato")]
        public IReadOnlyList<MandatoJson>? Mandato { get; init; }
    }

    private sealed record MandatoJson
    {
        [JsonPropertyName("UfParlamentar")]
        public string? UfParlamentar { get; init; }
    }

    private sealed record IdentificacaoJson
    {
        [JsonPropertyName("CodigoParlamentar")]
        public string? CodigoParlamentar { get; init; }

        [JsonPropertyName("NomeParlamentar")]
        public string? NomeParlamentar { get; init; }

        [JsonPropertyName("NomeCompletoParlamentar")]
        public string? NomeCompletoParlamentar { get; init; }

        [JsonPropertyName("SiglaPartidoParlamentar")]
        public string? SiglaPartidoParlamentar { get; init; }

        [JsonPropertyName("UfParlamentar")]
        public string? UfParlamentar { get; init; }

        [JsonPropertyName("UrlFotoParlamentar")]
        public string? UrlFotoParlamentar { get; init; }
    }
}
