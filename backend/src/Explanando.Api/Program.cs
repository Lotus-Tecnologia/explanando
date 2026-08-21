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

// A API e' publica e sem autenticacao. O navegador precisa poder consumi-la de qualquer
// origem — e' dado publico —, mas so' por leitura.
builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(politica => politica
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .WithMethods("GET", "HEAD", "OPTIONS")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

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
