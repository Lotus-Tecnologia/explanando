using System.Text.Json;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;
using Explanando.Domain.Retencao;
using Explanando.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Explanando.E2E.Tests;

/// <summary>
/// Sobe a API de verdade contra um Postgres real e semeia uma base pequena mas
/// representativa.
///
/// <para>É o único nível de teste que exercita o caminho inteiro — HTTP, serialização,
/// projeção, SQL —, e por isso é onde erro de contrato aparece. Um teste de unidade sobre o
/// serviço de consulta passaria com o JSON errado.</para>
///
/// <para>A semente inclui de propósito os casos que a base real tem e que a interface precisa
/// tratar: parlamentar abaixo do piso amostral, regra que se abstém, despesa sem comprovante
/// publicado e estorno.</para>
/// </summary>
public sealed class ApiDeTestes : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("explanando_e2e")
            .WithUsername("explanando")
            .WithPassword("explanando_e2e")
            .Build();

    /// <summary>Deputado com muitas despesas: entra nos dois rankings.</summary>
    public Guid ParlamentarRanqueado { get; private set; }

    /// <summary>Deputado com poucas despesas: sai do ranking de risco, fica no de volume.</summary>
    public Guid ParlamentarSemAmostra { get; private set; }

    /// <summary>
    /// Senador. Existe para que a separação dos rankings seja provável: sem alguém da outra
    /// casa na semente, um filtro quebrado passaria nos testes sem ninguém notar.
    /// </summary>
    public Guid SenadorSemeado { get; private set; }

    /// <summary>Despesa cujo comprovante a origem não publicou.</summary>
    public Guid DespesaSemComprovante { get; private set; }

    /// <summary>Despesa com link de comprovante reconstruível.</summary>
    public Guid DespesaComComprovante { get; private set; }

    /// <summary>
    /// As mesmas opcoes que a API usa para escrever. Se o teste desserializasse com outras,
    /// ele provaria que o SEU JSON funciona, nao que o da API funciona.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// A conexão do contêiner, para um segundo host de teste apontar para a mesma base.
    ///
    /// <para>Quem precisa disso é a suíte de limitação de taxa: ela sobe a API com um teto
    /// minúsculo, e subir outro Postgres só para isso custaria mais que o teste inteiro.</para>
    /// </summary>
    public string ConexaoDeTeste => _container.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSetting("ConnectionStrings:Explanando", _container.GetConnectionString());
        builder.UseEnvironment("Development");

        // Teto alto de propósito: esta suíte tem dezenas de testes batendo na mesma API, e
        // no TestServer todos chegam sem IP — ou seja, na mesma partição. Um teto realista
        // aqui faria os testes derrubarem uns aos outros conforme a suíte crescesse. Quem
        // prova a limitação é `ApiComTetoBaixo`, com um teto que cabe num teste.
        builder.UseSetting("RateLimit:Capacidade", "100000");
        builder.UseSetting("RateLimit:PorMinuto", "100000");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var escopo = Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ExplanandoDbContext>();
        await contexto.Database.MigrateAsync();

        await SemearAsync(contexto, escopo.ServiceProvider);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    private async Task SemearAsync(ExplanandoDbContext contexto, IServiceProvider servicos)
    {
        var fornecedor = Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar("36826246000164"),
            RazaoSocial = "AUTO POSTO BOMTEMPO LTDA",
            // Ramo alheio a consultoria: dispara REG_KYB_001.
            CnaePrincipal = "4723700",
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataSituacaoCadastral = new DateOnly(2005, 9, 24),
            DataInicioAtividade = new DateOnly(1991, 7, 23),
            // OptantePeloMei fica nulo: e' o que faz REG_KYB_004 se abster, como em 65%
            // dos fornecedores reais.
            Endereco = new EnderecoFornecedor { Uf = "GO", Municipio = "GOIANIA" },
        });

        var ranqueado = Deputado(101, "Deputada Ranqueada", "AP");
        var semAmostra = Deputado(102, "Deputado Sem Amostra", "SP");
        var senador = Senador(201, "Senadora Semeada", "BA");

        // A carteira e' o que permite reconstruir o link do comprovante.
        ranqueado.RegistrarNumeroDeCarteira(1472);

        contexto.Parlamentares.AddRange(ranqueado, semAmostra, senador);
        contexto.Fornecedores.Add(fornecedor);
        contexto.LimitesCota.Add(LimiteCota.Criar("AP", 50_000m, new DateOnly(2026, 1, 1)));
        await contexto.SaveChangesAsync();

        ParlamentarRanqueado = ranqueado.Id;
        ParlamentarSemAmostra = semAmostra.Id;
        SenadorSemeado = senador.Id;

        var despesas = new List<Despesa>();

        // 120 despesas: acima do piso amostral de 100.
        for (var i = 1; i <= 120; i++)
        {
            var comLink = i % 2 == 0;

            var despesa = Despesa.Criar(new NovaDespesa
            {
                ParlamentarId = ranqueado.Id,
                DocumentoFornecedor = fornecedor.Documento,
                NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
                Competencia = Competencia.Criar(2026, (i % 6) + 1),
                TipoDespesa = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.",
                CodigoDocumento = $"7502{i:D3}",
                NumeroDocumento = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CodigoTipoDocumento = 0,
                DataDocumento = new DateTimeOffset(2026, (i % 6) + 1, 6, 0, 0, 0, TimeSpan.Zero),
                ValorDocumento = 1_000m,
                ValorLiquido = 1_000m,
                ValorGlosa = 0m,
                UrlDocumento = comLink
                    ? $"https://www.camara.leg.br/cota-parlamentar/documentos/publ/1472/2026/7502{i:D3}.pdf"
                    : null,
            });

            despesa.AssociarFornecedor(fornecedor.Id);
            despesas.Add(despesa);
        }

        // Um estorno, como 5% da base real.
        var estorno = Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = ranqueado.Id,
            DocumentoFornecedor = fornecedor.Documento,
            NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
            Competencia = Competencia.Criar(2026, 3),
            TipoDespesa = "PASSAGEM AÉREA - SIGEPA",
            CodigoDocumento = "estorno-1",
            NumeroDocumento = "999",
            CodigoTipoDocumento = 999,
            DataDocumento = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = -500m,
            ValorLiquido = -500m,
        });

        estorno.AssociarFornecedor(fornecedor.Id);
        despesas.Add(estorno);

        // O segundo deputado fica com 3 despesas: abaixo do piso.
        for (var i = 1; i <= 3; i++)
        {
            var despesa = Despesa.Criar(new NovaDespesa
            {
                ParlamentarId = semAmostra.Id,
                DocumentoFornecedor = fornecedor.Documento,
                NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
                Competencia = Competencia.Criar(2026, 5),
                TipoDespesa = "TELEFONIA",
                CodigoDocumento = $"peq-{i}",
                NumeroDocumento = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                DataDocumento = new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero),
                ValorDocumento = 200m,
                ValorLiquido = 200m,
            });

            despesa.AssociarFornecedor(fornecedor.Id);
            despesas.Add(despesa);
        }

        // Poucas despesas de propósito: o senador precisa aparecer no ranking de volume
        // sem alcançar o piso amostral, o que também prova que o piso vale por casa.
        for (var i = 1; i <= 8; i++)
        {
            var doSenador = Despesa.Criar(new NovaDespesa
            {
                ParlamentarId = senador.Id,
                DocumentoFornecedor = fornecedor.Documento,
                NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
                Competencia = Competencia.Criar(2026, i),
                TipoDespesa = "Locomoção, hospedagem, alimentação, combustíveis e lubrificantes",
                CodigoDocumento = $"sen-{i:D3}",
                NumeroDocumento = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CodigoTipoDocumento = 0,
                DataDocumento = new DateTimeOffset(2026, i, 9, 0, 0, 0, TimeSpan.Zero),
                ValorDocumento = 700m,
                ValorLiquido = 700m,

                // Nula, não zero: a CEAPS não publica glosa (ADR-0012 §2).
                ValorGlosa = null,
            });

            doSenador.AssociarFornecedor(fornecedor.Id);
            despesas.Add(doSenador);
        }

        contexto.Despesas.AddRange(despesas);
        await contexto.SaveChangesAsync();

        DespesaComComprovante = despesas.First(d => d.TemDocumentoPublicado).Id;
        DespesaSemComprovante = despesas.First(d => !d.TemDocumentoPublicado).Id;

        // Varre e consolida de verdade: assim os testes leem o que o pipeline produz, e nao
        // um estado montado a mao que poderia divergir dele.
        var varredura = servicos.GetRequiredService<Application.Auditoria.ServicoDeVarredura>();
        await varredura.VarrerAsync(ranqueado.Id);
        await varredura.VarrerAsync(semAmostra.Id);
        await varredura.VarrerAsync(senador.Id);

        var retencao = servicos.GetRequiredService<Application.Retencao.ServicoDeRetencao>();
        await retencao.AplicarAsync(JanelaDeRetencao.Ilimitada);
    }

    private static Parlamentar Deputado(int idExterno, string nome, string uf) =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = idExterno,
            NomeCivil = $"{nome} da Silva",
            NomeParlamentar = nome,
            SiglaPartido = "XYZ",
            Uf = uf,
            Tipo = TipoParlamentar.DeputadoFederal,
            FotoUrl = "https://www.camara.leg.br/foto.jpg",
        });

    private static Parlamentar Senador(int idExterno, string nome, string uf) =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = idExterno,
            NomeCivil = $"{nome} de Souza",
            NomeParlamentar = nome,
            SiglaPartido = "ABC",
            Uf = uf,
            Tipo = TipoParlamentar.Senador,
            FotoUrl = "https://www.senado.leg.br/foto.jpg",
        });
}

[CollectionDefinition(Nome)]
public sealed class ColecaoDaApi : ICollectionFixture<ApiDeTestes>
{
    public const string Nome = "api-e2e";
}
