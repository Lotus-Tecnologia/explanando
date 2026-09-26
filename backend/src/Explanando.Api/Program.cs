using Explanando.Api;
using Explanando.Application.Consultas;
using Explanando.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// O Cloud Run — e Railway, e Heroku — injeta a porta em PORT e espera o contêiner escutando
// nela. O ASP.NET Core nao le PORT: ele le ASPNETCORE_HTTP_PORTS/ASPNETCORE_URLS. Sem esta
// ponte, mudar a porta do contêiner no painel faz o deploy falhar com "failed to start and
// listen on the port defined by the PORT variable" — erro que aponta para a plataforma
// quando a causa e' nossa. A imagem chiseled nao tem shell para expandir a variavel no
// ENTRYPOINT, entao a ponte precisa ser aqui.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } porta)
{
    builder.WebHost.UseUrls($"http://+:{porta}");
}

// O padrao de desenvolvimento so' vale em Development. Em producao, variavel faltando ou
// com nome errado fazia a API subir contra localhost com a senha de dev — sem erro, e com o
// sintoma aparecendo longe da causa. Melhor nao subir (auditoria de 2026-09-25).
var conexao = builder.Configuration.GetConnectionString("Explanando")
    ?? Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
    ?? (builder.Environment.IsDevelopment()
        ? "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev"
        : throw new InvalidOperationException(
            "Sem conexao com o banco: defina EXPLANANDO_CONEXAO (ou ConnectionStrings__Explanando)."));

builder.Services.AdicionarInfraestrutura(conexao);
builder.Services.AdicionarObservabilidade(builder.Configuration, "explanando-api");
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

// Cabecalhos de seguranca em toda resposta, inclusive erro e 429. A API so' devolve JSON,
// entao a politica pode ser a mais fechada que existe: nada carrega, nada emoldura. O HSTS
// fica na Cloudflare, que e' onde o HTTPS termina (auditoria de 2026-09-25).
app.Use(async (contexto, proximo) =>
{
    var cabecalhos = contexto.Response.Headers;
    cabecalhos.XContentTypeOptions = "nosniff";
    cabecalhos.XFrameOptions = "DENY";
    cabecalhos["Referrer-Policy"] = "no-referrer";
    cabecalhos.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    await proximo(contexto).ConfigureAwait(false);
});
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
app.MapEndpointsDoEstado();
app.MapEndpointsDaRede();
app.MapEndpointsDasCadeiras();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposto para que o <c>WebApplicationFactory</c> dos testes de ponta a ponta consiga
/// referenciar a aplicação. Sem isto, a classe gerada pelo compilador é interna e o teste
/// não a enxerga.
/// </summary>
public partial class Program;
