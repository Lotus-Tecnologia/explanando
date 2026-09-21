using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;
using Explanando.Infrastructure.Integracoes.Planalto;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// O leitor das cadeiras do Executivo, contra as páginas reais do Planalto.
///
/// <para><b>As duas páginas estão versionadas em <c>docs/contratos/</c> por um motivo que
/// vale mais que o teste.</b> Ler HTML é frágil: o dia em que o Planalto mudar o layout,
/// o `git diff` da fixture mostra exatamente o que mudou, e a correção do extrator vira
/// meia hora em vez de uma tarde de adivinhação.</para>
///
/// <para>O que está protegido aqui não é o número de ministros — esse muda a cada
/// reforma. É a <b>forma</b>: que o par cargo/nome seja lido corretamente, que biografia
/// não vire nome de pessoa, e que uma página que deixou de ter ministros <b>falhe</b> em
/// vez de devolver um Executivo pela metade.</para>
/// </summary>
public class PlanaltoTests
{
    private static string Ministros() => Fixtures.Ler("planalto-ministros.html");

    private static string QuemEQuem() => Fixtures.Ler("planalto-quem-e-quem.html");

    [Fact]
    public void A_pagina_de_ministros_rende_o_ministerio_inteiro()
    {
        // Eram 38 na captura. O assert e' por PISO, e nao por igualdade: uma reforma
        // ministerial nao deve quebrar o teste, mas a pagina render seis cargos deve.
        ClienteDoPlanalto.ContarMinistros(Ministros()).ShouldBeGreaterThanOrEqualTo(30);
    }

    [Fact]
    public void O_presidente_sai_do_quem_e_quem__sem_a_biografia_junto()
    {
        // A pagina traz "Fulano (nascido ...; Garanhuns/PE, em 27 de outubro de ...".
        // Sem o corte no parentese, o "ocupante" seria um paragrafo inteiro.
        var nome = ClienteDoPlanalto.NomeDoPresidente(QuemEQuem());

        nome.ShouldNotBeNullOrWhiteSpace();
        nome!.Length.ShouldBeLessThan(60);
        nome.ShouldNotContain("(");
        nome.ShouldNotContain("nascido");
    }

    [Fact]
    public async Task A_coleta_junta_a_chefia_e_a_cupula_num_resultado_so()
    {
        var coleta = await ClienteFalso(QuemEQuem(), Ministros()).LerAsync();

        coleta.DeuCerto.ShouldBeTrue(coleta.Falha);
        coleta.Fonte.ShouldBe("planalto");

        coleta.Cadeiras.ShouldContain(c => c.Camada == CamadaDeComando.Chefia);
        coleta.Cadeiras.Count(c => c.Camada == CamadaDeComando.Cupula)
            .ShouldBeGreaterThanOrEqualTo(30);

        coleta.Cadeiras.ShouldAllBe(c => c.Poder == Poder.Executivo);
    }

    [Fact]
    public async Task Toda_cadeira_chega_com_a_fonte_de_onde_foi_lida()
    {
        // Sem fonte a linha nao entra: e' o que permite a' tela dizer "verificado em tal
        // dia, nesta pagina", que e' a diferenca entre informar e afirmar.
        var coleta = await ClienteFalso(QuemEQuem(), Ministros()).LerAsync();

        coleta.Cadeiras.ShouldAllBe(c => c.Fonte.StartsWith("https://www.gov.br/planalto"));
        coleta.Cadeiras.ShouldAllBe(c => c.Origem == OrigemDaCadeira.Automatica);
    }

    [Fact]
    public async Task Nenhum_ocupante_vem_com_cara_de_paragrafo()
    {
        var coleta = await ClienteFalso(QuemEQuem(), Ministros()).LerAsync();

        foreach (var cadeira in coleta.Cadeiras)
        {
            cadeira.Ocupante.ShouldNotBeNullOrWhiteSpace();
            cadeira.Ocupante!.Length.ShouldBeLessThan(120);
        }
    }

    [Fact]
    public async Task O_cargo_vira_o_nome_do_orgao_quando_da__e_nao_e_forcado_quando_nao_da()
    {
        var coleta = await ClienteFalso(QuemEQuem(), Ministros()).LerAsync();

        // "Ministro de Estado da Fazenda" -> "Ministério da Fazenda".
        coleta.Cadeiras.ShouldContain(c => c.Orgao != null && c.Orgao.StartsWith("Ministério"));

        // "Advocacia-Geral da Uniao" nao vira "Ministerio Advocacia-Geral": ja' e' o orgao.
        var agu = coleta.Cadeiras.FirstOrDefault(c => c.Cargo.StartsWith("Advocacia-Geral"));

        if (agu is not null)
        {
            agu.Orgao.ShouldBe(agu.Cargo);
        }
    }

    [Fact]
    public async Task Pagina_sem_ministros_FALHA__em_vez_de_publicar_meio_governo()
    {
        // O modo de falhar que importa. Um mapa do Executivo com tres ministerios parece
        // completo, e por isso engana mais que um mapa vazio.
        var coleta = await ClienteFalso(QuemEQuem(), "<html><body><p>Em manutenção</p></body></html>")
            .LerAsync();

        coleta.DeuCerto.ShouldBeFalse();
        coleta.Falha.ShouldNotBeNull().ShouldContain("abaixo do piso");
        coleta.Cadeiras.ShouldBeEmpty();
    }

    [Fact]
    public async Task Portal_fora_do_ar_e_falha__e_nao_lista_vazia()
    {
        var http = new HttpClient(new ManipuladorMudo()) { BaseAddress = new Uri("https://www.gov.br") };

        var coleta = await new ClienteDoPlanalto(http).LerAsync();

        coleta.DeuCerto.ShouldBeFalse();
        coleta.Falha.ShouldNotBeNull().ShouldContain("não respondeu");
    }

    private static ClienteDoPlanalto ClienteFalso(string quemEQuem, string ministros)
    {
        var manipulador = new ManipuladorDeDuasPaginas(quemEQuem, ministros);

        return new ClienteDoPlanalto(new HttpClient(manipulador));
    }

    private sealed class ManipuladorDeDuasPaginas(string quemEQuem, string ministros)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage pedido,
            CancellationToken cancelamento)
        {
            var caminho = pedido.RequestUri!.AbsolutePath;

            var corpo = caminho.Contains("quem-e-quem", StringComparison.Ordinal)
                ? quemEQuem
                : ministros;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(corpo),
            });
        }
    }

    /// <summary>Responde 404 a tudo — que é como o gov.br recusa um cliente.</summary>
    private sealed class ManipuladorMudo : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage pedido,
            CancellationToken cancelamento) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
