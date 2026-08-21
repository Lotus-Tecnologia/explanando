using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Explanando.Api;

/// <summary>
/// O teto de requisições da API pública.
///
/// <para>A API não tem autenticação — é dado público e assim deve continuar —, e até aqui só
/// o teto de 200 itens por página a protegia. Isso bastava em <c>localhost</c>. No Cloud Run,
/// onde se paga por tempo de contêiner e o Postgres é o do plano gratuito, um raspador de
/// terceiros deixa de ser incômodo e vira fatura: "custo fixo zero" é premissa do projeto
/// (<c>ADR-0001</c>), não meta.</para>
///
/// <para><b>O objetivo não é dificultar o acesso ao dado.</b> O dado é público e queremos que
/// circule. O teto é generoso o bastante para ninguém que esteja lendo perceber, e baixo o
/// bastante para que uma varredura automatizada caiba no que o banco absorve.</para>
/// </summary>
internal static partial class LimitacaoDeTaxa
{
    /// <summary>Cabeçalho pelo qual o nosso próprio renderizador se identifica.</summary>
    public const string CabecalhoInterno = "X-Explanando-Interno";

    /// <summary>
    /// 120 por minuto — dois por segundo sustentados, com a rajada inteira disponível de
    /// saída.
    ///
    /// <para>Um leitor abrindo páginas faz alguns pedidos por minuto e nunca encosta nisso.
    /// Uma varredura das 711 mil despesas passa a levar horas em vez de minutos, que é
    /// exatamente a diferença entre uma consulta que o banco absorve e uma que ele não
    /// absorve.</para>
    /// </summary>
    private const int CapacidadePadrao = 120;

    /// <summary>
    /// O quanto o balde interno é maior que o público.
    ///
    /// <para>Nosso próprio frontend é, de longe, o cliente mais pesado que existe por IP: o
    /// build busca as três páginas de ranking e os <b>513 perfis</b> de um endereço só, com
    /// onze processos em paralelo, e a revalidação do ISR continua batendo dali depois. Sem
    /// escape, o primeiro deploy derrubaria o próprio limite — e a falha apareceria como
    /// "build quebrado", não como "teto apertado".</para>
    /// </summary>
    private const int FatorInterno = 20;

    private const string ParticaoDaSonda = "sonda";
    private const string ParticaoInterna = "interno";
    private const string SemEndereco = "sem-endereco";

    public static IServiceCollection AdicionarLimitacaoDeTaxa(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(servicos);
        ArgumentNullException.ThrowIfNull(configuracao);

        var capacidade = configuracao.GetValue("RateLimit:Capacidade", CapacidadePadrao);
        var porMinuto = configuracao.GetValue("RateLimit:PorMinuto", CapacidadePadrao);
        var atrasDeProxy = configuracao.GetValue("RateLimit:AtrasDeProxy", false);
        var chaveInterna = configuracao.GetValue<string?>("RateLimit:ChaveInterna");

        var esperaSugerida = TimeSpan.FromSeconds(
            porMinuto <= 0 ? 60 : Math.Max(1, 60d / porMinuto));

        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                contexto =>
                {
                    // A sonda fica de fora. Limitá-la faz a plataforma matar o contêiner
                    // exatamente no momento de carga em que ele precisa continuar de pé.
                    if (contexto.Request.Path.StartsWithSegments("/saude"))
                    {
                        return RateLimitPartition.GetNoLimiter(ParticaoDaSonda);
                    }

                    return EhNosso(contexto, chaveInterna)
                        ? Balde(ParticaoInterna, capacidade * FatorInterno, porMinuto * FatorInterno)
                        : Balde(IdentificarLeitor(contexto, atrasDeProxy), capacidade, porMinuto);
                });

            opcoes.OnRejected = async (contexto, cancelamento) =>
            {
                var resposta = contexto.HttpContext.Response;
                resposta.StatusCode = StatusCodes.Status429TooManyRequests;

                // **A armadilha desta etapa.** O limitador recusa antes de o endpoint rodar,
                // então o filtro que carimba Cache-Control nas leituras nunca vê este 429.
                // Sem cabeçalho, a CDN pode guardá-lo — e um 429 guardado no edge tranca
                // todo mundo que sai por aquele nó, por minutos, por causa de um raspador só.
                resposta.Headers.CacheControl = "no-store";

                var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var informada)
                    ? informada
                    : esperaSugerida;

