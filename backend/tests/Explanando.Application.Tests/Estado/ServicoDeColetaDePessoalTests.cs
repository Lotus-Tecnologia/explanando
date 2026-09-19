using System.Runtime.CompilerServices;
using Explanando.Application.Estado;
using Explanando.Domain.Comum;
using Explanando.Domain.Estado;

namespace Explanando.Application.Tests.Estado;

/// <summary>
/// A coleta do quadro de pessoal, e a ponte para o grafo.
///
/// <para>Duas coisas estão sendo protegidas aqui, e a segunda é a que importa mais. A
/// primeira é a contagem. A segunda é <b>a recusa em adivinhar</b>: um par errado entre os
/// dois cadastros não produz erro visível — produz o quadro de pessoal de um órgão exibido
/// dentro de outro, com a aparência de um fato.</para>
/// </summary>
public sealed class ServicoDeColetaDePessoalTests
{
    private static readonly Competencia Julho = Competencia.Criar(2026, 7);

    private static VinculoDaFonte Vinculo(int siafi, string nome, string? funcao = null) =>
        new() { CodigoSiafi = siafi, NomeDoOrgao = nome, SiglaDaFuncao = funcao };

    private static UnidadeParaPonte Unidade(int codigo, string nome, bool topo = true) =>
        new() { Codigo = codigo, Nome = nome, EhDeTopo = topo };

    // ------------------------------------------------------------------
    //  A contagem
    // ------------------------------------------------------------------

    [Fact]
    public async Task Agrega_por_orgao_e_conta_as_funcoes()
    {
        var repositorio = new RepositorioFalso([Unidade(471, "Universidade Federal de Sergipe")]);

        var resultado = await Coletar(repositorio,
        [
            Vinculo(26298, "Universidade Federal de Sergipe"),
            Vinculo(26298, "Universidade Federal de Sergipe", "CD"),
            Vinculo(26298, "Universidade Federal de Sergipe", "FG"),
            Vinculo(26298, "Universidade Federal de Sergipe", "FG"),
        ]);

        resultado.VinculosLidos.ShouldBe(4);
        resultado.OrgaosEncontrados.ShouldBe(1);

        var lotacao = repositorio.Gravadas.ShouldHaveSingleItem();

        lotacao.Vinculos.ShouldBe(4);
        lotacao.ComFuncaoDeConfianca.ShouldBe(3);
        lotacao.Funcoes.Single(f => f.Sigla == "FG").Quantidade.ShouldBe(2);
    }

    [Fact]
    public async Task O_agregado_que_nao_e_orgao_e_descartado_em_vez_de_virar_no()
    {
        // "Estados / Municipios / Empresas" carrega 16.689 vinculos na base real. E' uma
        // rubrica de totalizacao do proprio SIAPE — poe-la no grafo seria inventar um orgao.
        var repositorio = new RepositorioFalso([Unidade(471, "Universidade Federal de Sergipe")]);

        var resultado = await Coletar(repositorio,
        [
            Vinculo(99991, "Estados / Municípios / Empresas"),
            Vinculo(99991, "Estados / Municípios / Empresas"),
            Vinculo(26298, "Universidade Federal de Sergipe"),
        ]);

        resultado.AgregadosDescartados.ShouldBe(2);
        resultado.OrgaosEncontrados.ShouldBe(1);
        repositorio.Gravadas.ShouldAllBe(l => l.CodigoSiafi == 26298);
    }

    [Fact]
    public async Task Recoletar_a_mesma_competencia_nao_dobra_o_quadro()
    {
        // A fonte republica o arquivo do mes com correcoes, e recoletar e' rotina. Sem a
        // limpeza, a segunda passada dobraria o quadro de pessoal de todo orgao.
        var repositorio = new RepositorioFalso([Unidade(471, "Universidade Federal de Sergipe")]);

        await Coletar(repositorio, [Vinculo(26298, "Universidade Federal de Sergipe")]);
        await Coletar(repositorio, [Vinculo(26298, "Universidade Federal de Sergipe")]);

        repositorio.LimpezasPedidas.ShouldBe(2);
        repositorio.Gravadas.Count.ShouldBe(1);
    }

