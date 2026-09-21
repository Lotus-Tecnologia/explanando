using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;
using Microsoft.Extensions.Time.Testing;

namespace Explanando.Application.Tests.Cadeiras;

/// <summary>
/// A coleta das cadeiras de comando.
///
/// <para><b>Tudo aqui gira em torno de uma regra: o mapa pode ficar velho, mas não pode
/// ficar vazio por engano.</b> São quatro fontes independentes — duas páginas do Planalto,
/// duas APIs do Congresso — e cada uma cai por sua conta. Um serviço que tratasse a coleta
/// como uma transação só apagaria o Congresso porque o portal da Presidência estava em
/// manutenção.</para>
/// </summary>
public class ServicoDeColetaDeCadeirasTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class FonteFalsa(string nome, ColetaDeCadeiras resposta) : IFonteDeCadeiras
    {
        public string Nome => nome;

        public Task<ColetaDeCadeiras> LerAsync(CancellationToken cancelamento = default) =>
            Task.FromResult(resposta);
    }

    private sealed class RepositorioEmMemoria : IRepositorioDeCadeiras
    {
        public Dictionary<string, List<CadeiraDeComando>> PorFonte { get; } = [];

        public List<string> FontesSubstituidas { get; } = [];

        public Task SubstituirAsync(
            string fonte,
            IReadOnlyList<CadeiraDeComando> cadeiras,
            CancellationToken cancelamento = default)
        {
            FontesSubstituidas.Add(fonte);
            PorFonte[fonte] = [.. cadeiras];

            return Task.CompletedTask;
        }
    }

    private static NovaCadeira Cadeira(string cargo) => new()
    {
        Poder = Poder.Executivo,
        Camada = CamadaDeComando.Cupula,
        Cargo = cargo,
        Ocupante = "Fulano",
        Fonte = "https://exemplo/",
        Origem = OrigemDaCadeira.Automatica,
    };

    private static ServicoDeColetaDeCadeiras Servico(
        RepositorioEmMemoria repositorio,
        params IFonteDeCadeiras[] fontes) =>
        new(fontes, repositorio, new FakeTimeProvider(Agora));

    [Fact]
    public async Task Grava_o_que_cada_fonte_trouxe__separadamente()
    {
        var repositorio = new RepositorioEmMemoria();

        var resumo = await Servico(
                repositorio,
                new FonteFalsa("planalto", new ColetaDeCadeiras
                {
                    Fonte = "planalto",
                    Cadeiras = [Cadeira("Ministro A"), Cadeira("Ministro B")],
                }),
                new FonteFalsa("camara", new ColetaDeCadeiras
                {
                    Fonte = "camara",
                    Cadeiras = [Cadeira("Presidente da Câmara")],
                }))
            .ColetarAsync();

        Assert.Equal(3, resumo.Gravadas);
        Assert.Equal(0, resumo.Falhas);
        Assert.Equal(["planalto", "camara"], repositorio.FontesSubstituidas);
        Assert.Equal(2, repositorio.PorFonte["planalto"].Count);
    }

    [Fact]
    public async Task Uma_fonte_que_cai_NAO_derruba_as_outras()
    {
        // A propriedade que justifica o servico existir. Planalto em manutencao nao pode
        // apagar o Congresso do mapa.
        var repositorio = new RepositorioEmMemoria();

        var resumo = await Servico(
                repositorio,
                new FonteFalsa("planalto", new ColetaDeCadeiras
                {
                    Fonte = "planalto",
                    Falha = "portal fora do ar",
                }),
                new FonteFalsa("camara", new ColetaDeCadeiras
                {
                    Fonte = "camara",
                    Cadeiras = [Cadeira("Presidente da Câmara")],
                }))
            .ColetarAsync();

        Assert.Equal(1, resumo.Falhas);
        Assert.Equal(1, resumo.Gravadas);
        Assert.Equal(["camara"], repositorio.FontesSubstituidas);
    }

    [Fact]
    public async Task Fonte_que_falha_NAO_apaga_o_que_ela_mesma_ja_tinha_gravado()
    {
        // Substituir por lista vazia seria transformar indisponibilidade em "nao ha'
        // ministros" — o mesmo erro que a coleta do Estado evita com a coleta incompleta.
        var repositorio = new RepositorioEmMemoria();

        await Servico(repositorio, new FonteFalsa("planalto", new ColetaDeCadeiras
        {
            Fonte = "planalto",
            Falha = "portal fora do ar",
        })).ColetarAsync();

        Assert.Empty(repositorio.FontesSubstituidas);
    }

    [Fact]
    public async Task Coleta_VAZIA_sem_erro_tambem_nao_apaga__e_vira_falha()
    {
        // Uma pagina que responde 200 com o corpo trocado e' indistinguivel de um governo
        // sem ministros. Entre as duas leituras, a segunda e' a absurda.
        var repositorio = new RepositorioEmMemoria();

        var resumo = await Servico(repositorio, new FonteFalsa("planalto", new ColetaDeCadeiras
        {
            Fonte = "planalto",
            Cadeiras = [],
        })).ColetarAsync();

        Assert.Empty(repositorio.FontesSubstituidas);
        Assert.Equal(1, resumo.Falhas);
        Assert.Contains("mantido", resumo.PorFonte[0].Falha, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Toda_cadeira_gravada_leva_a_hora_da_coleta()
    {
        var repositorio = new RepositorioEmMemoria();

        await Servico(repositorio, new FonteFalsa("planalto", new ColetaDeCadeiras
        {
            Fonte = "planalto",
            Cadeiras = [Cadeira("Ministro A")],
        })).ColetarAsync();

        Assert.All(
            repositorio.PorFonte["planalto"],
            cadeira => Assert.Equal(Agora, cadeira.VerificadoEm));
    }

    [Fact]
    public async Task Sem_fonte_nenhuma_o_resumo_e_vazio__e_nao_estoura()
    {
        var repositorio = new RepositorioEmMemoria();

        var resumo = await Servico(repositorio).ColetarAsync();

        Assert.Empty(resumo.PorFonte);
        Assert.Equal(0, resumo.Gravadas);
    }
}
