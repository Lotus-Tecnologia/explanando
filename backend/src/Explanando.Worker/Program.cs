using System.Globalization;
using Explanando.Application.Auditoria;
using Explanando.Application.Ingestao;
using Explanando.Domain.Regras;
using Explanando.Infrastructure;
using Explanando.Infrastructure.Persistencia;
using Explanando.Worker;
using Microsoft.EntityFrameworkCore;

var conexao = Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
    ?? "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev";

var comando = args.Length > 0 ? args[0].ToLowerInvariant() : "servico";

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AdicionarInfraestrutura(conexao);

// O EF Core registra cada INSERT em nivel Information. Numa coleta de milhares de despesas
// isso enterra o resumo em dezenas de milhares de linhas de SQL.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);
builder.Logging.AddFilter("Polly", LogLevel.Warning);

if (comando == "servico")
{
    builder.Services.AddHostedService<Worker>();
    builder.Build().Run();
    return 0;
}

using var host = builder.Build();
using var escopo = host.Services.CreateScope();
var servicos = escopo.ServiceProvider;

var contexto = servicos.GetRequiredService<ExplanandoDbContext>();
await contexto.Database.MigrateAsync().ConfigureAwait(false);

var legislatura = LerInteiro("--legislatura") ?? 57;

switch (comando)
{
    case "coletar":
        {
            var limite = LerInteiro("--limite");

            Console.WriteLine(limite is { } n
                ? $"Coletando ate {n} deputados da legislatura {legislatura}..."
                : $"Coletando TODOS os deputados da legislatura {legislatura}...");

            var relogio = new System.Diagnostics.Stopwatch();
            relogio.Start();

            var resultado = await servicos.GetRequiredService<ServicoDeColeta>()
                .ColetarTudoAsync(legislatura, limite)
                .ConfigureAwait(false);

            relogio.Stop();

            Console.WriteLine($"""

                === COLETA ===
                Deputados visitados : {resultado.DeputadosVisitados}
                Deputados pulados   : {resultado.DeputadosPulados}
                Despesas novas      : {resultado.DespesasNovas}
                Despesas rejeitadas : {resultado.DespesasRejeitadas}
                CNPJ sem consulta   : {resultado.FornecedoresNaoEnriquecidos}
                Alertas de sanidade : {resultado.Alertas.Count}
                Falhas              : {resultado.Falhas.Count}
                Tempo               : {relogio.Elapsed:hh\:mm\:ss}
                """);

            if (resultado.MotivosDeRejeicao.Count > 0)
            {
                Console.WriteLine("--- motivos de rejeicao ---");

                foreach (var (motivo, ocorrencias) in resultado.MotivosDeRejeicao)
                {
                    Console.WriteLine($"  {ocorrencias,6}  {motivo}");
                }

                Console.WriteLine();
            }

            foreach (var alerta in resultado.Alertas.Take(10))
            {
                Console.WriteLine($"  [ALERTA] {alerta}");
            }

            foreach (var falha in resultado.Falhas.Take(10))
            {
                Console.WriteLine($"  [FALHA]  {falha}");
            }

            return resultado.Falhas.Count > 0 ? 1 : 0;
        }

    case "varrer":
        {
            var varredura = servicos.GetRequiredService<ServicoDeVarredura>();

            var ids = await contexto.Parlamentares
                .AsNoTracking()
                .Select(parlamentar => parlamentar.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            Console.WriteLine($"Varrendo {ids.Count} parlamentares...");

            var totalFlags = 0;
            var totalDespesas = 0;
            var comFlag = 0;
            var semAmostra = 0;
            decimal volumeTotal = 0;
            var porRegra = new Dictionary<string, int>(StringComparer.Ordinal);
            var falhas = new List<FalhaDeRegra>();

            foreach (var id in ids)
            {
                var resultado = await varredura.VarrerAsync(id).ConfigureAwait(false);

                totalFlags += resultado.FlagsEmitidas;
                totalDespesas += resultado.DespesasAvaliadas;

                volumeTotal += resultado.VolumeTotalGasto;

                if (resultado.FlagsEmitidas > 0)
                {
                    comFlag++;
                }

                if (!resultado.AmostraSuficiente)
                {
                    semAmostra++;
                }

                foreach (var (regra, quantidade) in resultado.FlagsPorRegra)
                {
                    porRegra[regra] = porRegra.GetValueOrDefault(regra) + quantidade;
                }

                falhas.AddRange(resultado.Falhas);
            }

            Console.WriteLine($"""

                === VARREDURA ===
                Parlamentares       : {ids.Count} ({comFlag} com ao menos uma flag)
                Despesas avaliadas  : {totalDespesas}
                Flags emitidas      : {totalFlags}
                Volume total gasto  : {volumeTotal:N2}
                Fora do ranking de risco (amostra insuficiente) : {semAmostra}
                Falhas de regra     : {falhas.Count}

                --- flags por regra ---
                """);

            foreach (var (regra, quantidade) in porRegra.OrderByDescending(par => par.Value))
            {
                var proporcao = totalDespesas == 0 ? 0d : (double)quantidade / totalDespesas * 100;
                Console.WriteLine($"  {regra,-14} {quantidade,7}   ({proporcao,6:F3}% das despesas)");
            }

            // Passada de fechamento: as regras que cruzam parlamentares so' podem rodar
            // depois que todos foram varridos.
            var rede = servicos.GetRequiredService<ServicoDeVarreduraDeRede>();
            var resultadoDeRede = await rede.VarrerAsync().ConfigureAwait(false);

            var avisoDeAmostra = resultadoDeRede.BaseSuficiente
                ? string.Empty
                : $"  [ABAIXO do piso de {resultadoDeRede.PisoDeParlamentares}: "
                  + "os achados abaixo sao de amostra e nao devem ser publicados]";

            Console.WriteLine();
            Console.WriteLine("--- passada de rede (cruza parlamentares) ---");
            Console.WriteLine(
                $"Parlamentares na base : {resultadoDeRede.ParlamentaresNaBase}{avisoDeAmostra}");

            foreach (var (regra, quantidade) in resultadoDeRede.FlagsPorRegra)
            {
                Console.WriteLine($"  {regra,-14} {quantidade,7}");
            }

            foreach (var falha in resultadoDeRede.Falhas)
            {
                Console.WriteLine($"  [FALHA]  {falha.CodigoRegra}: {falha.Motivo}");
            }

            falhas.AddRange(resultadoDeRede.Falhas);

            return falhas.Count > 0 ? 1 : 0;
        }

    default:
        Console.Error.WriteLine($"""
            Comando desconhecido: '{comando}'.

            Uso:
              dotnet run -- servico                       roda como BackgroundService
              dotnet run -- coletar [--legislatura 57] [--limite N]
              dotnet run -- varrer  [--legislatura 57]
            """);
        return 2;
}

int? LerInteiro(string nome)
{
    var indice = Array.IndexOf(args, nome);

    return indice >= 0 && indice + 1 < args.Length
        && int.TryParse(args[indice + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
        ? valor
        : null;
}