                // Sem Retry-After o cliente educado não tem como saber que basta esperar, e o
                // comportamento natural dele passa a ser tentar de novo na hora, em laço.
                resposta.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);

                await resposta.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Requisições demais",
                        Detail =
                            "Esta API é pública e sem autenticação, então há um teto por "
                            + "origem para manter o serviço de pé. Espere os segundos "
                            + "indicados em Retry-After e tente de novo.",
                    },
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancelamento).ConfigureAwait(false);
            };
        });

        return servicos;
    }

    /// <summary>
    /// Avisa, uma vez na subida, que não há chave interna configurada.
    ///
    /// <para>Sem ela o nosso próprio renderizador disputa o teto público, e o sintoma aparece
    /// como build quebrado num deploy futuro — longe da causa. Um aviso na subida é mais
    /// barato que essa investigação.</para>
    /// </summary>
    public static void AvisarSeFaltaChaveInterna(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (string.IsNullOrWhiteSpace(app.Configuration["RateLimit:ChaveInterna"]))
        {
            Registro.SemChaveInterna(app.Logger, CabecalhoInterno);
        }
    }

    private static RateLimitPartition<string> Balde(string chave, int capacidade, int porMinuto) =>
        RateLimitPartition.GetTokenBucketLimiter(chave, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, capacidade),
            TokensPerPeriod = Math.Max(1, porMinuto),
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,

            // Fila zero: recusa na hora, não segura. Enfileirar prenderia a conexão esperando
            // uma vaga, e no Cloud Run se paga por tempo de contêiner — a fila transformaria
            // uma rajada em conta, que é justamente o que este limitador existe para evitar.
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });

    /// <summary>
    /// Quem está do outro lado, para efeito de teto.
    ///
    /// <para>Atrás de Cloudflare e Cloud Run, o IP da conexão é o do <b>proxy</b>: usá-lo
    /// jogaria todo mundo na mesma partição e o teto viraria global — a centésima pessoa a
    /// abrir a home levaria <c>429</c> por causa das noventa e nove anteriores.</para>
    ///
    /// <para>Só que o outro lado é igualmente ruim: confiar num cabeçalho que o cliente
    /// escreve dá a qualquer um uma partição nova por requisição, e o limitador vira
    /// decoração. Por isso o cabeçalho só é lido quando a configuração declara que há um
    /// proxy na frente — quem sabe disso é o desdobramento, não o código.</para>
    /// </summary>
    private static string IdentificarLeitor(HttpContext contexto, bool atrasDeProxy)
    {
        if (atrasDeProxy)
        {
            var daCloudflare = contexto.Request.Headers["CF-Connecting-IP"].ToString();

            if (!string.IsNullOrWhiteSpace(daCloudflare))
            {
                return daCloudflare.Trim();
            }

            var encaminhado = contexto.Request.Headers["X-Forwarded-For"].ToString();

            if (!string.IsNullOrWhiteSpace(encaminhado))
            {
                // O primeiro da lista é o cliente; os seguintes são os proxies do caminho.
                var cliente = encaminhado.Split(',')[0].Trim();

                if (cliente.Length > 0)
                {
                    return cliente;
                }
            }
        }

        return contexto.Connection.RemoteIpAddress?.ToString() ?? SemEndereco;
    }

    /// <summary>
    /// Comparação em tempo fixo: a chave é segredo, e comparar por igualdade de string
    /// vazaria o tamanho do prefixo acertado pelo tempo de resposta.
    /// </summary>
    private static bool EhNosso(HttpContext contexto, string? chave)
    {
        if (string.IsNullOrEmpty(chave)
            || !contexto.Request.Headers.TryGetValue(CabecalhoInterno, out var enviada))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(enviada.ToString()),
            Encoding.UTF8.GetBytes(chave));
    }

    private static partial class Registro
    {
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "RateLimit:ChaveInterna nao configurada: o proprio frontend disputa o "
                + "teto publico. Defina a chave e envie-a em {Cabecalho} a partir do "
                + "renderizador, ou o build de 513 paginas vai esbarrar no limite.")]
        public static partial void SemChaveInterna(ILogger logger, string cabecalho);
    }
}
