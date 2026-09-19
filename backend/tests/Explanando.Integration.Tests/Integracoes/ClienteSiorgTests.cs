using System.Net;
using Explanando.Domain.Estado;
using Explanando.Infrastructure.Integracoes.Siorg;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// A esteira do SIORG contra as respostas reais congeladas em <c>docs/contratos/</c>.
///
/// <para>As fixtures foram escolhidas <b>pelas armadilhas</b>: a raiz sem poder e sem
/// esfera, o órgão de outra esfera de governo, as duas grafias do mesmo código, a recusa
/// que chega com <c>HTTP 200</c> e a recusa que é esperada e não é defeito.</para>
/// </summary>
public sealed class ClienteSiorgTests
{
    private static ClienteSiorg Cliente(ManipuladorDeFixtures rotas) =>
        new(
            new HttpClient(rotas)
            {
                BaseAddress = new Uri("https://estruturaorganizacional.dados.gov.br/doc/"),
            })
        {
            // Zerar as esperas: um teste que aguarda de verdade nao prova nada alem de
            // paciencia, e a repeticao real e' de segundos.
            IntervaloEntreChamadas = TimeSpan.Zero,
            EsperaBase = TimeSpan.Zero,
        };

    // ------------------------------------------------------------------
    //  O codigo chega como URI, e em duas grafias
    // ------------------------------------------------------------------

    [Fact]
    public void A_grafia_por_extenso_e_a_numerica_dao_o_mesmo_resultado()
    {
        // Medido: /unidade-organizacional/26 devolve ".../poder/executivo" e
        // /estrutura-organizacional devolve ".../poder/1" para a MESMA unidade. Um
        // conversor que soubesse so' uma das grafias devolveria nulo silencioso na outra.
        var porExtenso = ConversorDoSiorg
            .ConverterUnidade(Fixtures.Ler("siorg-unidade-presidencia.json"))
            .Unidades.Single();

        var numerica = ConversorDoSiorg
            .ConverterLista(Fixtures.Ler("siorg-estrutura-presidencia.json"))
            .Unidades.Single(unidade => unidade.Codigo == 26);

        porExtenso.Poder.ShouldBe(Poder.Executivo);
        numerica.Poder.ShouldBe(Poder.Executivo);

        porExtenso.Esfera.ShouldBe(numerica.Esfera);
        porExtenso.NaturezaJuridica.ShouldBe(numerica.NaturezaJuridica);
        porExtenso.Tipo.ShouldBe(numerica.Tipo);
        porExtenso.CodigoPai.ShouldBe(numerica.CodigoPai);
    }

    [Fact]
    public void A_raiz_do_Estado_atravessa_sem_poder_sem_esfera_e_sem_sigla()
    {
        // A unidade 200000 e' a Republica Federativa do Brasil, e a fonte devolve
        // codigoPoder e codigoEsfera como STRING VAZIA e a sigla como um espaco. Exigir
        // esses campos recusaria a raiz da arvore inteira.
        var raiz = ConversorDoSiorg
            .ConverterUnidade(Fixtures.Ler("siorg-unidade-raiz.json"))
            .Unidades.Single();

        raiz.Codigo.ShouldBe(200000);
        raiz.Nome.ShouldBe("República Federativa do Brasil");
        raiz.Poder.ShouldBeNull();
        raiz.Esfera.ShouldBeNull();
        raiz.Sigla.ShouldBeNull();
        raiz.CodigoPai.ShouldBeNull();
        raiz.Tipo.ShouldBe(TipoDeUnidade.Ente);
    }

    [Fact]
    public void A_acentuacao_sobrevive_a_travessia()
    {
        // Num grafo em portugues, ler UTF-8 errado aparece no primeiro no' e em todos os
        // outros. Barato de testar, caro de descobrir depois de publicado.
        var unidades = ConversorDoSiorg
            .ConverterLista(Fixtures.Ler("siorg-orgaos-amostra.json"))
            .Unidades;

        unidades.ShouldContain(unidade => unidade.Nome == "Presidência da República");
        unidades.ShouldNotContain(unidade => unidade.Nome.Contains('�', StringComparison.Ordinal));
    }

    [Fact]
    public void Os_quatro_poderes_sao_reconhecidos()
    {
        // A amostra foi montada com um orgao de cada poder de proposito: o grafo desenha os
        // setores por este campo, e um poder que caisse em nulo sumiria do desenho.
        var unidades = ConversorDoSiorg
            .ConverterLista(Fixtures.Ler("siorg-orgaos-amostra.json"))
            .Unidades;

        unidades.Select(unidade => unidade.Poder).Distinct().Order().ShouldBe(
            [Poder.Executivo, Poder.Legislativo, Poder.Judiciario, Poder.FuncoesEssenciaisAJustica]);
    }

    [Fact]
    public void A_esfera_estadual_chega_marcada_em_vez_de_disfarcada()
    {
        // O Governo do Distrito Federal esta' na lista de orgaos federais da fonte. Quem
        // decide se ele entra no grafo e' a coleta — mas so' consegue decidir se o campo
        // chegar ate' la'.
        var gdf = ConversorDoSiorg
            .ConverterLista(Fixtures.Ler("siorg-orgaos-amostra.json"))
            .Unidades.Single(unidade => unidade.Codigo == 200002);

        gdf.Esfera.ShouldBe(EsferaDeGoverno.EstadualOuDistrital);
    }

