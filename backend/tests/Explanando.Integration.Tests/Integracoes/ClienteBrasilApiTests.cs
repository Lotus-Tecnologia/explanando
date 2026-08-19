using System.Net;
using Explanando.Infrastructure.Integracoes.BrasilApi;

namespace Explanando.Integration.Tests.Integracoes;

public sealed class ClienteBrasilApiTests
{
    private const string CnpjReal = "36826246000164";

    private static ClienteBrasilApi Cliente(ManipuladorDeFixtures manipulador) =>
        new(new HttpClient(manipulador) { BaseAddress = new Uri("https://brasilapi.com.br/api/") });

    [Fact]
    public async Task Le_o_cadastro_real_com_os_campos_que_o_motor_usa()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver($"/cnpj/v1/{CnpjReal}", "brasilapi-cnpj.json");

        var empresa = await Cliente(manipulador).ConsultarAsync(CnpjReal);

        empresa.ShouldNotBeNull();
        empresa.RazaoSocial.ShouldBe("AUTO POSTO BOMTEMPO LTDA");
        empresa.CnaePrincipal.ShouldBe("4731800");
        empresa.CnaesSecundarios.Count.ShouldBe(4, "REG_KYB_001 depende dos secundarios");
        empresa.DescricaoSituacaoCadastral.ShouldBe("ATIVA");
        empresa.DataInicioAtividade.ShouldBe(new DateOnly(1991, 7, 23));
        empresa.Uf.ShouldBe("GO");
        empresa.CodigoMunicipioIbge.ShouldBe(5208707);
    }

    [Fact]
    public async Task Le_o_quadro_societario_com_o_cpf_parcial()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver($"/cnpj/v1/{CnpjReal}", "brasilapi-cnpj.json");

        var empresa = await Cliente(manipulador).ConsultarAsync(CnpjReal);

        empresa.ShouldNotBeNull();
        empresa.QuadroSocietario.Count.ShouldBe(2);

        var socio = empresa.QuadroSocietario[0];
        socio.NomeSocio.ShouldBe("FULANO DE TAL PRIMEIRO SOCIO");
        socio.CpfMascarado.ShouldBe("***456789**");
        socio.DataEntradaSociedade.ShouldBe(new DateOnly(2011, 7, 18));
    }

    [Fact]
    public async Task Faixa_etaria_do_socio_nao_atravessa_a_fronteira()
    {
        // A fonte entrega `faixa_etaria`. Nao alimenta regra nenhuma, e o que nao e'
        // desserializado nao pode ser persistido por descuido (LGPD art. 6, III).
        typeof(Application.Ingestao.SocioDaFonte)
            .GetProperties()
            .ShouldNotContain(p => p.Name.Contains("Faixa", StringComparison.OrdinalIgnoreCase));

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Opcao_pelo_mei_nula_na_fonte_continua_nula()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver($"/cnpj/v1/{CnpjReal}", "brasilapi-cnpj.json");

        var empresa = await Cliente(manipulador).ConsultarAsync(CnpjReal);

        empresa.ShouldNotBeNull();
        empresa.OptantePeloMei.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  Comportamento diante de dado ruim
    // ------------------------------------------------------------------

    [Fact]
    public async Task Nao_faz_requisicao_para_o_cnpj_sentinela()
    {
        // A fonte responde 400 para "00000000000001" (docs/contratos/brasilapi-cnpj-invalido.json).
        // Consultar seria queimar requisicao de um servico comunitario e poluir o log de erro
        // em 2% de todas as despesas.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", "brasilapi-cnpj.json");

        var empresa = await Cliente(manipulador).ConsultarAsync("00000000000001");

        empresa.ShouldBeNull();
        manipulador.UrlsChamadas.ShouldBeEmpty("nem chegou a sair da maquina");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("123")]
    [InlineData("36826246000165")]
    public async Task Nao_faz_requisicao_para_documento_invalido(string? invalido)
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", "brasilapi-cnpj.json");

        (await Cliente(manipulador).ConsultarAsync(invalido!)).ShouldBeNull();
        manipulador.UrlsChamadas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cnpj_desconhecido_pela_fonte_devolve_nulo()
    {
        // CNPJ valido mas ausente do cadastro e' rotina, nao excecao: a despesa continua
        // sendo persistida, apenas sem enriquecimento.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", HttpStatusCode.NotFound);

        (await Cliente(manipulador).ConsultarAsync(CnpjReal)).ShouldBeNull();
    }

    [Fact]
    public async Task Resposta_400_da_fonte_devolve_nulo_em_vez_de_explodir()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", HttpStatusCode.BadRequest, Fixtures.Ler("brasilapi-cnpj-invalido.json"));

        (await Cliente(manipulador).ConsultarAsync(CnpjReal)).ShouldBeNull();
    }

    [Fact]
    public async Task Erro_de_servidor_propaga_para_a_politica_de_resiliencia()
    {
        // 5xx e' instabilidade temporaria: quem decide reexecutar e' o Polly, nao o cliente.
        // Engolir aqui marcaria o fornecedor como "sem cadastro" por causa de um soluco.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(
            () => Cliente(manipulador).ConsultarAsync(CnpjReal));
    }

    [Fact]
    public async Task Rate_limit_propaga_para_a_politica_de_resiliencia()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/cnpj/", HttpStatusCode.TooManyRequests);

        await Should.ThrowAsync<HttpRequestException>(
            () => Cliente(manipulador).ConsultarAsync(CnpjReal));
    }
}