    // ------------------------------------------------------------------
    //  A ponte, e a recusa em adivinhar
    // ------------------------------------------------------------------

    [Fact]
    public async Task Liga_ao_grafo_quando_o_nome_normalizado_e_unico()
    {
        var repositorio = new RepositorioFalso([Unidade(471, "Universidade Federal de Sergipe")]);

        var resultado = await Coletar(repositorio,
            [Vinculo(26298, "Fundação Universidade Federal Sergipe - SE")]);

        resultado.OrgaosLigadosAoGrafo.ShouldBe(1);
        repositorio.Gravadas.Single().CodigoSiorg.ShouldBe(471);
    }

    [Fact]
    public async Task Nome_ambiguo_nao_casa_com_nenhum_dos_dois()
    {
        // O caso que este teste impede: duas unidades chamadas "Secretaria-Executiva", e o
        // quadro de pessoal de um ministerio inteiro caindo na secretaria de outro.
        var repositorio = new RepositorioFalso(
        [
            Unidade(100, "Secretaria-Executiva"),
            Unidade(200, "Secretaria Executiva"),
        ]);

        var resultado = await Coletar(repositorio, [Vinculo(30000, "Secretaria-Executiva")]);

        resultado.OrgaosLigadosAoGrafo.ShouldBe(0);
        repositorio.Gravadas.Single().CodigoSiorg.ShouldBeNull();
    }

    [Fact]
    public async Task Um_orgao_de_topo_vence_uma_unidade_interna_de_mesmo_nome()
    {
        // A ordem da busca e' o que separa "o ministerio" de "uma secretaria dentro de
        // outro ministerio com o mesmo nome".
        var repositorio = new RepositorioFalso(
        [
            Unidade(26, "Ministério do Exemplo", topo: true),
            Unidade(9001, "Ministério do Exemplo", topo: false),
        ]);

        var resultado = await Coletar(repositorio, [Vinculo(30000, "Ministério do Exemplo")]);

        resultado.OrgaosLigadosAoGrafo.ShouldBe(1);
        repositorio.Gravadas.Single().CodigoSiorg.ShouldBe(26);
    }

    [Fact]
    public async Task Unidade_interna_serve_quando_nao_ha_orgao_de_topo_com_o_nome()
    {
        // E' assim que os comandos do Exercito, da Marinha e da Aeronautica encontram par:
        // eles sao unidades administrativas, nao orgaos.
        var repositorio = new RepositorioFalso(
        [
            Unidade(26, "Presidência da República", topo: true),
            Unidade(94, "Comando do Exército", topo: false),
        ]);

        var resultado = await Coletar(repositorio, [Vinculo(16000, "Comando do Exército")]);

        repositorio.Gravadas.Single().CodigoSiorg.ShouldBe(94);
        resultado.OrgaosLigadosAoGrafo.ShouldBe(1);
    }

    [Fact]
    public async Task A_tabela_curada_vence_a_regra_de_nome()
    {
        // A curadoria existe justamente para quando o nome mente. Aqui o SIAPE diz
        // "Ministério do Meio Ambiente" e o grafo tem "…e Mudança do Clima": a entrada
        // curada 40211 → 1927 resolve, e ela precisa ter precedencia.
        var repositorio = new RepositorioFalso(
        [
            Unidade(1927, "Ministério do Meio Ambiente e Mudança do Clima"),
            Unidade(7777, "Ministério do Meio Ambiente"),
        ]);

        var resultado = await Coletar(repositorio, [Vinculo(40211, "MINISTERIO DO MEIO AMBIENTE")]);

        repositorio.Gravadas.Single().CodigoSiorg.ShouldBe(1927);
        resultado.LigadosPorExcecaoCurada.ShouldBe(1);
    }

