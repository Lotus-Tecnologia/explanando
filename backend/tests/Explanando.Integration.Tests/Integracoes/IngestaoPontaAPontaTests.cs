using Explanando.Application.Ingestao;
using Explanando.Infrastructure.Integracoes.BrasilApi;
using Explanando.Infrastructure.Integracoes.Camara;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// Fluxo completo da Etapa 03: fonte HTTP (servida pelas respostas reais capturadas na
/// Etapa 01) -> traducao -> dominio -> Postgres de verdade.
///
/// Os testes unitarios provam que cada peca esta certa. Este prova que elas encaixam —
/// e' onde aparecem erro de mapeamento, restricao de banco e conversao de tipo, que
/// nenhum duble em memoria pega.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class IngestaoPontaAPontaTests(BancoDeTestes banco)
{
    private const int Legislatura = 57;
    private static int _sequencia;

    private static DeputadoDaFonte NovoDeputado() => new()
    {
        IdExterno = Interlocked.Increment(ref _sequencia) + 800_000,
        NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
        NomeParlamentar = "Acácio Favacho",
        SiglaPartido = "MDB",
        Uf = "AP",
        EmExercicio = true,
    };

    private ServicoDeIngestao MontarServico(out ExplanandoDbContext contexto)
    {
        contexto = banco.CriarContexto();

        var camara = new ClienteCamara(new HttpClient(
            new ManipuladorDeFixtures().QuandoContiverUmaVez("/despesas", "camara-despesas.json"))
        {
            BaseAddress = new Uri("https://dadosabertos.camara.leg.br/api/v2/"),
        });

        var brasilApi = new ClienteBrasilApi(new HttpClient(
            new ManipuladorDeFixtures().QuandoContiver("/cnpj/", "brasilapi-cnpj.json"))
        {
            BaseAddress = new Uri("https://brasilapi.com.br/api/"),
        });

        return new ServicoDeIngestao(camara, brasilApi, new RepositorioDeIngestao(contexto));
    }

    [Fact]
    public async Task Ingere_deputado_despesas_fornecedor_e_socios_no_banco()
    {
        var deputado = NovoDeputado();
        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            var resultado = await servico.IngerirDeputadoAsync(deputado, Legislatura);

            resultado.DespesasNovas.ShouldBe(3);
            resultado.TemAlerta.ShouldBeFalse();
            resultado.Rejeitadas.ShouldBeEmpty();
        }

        await using var leitura = banco.CriarContexto();

        var parlamentar = await leitura.Parlamentares
            .SingleAsync(p => p.IdExterno == deputado.IdExterno);
        parlamentar.Uf.ShouldBe("AP");

        var despesas = await leitura.Despesas
            .Where(d => d.ParlamentarId == parlamentar.Id)
            .OrderBy(d => d.CodigoDocumento)
            .ToListAsync();

        despesas.Count.ShouldBe(3);

        // Terceiro padrao de dado sujo, alem do CNPJ sentinela: a passagem aerea (SIGEPA)
        // vem com `cnpjCpfFornecedor` VAZIO, mas com fornecedor nomeado ("LATAM Airlines
        // Brasil"). A despesa e' gravada com o nome e sem vinculo — o gasto publico fica
        // registrado, e as regras de KYB simplesmente se abstem sobre ela.
        var semDocumento = despesas.Single(d => d.DocumentoFornecedor.EhVazio);
        semDocumento.FornecedorId.ShouldBeNull();
        semDocumento.NomeFornecedor.ShouldBe("LATAM Airlines Brasil");
        semDocumento.ValorLiquido.ShouldBe(163.00m, "o gasto continua contabilizado");

        despesas.Where(d => !d.DocumentoFornecedor.EhVazio)
            .ShouldAllBe(d => d.FornecedorId != null);

        // As tres notas tem numeros distintos, e um deles tem 13 digitos: e' um bilhete
        // aereo (SIGEPA), nao uma nota fiscal sequencial. REG_KYB_005 procura numeracao
        // inicial baixa e vai precisar se limitar a documento fiscal — anotado para a
        // Etapa 04.
        despesas.Select(d => d.NumeroDocumento).ShouldBe(
            ["8887", "7308", "9572293573464"], ignoreOrder: true);
        despesas.ShouldAllBe(d => d.NumeroDocumento.Length > 0);

        // codDocumento nem sempre e' numerico: a passagem aerea usa GUID.
        despesas.ShouldContain(d => d.CodigoDocumento.Contains('-', StringComparison.Ordinal));

        var comFornecedor = despesas.First(d => d.FornecedorId is not null);
        var fornecedor = await leitura.Fornecedores.SingleAsync(f => f.Id == comFornecedor.FornecedorId);

        fornecedor.RazaoSocial.ShouldBe("AUTO POSTO BOMTEMPO LTDA");
        fornecedor.CnaesSecundarios.Count.ShouldBe(4);
        fornecedor.PossuiCnae("4732600").ShouldBeTrue();
        fornecedor.Endereco.Uf.ShouldBe("GO");

        var socios = await leitura.Sociedades
            .Where(s => s.FornecedorId == fornecedor.Id)
            .ToListAsync();

        socios.Count.ShouldBe(2);
        socios.ShouldContain(s => s.Cpf.DigitosVisiveis == "456789");
    }

    [Fact]
    public async Task Reexecutar_a_coleta_nao_duplica_nada_no_banco()
    {
        // A prova de que a idempotencia funciona contra o banco, e nao so' contra a
        // colecao em memoria dos testes unitarios: aqui existe o indice unico de verdade.
        var deputado = NovoDeputado();

        var primeiro = MontarServico(out var contextoA);
        await using (contextoA)
        {
            (await primeiro.IngerirDeputadoAsync(deputado, Legislatura)).DespesasNovas.ShouldBe(3);
        }

        var segundo = MontarServico(out var contextoB);
        await using (contextoB)
        {
            var resultado = await segundo.IngerirDeputadoAsync(deputado, Legislatura);

            resultado.DespesasNovas.ShouldBe(0);
            resultado.DespesasJaConhecidas.ShouldBe(3);
            resultado.FornecedoresNovos.ShouldBe(0, "o fornecedor ja estava no banco");
        }

        await using var leitura = banco.CriarContexto();
        var parlamentar = await leitura.Parlamentares.SingleAsync(p => p.IdExterno == deputado.IdExterno);

        (await leitura.Despesas.CountAsync(d => d.ParlamentarId == parlamentar.Id)).ShouldBe(3);
        (await leitura.Parlamentares.CountAsync(p => p.IdExterno == deputado.IdExterno)).ShouldBe(1);
    }

    [Fact]
    public async Task Coleta_vazia_para_deputado_em_exercicio_alarma_e_nao_grava_nada()
    {
        var deputado = NovoDeputado();

        await using var contexto = banco.CriarContexto();

        var camaraSemDados = new ClienteCamara(new HttpClient(
            new ManipuladorDeFixtures().QuandoContiver(
                "/despesas", System.Net.HttpStatusCode.OK, """{"dados":[],"links":[]}"""))
        {
            BaseAddress = new Uri("https://dadosabertos.camara.leg.br/api/v2/"),
        });

        var servico = new ServicoDeIngestao(
            camaraSemDados,
            new ClienteBrasilApi(new HttpClient(new ManipuladorDeFixtures())
            {
                BaseAddress = new Uri("https://brasilapi.com.br/api/"),
            }),
            new RepositorioDeIngestao(contexto));

        var resultado = await servico.IngerirDeputadoAsync(deputado, Legislatura);

        resultado.TemAlerta.ShouldBeTrue();
        resultado.AlertasDeSanidade[0].ShouldContain("idLegislatura");
        resultado.DespesasNovas.ShouldBe(0);

        await using var leitura = banco.CriarContexto();
        var parlamentar = await leitura.Parlamentares.SingleAsync(p => p.IdExterno == deputado.IdExterno);
        (await leitura.Despesas.CountAsync(d => d.ParlamentarId == parlamentar.Id)).ShouldBe(0);
    }
}
