using Explanando.Api;
using Explanando.Application.Consultas;
using Explanando.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var conexao = builder.Configuration.GetConnectionString("Explanando")
    ?? Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
    ?? "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev";

builder.Services.AdicionarInfraestrutura(conexao);
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();

// Enum sai por NOME, nao por numero. "situacao": 4 obriga o consumidor a conhecer a ordem
// de um enum C# que ele nao ve — e a ordem pode mudar. "situacao": "SemDado" e'
// auto-descritivo, e e' o que separa um contrato publico de um vazamento de detalhe interno.
builder.Services.ConfigureHttpJsonOptions(opcoes =>
    opcoes.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

// A API e' publica e sem autenticacao. O navegador precisa poder consumi-la de qualquer
// origem — e' dado publico —, mas so' por leitura.
builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica => politica
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("GET", "HEAD", "OPTIONS")));

// A API e' publica e sem autenticacao, entao o unico freio possivel e' por origem. Ver
// LimitacaoDeTaxa: o teto e' generoso para quem le e apertado para quem raspa.
builder.Services.AdicionarLimitacaoDeTaxa(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

// Depois do CORS: o preflight do navegador nao deve consumir cota de leitura.
app.UseRateLimiter();

LimitacaoDeTaxa.AvisarSeFaltaChaveInterna(app);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/saude", () => Results.Ok(new { situacao = "ok" }))
    .WithName("Saude")
    .WithSummary("Sonda de disponibilidade.");

app.MapEndpointsPublicos();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposto para que o <c>WebApplicationFactory</c> dos testes de ponta a ponta consiga
/// referenciar a aplicação. Sem isto, a classe gerada pelo compilador é interna e o teste
/// não a enxerga.
/// </summary>
public partial class Program;