    [Fact]
    public async Task Orgao_sem_par_entra_na_base_e_aparece_no_resumo()
    {
        // Nao casar e' um desfecho legitimo — os governos dos ex-territorios, por exemplo,
        // nao existem no SIORG. O numero nao pode sumir por causa disso, e o resumo precisa
        // dizer quem ficou de fora para alguem poder investigar.
        var repositorio = new RepositorioFalso([Unidade(26, "Presidência da República")]);

        var resultado = await Coletar(repositorio,
        [
            Vinculo(40801, "Governo do Ex-Território Federal do Amapá"),
            Vinculo(40801, "Governo do Ex-Território Federal do Amapá"),
        ]);

        resultado.OrgaosLigadosAoGrafo.ShouldBe(0);
        resultado.SemPar.ShouldHaveSingleItem();
        resultado.SemPar[0].Vinculos.ShouldBe(2);
        repositorio.Gravadas.Single().Vinculos.ShouldBe(2);
    }

    [Fact]
    public async Task A_cobertura_e_medida_em_vinculos_e_nao_em_orgaos()
    {
        // Contar orgaos esconderia o que importa: cem conselhos de tres pessoas sem par
        // pesam menos que um ministerio de dez mil. A tela fala de gente.
        var repositorio = new RepositorioFalso([Unidade(26, "Presidência da República")]);

        var resultado = await Coletar(repositorio,
        [
            .. Enumerable.Repeat(Vinculo(1000, "Presidência da República"), 9),
            Vinculo(2000, "Órgão Desconhecido"),
        ]);

        resultado.CoberturaDosVinculos.ShouldBe(0.9, 0.001);
    }

    [Fact]
    public async Task O_agregado_nao_conta_contra_a_cobertura()
    {
        // "Estados / Municipios / Empresas" carrega 16.689 vinculos na base real e NUNCA
        // poderia casar, porque nao e' um orgao. Deixa-la no denominador mediria a fonte,
        // nao a qualidade da ponte — e faria a cobertura parecer pior a cada mes em que
        // essa rubrica crescesse.
        var repositorio = new RepositorioFalso([Unidade(26, "Presidência da República")]);

        var resultado = await Coletar(repositorio,
        [
            Vinculo(1000, "Presidência da República"),
            .. Enumerable.Repeat(Vinculo(99991, "Estados / Municípios / Empresas"), 50),
        ]);

        resultado.VinculosLidos.ShouldBe(51);
        resultado.VinculosDeOrgaos.ShouldBe(1);
        resultado.CoberturaDosVinculos.ShouldBe(1.0, 0.001);
    }

    // ------------------------------------------------------------------
    //  Dubles
    // ------------------------------------------------------------------

    private static Task<ResultadoDaColetaDePessoal> Coletar(
        RepositorioFalso repositorio,
        IReadOnlyList<VinculoDaFonte> vinculos) =>
        new ServicoDeColetaDePessoal(new FonteFalsa(vinculos), repositorio).ColetarAsync(Julho);

    private sealed class FonteFalsa(IReadOnlyList<VinculoDaFonte> vinculos) : IFonteDePessoal
    {
        public async IAsyncEnumerable<VinculoDaFonte> LerVinculosAsync(
            Competencia competencia,
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            foreach (var vinculo in vinculos)
            {
                yield return vinculo;
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    private sealed class RepositorioFalso(IReadOnlyList<UnidadeParaPonte> unidades)
        : IRepositorioDePessoal
    {
        public List<LotacaoDoOrgao> Gravadas { get; } = [];

        public int LimpezasPedidas { get; private set; }

        public Task<IReadOnlyList<UnidadeParaPonte>> CarregarUnidadesParaPonteAsync(
            CancellationToken cancelamento = default) => Task.FromResult(unidades);

        public Task LimparCompetenciaAsync(
            Competencia competencia,
            CancellationToken cancelamento = default)
        {
            LimpezasPedidas++;
            Gravadas.Clear();

            return Task.CompletedTask;
        }

        public void Adicionar(LotacaoDoOrgao lotacao) => Gravadas.Add(lotacao);

        public Task SalvarAsync(CancellationToken cancelamento = default) => Task.CompletedTask;
    }
}
