using System.Net;
using Explanando.Infrastructure.Integracoes.Camara;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// Testa o cliente contra as respostas reais capturadas na Etapa 01, servidas no lugar da
/// rede. Cada caso aqui defende uma descoberta daquela etapa.
/// </summary>
public sealed class ClienteCamaraTests
{
    private const int LegislaturaCorrente = 57;

    private static ClienteCamara Cliente(ManipuladorDeFixtures manipulador) =>
        new(new HttpClient(manipulador)
        {
            BaseAddress = new Uri("https://dadosabertos.camara.leg.br/api/v2/"),
        });

    // ------------------------------------------------------------------
    //  A descoberta central da Etapa 01
    // ------------------------------------------------------------------

    [Fact]
    public async Task Sempre_envia_id_legislatura_ao_pedir_despesas()
    {
        // Sem idLegislatura a API responde 200 com lista vazia — nao erro. Um ingestor
        // que esqueca o parametro grava zero despesas e reporta sucesso. E' a falha mais
        // cara possivel, e este teste e' o que a impede de voltar.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiverUmaVez("/despesas", "camara-despesas.json");

        await foreach (var _ in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            break;
        }

        manipulador.UrlsChamadas.ShouldNotBeEmpty();
        manipulador.UrlsChamadas[0].Query.ShouldContain("idLegislatura=57");
    }

    [Fact]
    public async Task Pede_paginas_de_cem_itens()
    {
        // `itens` e' limitado a 100 pelo servidor: pedir mais devolve 100 do mesmo jeito.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiverUmaVez("/despesas", "camara-despesas.json");

        await foreach (var _ in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            break;
        }

        manipulador.UrlsChamadas[0].Query.ShouldContain("itens=100");
    }

    // ------------------------------------------------------------------
    //  Leitura do contrato real
    // ------------------------------------------------------------------

    [Fact]
    public async Task Le_a_despesa_real_com_todos_os_campos_que_o_motor_usa()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiverUmaVez("/despesas", "camara-despesas.json");

        var despesas = new List<Application.Ingestao.DespesaDaFonte>();

        await foreach (var despesa in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            despesas.Add(despesa);
        }

        despesas.Count.ShouldBe(3);