    // ------------------------------------------------------------------
    //  HTTP 200 que nao e' sucesso
    // ------------------------------------------------------------------

    [Fact]
    public async Task Recusa_por_concorrencia_e_repetida_ate_passar()
    {
        // O SIORG aceita UMA requisicao por IP de cada vez e recusa as demais com
        // codigoErro 119 e HTTP 200. Foi assim que seis threads derrubaram 329 de 335
        // subarvores sem que nada parecesse errado.
        var servida = false;

        var rotas = new ManipuladorDeFixtures().Quando(
            uri => uri.PathAndQuery.Contains("estrutura-organizacional", StringComparison.Ordinal),
            _ =>
            {
                if (servida)
                {
                    return ManipuladorDeFixtures.RespostaFalsa.DeFixture("siorg-estrutura-presidencia.json");
                }

                servida = true;
                return ManipuladorDeFixtures.RespostaFalsa.DeFixture("siorg-erro-concorrencia.json");
            });

        var subarvore = await Cliente(rotas).ObterSubarvoreAsync(26);

        subarvore.DeuCerto.ShouldBeTrue();
        subarvore.Unidades.ShouldNotBeEmpty();
        rotas.UrlsChamadas.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Recusa_esperada_nao_vira_martelada_na_fonte()
    {
        // codigoErro 104 e' "unidade organizacional deve ser um orgao ou entidade" — a
        // resposta correta a pedir a arvore de um ENTE. Insistir cinco vezes contra um
        // servico publico por um erro que nao vai mudar e' mau comportamento.
        var rotas = new ManipuladorDeFixtures()
            .QuandoContiver("estrutura-organizacional", "siorg-erro-ente.json");

        var subarvore = await Cliente(rotas).ObterSubarvoreAsync(200000);

        subarvore.DeuCerto.ShouldBeFalse();
        subarvore.Falha.ShouldNotBeNull().ShouldContain("104");
        rotas.UrlsChamadas.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Subarvore_que_falha_devolve_falha_em_vez_de_lista_vazia()
    {
        // Esta e' a diferenca entre "o ministerio nao tem unidades" e "nao conseguimos
        // perguntar". A primeira leitura faria a coleta concluir desaparecimento em massa.
        var rotas = new ManipuladorDeFixtures()
            .QuandoContiver("estrutura-organizacional", HttpStatusCode.InternalServerError);

        var subarvore = await Cliente(rotas).ObterSubarvoreAsync(26);

        subarvore.DeuCerto.ShouldBeFalse();
        subarvore.Unidades.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sem_a_lista_de_orgaos_a_coleta_para_em_vez_de_fingir()
    {
        // Devolver lista vazia aqui produziria uma "coleta completa com zero unidades" —
        // e uma coleta completa e' justamente a que tem licenca para marcar orgaos como
        // desaparecidos. O unico desfecho seguro e' nao comecar.
        var rotas = new ManipuladorDeFixtures()
            .QuandoContiver("orgao-entidade", HttpStatusCode.ServiceUnavailable);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await Cliente(rotas).ListarOrgaosAsync());
    }

    [Fact]
    public async Task A_lista_de_orgaos_chega_inteira_numa_chamada_so()
    {
        var rotas = new ManipuladorDeFixtures()
            .QuandoContiver("orgao-entidade", "siorg-orgaos-amostra.json");

        var orgaos = await Cliente(rotas).ListarOrgaosAsync();

        orgaos.Count.ShouldBe(6);
        rotas.UrlsChamadas.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Unidade_desconhecida_volta_nula_sem_excecao()
    {
        // Ancestral que a fonte nao conhece e' rotina, nao defeito: a coleta conta e segue.
        var rotas = new ManipuladorDeFixtures()
            .QuandoContiver("unidade-organizacional", "siorg-erro-ente.json");

        (await Cliente(rotas).ObterUnidadeAsync(999999)).ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  A subarvore real
    // ------------------------------------------------------------------

    [Fact]
    public void A_subarvore_da_Presidencia_chega_com_hierarquia_e_base_legal()
    {
        var unidades = ConversorDoSiorg
            .ConverterLista(Fixtures.Ler("siorg-estrutura-presidencia.json"))
            .Unidades;

        unidades.ShouldNotBeEmpty();

        // Toda unidade da subarvore tem pai — a raiz da arvore nao e' raiz do grafo.
        unidades.ShouldAllBe(unidade => unidade.CodigoPai != null);

        // O nivel de normatizacao e' informacao de peso, nao metadado: separa o orgao que
        // precisa do Congresso para desaparecer do que some com uma assinatura.
        unidades.ShouldContain(unidade => unidade.Normatizacao == NivelDeNormatizacao.LeiOuDecreto);
        unidades.ShouldContain(unidade => unidade.Normatizacao == NivelDeNormatizacao.AtoInterno);
    }
}