        var primeira = despesas[0];
        primeira.NumeroDocumento.ShouldBe("8887", "REG_KYB_005 depende deste campo");
        primeira.CnpjCpfFornecedor.ShouldBe("36826246000164");
        primeira.ValorLiquido.ShouldBe(385.92m);
        primeira.ValorDocumento.ShouldBe(385.92m);
        primeira.Ano.ShouldBe(2026);
        primeira.Mes.ShouldBe(7);
        primeira.TipoDespesa.ShouldContain("COMBUST");
        primeira.CodigoLote.ShouldBe(2253045);
    }

    [Fact]
    public async Task Data_do_documento_nao_depende_do_fuso_da_maquina()
    {
        // O contrato traz "2026-07-06T00:00:00", sem fuso. Deixar o parser assumir o fuso
        // local faria a mesma nota gerar DataDocumento diferente em maquinas diferentes —
        // e, como a data entra no HashUnico, a mesma despesa entraria duas vezes no banco
        // ao ser reprocessada em outro servidor.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiverUmaVez("/despesas", "camara-despesas.json");

        Application.Ingestao.DespesaDaFonte? primeira = null;

        await foreach (var despesa in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            primeira = despesa;
            break;
        }

        primeira.ShouldNotBeNull();
        primeira.DataDocumento.Offset.ShouldBe(TimeSpan.Zero);
        primeira.DataDocumento.ShouldBe(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Le_a_lista_de_deputados()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiverUmaVez("/deputados", "camara-deputados-lista.json");

        var deputados = new List<Application.Ingestao.DeputadoDaFonte>();

        await foreach (var deputado in Cliente(manipulador).ListarDeputadosAsync())
        {
            deputados.Add(deputado);
        }

        deputados.Count.ShouldBe(3);
        deputados[0].IdExterno.ShouldBe(204379);
        deputados[0].NomeParlamentar.ShouldBe("Acácio Favacho");
        deputados[0].Uf.ShouldBe("AP");
        deputados[0].SiglaPartido.ShouldBe("MDB");
    }

    [Fact]
    public async Task Detalhe_traz_nome_civil_e_ignora_o_cpf()
    {
        // A API devolve o CPF completo do parlamentar. Ele nao pode atravessar a fronteira
        // do cliente: nao alimenta regra nenhuma e guardar dado pessoal sem finalidade
        // contraria o principio da necessidade (LGPD art. 6, III).
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/deputados/204379", "camara-deputado-detalhe.json");

        var detalhe = await Cliente(manipulador).ObterDetalheAsync(204379);

        detalhe.ShouldNotBeNull();
        detalhe.NomeCivil.ShouldBe("ACÁCIO DA SILVA FAVACHO NETO");
        detalhe.NomeParlamentar.ShouldBe("Acácio Favacho");
        detalhe.EmExercicio.ShouldBeTrue();

        typeof(Application.Ingestao.DeputadoDaFonte)
            .GetProperties()
            .ShouldNotContain(p => p.Name.Contains("Cpf", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    //  Paginacao e falhas
    // ------------------------------------------------------------------

    [Fact]
    public async Task Segue_o_link_next_ate_a_ultima_pagina()
    {
        var chamadas = 0;
        var manipulador = new ManipuladorDeFixtures().Quando(
            uri => uri.PathAndQuery.Contains("/despesas", StringComparison.Ordinal),
            _ =>
            {
                chamadas++;
                return chamadas == 1
                    ? new ManipuladorDeFixtures.RespostaFalsa(HttpStatusCode.OK, PaginaCom("1", proxima: true))
                    : new ManipuladorDeFixtures.RespostaFalsa(HttpStatusCode.OK, PaginaCom("2", proxima: false));
            });

        var codigos = new List<string>();

        await foreach (var despesa in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            codigos.Add(despesa.CodigoDocumento);
        }

        codigos.ShouldBe(["1", "2"]);
        chamadas.ShouldBe(2, "parou quando a resposta deixou de trazer link 'next'");
    }

    [Fact]
    public async Task Pagina_vazia_encerra_a_leitura()
    {
        var manipulador = new ManipuladorDeFixtures().QuandoContiver(
            "/despesas", HttpStatusCode.OK, """{"dados":[],"links":[]}""");

        var total = 0;

        await foreach (var _ in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            total++;
        }

        total.ShouldBe(0);
    }

    [Fact]
    public async Task Erro_do_servidor_nao_e_engolido()
    {
        // Falha silenciosa aqui viraria "deputado sem despesas" no banco.
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/despesas", HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
            {
                // consome
            }
        });
    }

    [Fact]
    public async Task Deputado_inexistente_devolve_nulo_em_vez_de_explodir()
    {
        var manipulador = new ManipuladorDeFixtures()
            .QuandoContiver("/deputados/999", HttpStatusCode.NotFound);

        (await Cliente(manipulador).ObterDetalheAsync(999)).ShouldBeNull();
    }

    [Fact]
    public async Task Nao_entra_em_laco_quando_a_api_aponta_para_uma_pagina_ja_visitada()
    {
        // Uma resposta que aponte "next" para uma pagina anterior prenderia a ingestao
        // acumulando resultados ate derrubar o processo. Foi o que aconteceu de verdade
        // ao escrever estes testes, antes da guarda existir.
        var chamadas = 0;
        var manipulador = new ManipuladorDeFixtures().Quando(
            uri => uri.PathAndQuery.Contains("/despesas", StringComparison.Ordinal),
            _ =>
            {
                chamadas++;
                return new ManipuladorDeFixtures.RespostaFalsa(
                    HttpStatusCode.OK, PaginaQueApontaParaSiMesma());
            });

        var total = 0;

        await foreach (var _ in Cliente(manipulador).ListarDespesasAsync(204379, LegislaturaCorrente))
        {
            total++;
        }

        total.ShouldBe(2, "a pagina foi lida uma vez, e a repeticao do link encerrou o laco");
        chamadas.ShouldBe(2);
    }

    private static string PaginaQueApontaParaSiMesma() => $$"""
        {
          "dados": [{
            "ano": 2026, "mes": 7, "tipoDespesa": "TELEFONIA",
            "codDocumento": "circular", "codTipoDocumento": 4,
            "dataDocumento": "2026-07-06T00:00:00", "numDocumento": "1",
            "valorDocumento": 10.0, "valorLiquido": 10.0, "valorGlosa": 0.0,
            "cnpjCpfFornecedor": "36826246000164", "codLote": 1, "parcela": 0
          }],
          "links": [{"rel":"next","href":"https://dadosabertos.camara.leg.br/api/v2/deputados/204379/despesas?volta=1"}]
        }
        """;

    private static string PaginaCom(string codigoDocumento, bool proxima)
    {
        var links = proxima
            ? """[{"rel":"next","href":"https://dadosabertos.camara.leg.br/api/v2/deputados/204379/despesas?idLegislatura=57&itens=100&pagina=2"}]"""
            : "[]";

        return $$"""
            {
              "dados": [{
                "ano": 2026, "mes": 7,
                "tipoDespesa": "COMBUSTIVEIS E LUBRIFICANTES.",
                "codDocumento": "{{codigoDocumento}}",
                "tipoDocumento": "Nota Fiscal Eletronica", "codTipoDocumento": 4,
                "dataDocumento": "2026-07-06T00:00:00",
                "numDocumento": "8887",
                "valorDocumento": 385.92, "valorLiquido": 385.92, "valorGlosa": 0.0,
                "urlDocumento": "http://www.camara.leg.br/nota",
                "nomeFornecedor": "Auto Posto Bom Tempo Ltda",
                "cnpjCpfFornecedor": "36826246000164",
                "codLote": 2253045, "parcela": 0
              }],
              "links": {{links}}
            }
            """;
    }
}
