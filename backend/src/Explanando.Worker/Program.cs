using System.Globalization;
using Explanando.Application.Auditoria;
using Explanando.Application.Ciclo;
using Explanando.Application.Retencao;
using Explanando.Application.Ingestao;
using Explanando.Domain.Regras;
using Explanando.Domain.Retencao;
using Explanando.Infrastructure;
using Explanando.Infrastructure.Integracoes.Senado;
using Explanando.Infrastructure.Persistencia;
using Explanando.Domain.Parlamentares;
using Explanando.Worker;
using Microsoft.EntityFrameworkCore;

var conexao = Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
    ?? "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev";

var comando = args.Length > 0 ? args[0].ToLowerInvariant() : "servico";

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AdicionarInfraestrutura(conexao);
builder.Services.AdicionarObservabilidade(builder.Configuration, "explanando-worker");

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

            if (resultado.ListagemIncompleta)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "*** LISTAGEM INCOMPLETA: a fonte parou de entregar a lista de "
                    + "deputados. ***");
                Console.WriteLine(
                    "    O que foi coletado esta salvo. Rode de novo para alcancar o resto.");
                return 3;
            }

            if (resultado.InterrompidaPorFonteSuspeita)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "*** COLETA INTERROMPIDA: a origem respondeu 200 com lista vazia em "
                    + "sequencia. ***");
                Console.WriteLine(
                    "    Nenhum dado foi perdido — os checkpoints com alerta serao "
                    + "retentados na proxima passada.");
                Console.WriteLine("    Rode de novo quando /despesas voltar a responder.");
                return 2;
            }

            return resultado.Falhas.Count > 0 ? 1 : 0;
        }

    case "coletar-senado":
        {
            var limite = LerInteiro("--limite");
            var fonte = servicos.GetRequiredService<ClienteDoSenado>();
            var anos = ClienteDoSenado.AnosDaLegislatura(legislatura, DateTimeOffset.UtcNow.Year);

            Console.WriteLine(
                $"Coletando a CEAPS da legislatura {legislatura} "
                + $"({string.Join(", ", anos)})...");

            // A CEAPS entrega o ano inteiro num payload so'; isto ja baixa tudo e agrupa.
            var senadores = new List<int>();

            await foreach (var senador in fonte.ListarDeputadosAsync().ConfigureAwait(false))
            {
                senadores.Add(senador.IdExterno);
            }

            Console.WriteLine($"{senadores.Count} senadores com despesa na janela.");

            // ---------------------------------------------------------------
            //  Guarda de colisao de checkpoint
            //
            //  `parlamentares` e' unico por (IdExterno, Tipo) e esta correto. Mas
            //  `checkpoints_ingestao` e' unico por (IdExternoParlamentar, Legislatura), SEM a
            //  casa — ele foi desenhado quando so' havia deputados. Um codSenador igual ao
            //  IdExterno de um deputado faria os dois compartilharem checkpoint, e a coleta
            //  de um pularia a do outro em silencio.
            //
            //  Hoje as faixas nem se tocam — senadores vao a 6.381, deputados comecam em
            //  62.881 —, entao a colisao e' impossivel na pratica e nao vale uma migration em
            //  producao. Mas "impossivel na pratica" envelhece sem avisar: esta consulta
            //  custa milissegundos e transforma o dia em que envelhecer num erro claro, em
            //  vez de dado trocado.
            // ---------------------------------------------------------------
            var colisoes = await contexto.Parlamentares
                .AsNoTracking()
                .Where(parlamentar => parlamentar.Tipo == TipoParlamentar.DeputadoFederal
                    && senadores.Contains(parlamentar.IdExterno))
                .Select(parlamentar => parlamentar.IdExterno)
                .ToListAsync()
                .ConfigureAwait(false);

            if (colisoes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "*** COLETA ABORTADA: codigo de senador colide com id de deputado. ***");
                Console.WriteLine($"    Codigos: {string.Join(", ", colisoes.Take(20))}");
                Console.WriteLine(
                    "    A chave de checkpoints_ingestao nao tem a casa. Antes de coletar, "
                    + "acrescente o tipo a essa chave.");
                return 4;
            }

            var ingestaoDoSenado = new ServicoDeIngestao(
                fonte,
                servicos.GetRequiredService<IFonteDeCadastroEmpresarial>(),
                servicos.GetRequiredService<IRepositorioDeIngestao>());

            var coletaDoSenado = new ServicoDeColeta(
                fonte,
                ingestaoDoSenado,
                servicos.GetRequiredService<IRepositorioDeCheckpoints>(),
                servicos.GetRequiredService<IRelogio>());

            var cronometro = System.Diagnostics.Stopwatch.StartNew();

            var saida = await coletaDoSenado
                .ColetarTudoAsync(legislatura, limite)
                .ConfigureAwait(false);

            cronometro.Stop();

            Console.WriteLine($"""

                === COLETA DO SENADO ===
                Senadores visitados : {saida.DeputadosVisitados}
                Senadores pulados   : {saida.DeputadosPulados}
                Despesas novas      : {saida.DespesasNovas}
                Despesas rejeitadas : {saida.DespesasRejeitadas}
                CNPJ sem consulta   : {saida.FornecedoresNaoEnriquecidos}
                Alertas de sanidade : {saida.Alertas.Count}
                Falhas              : {saida.Falhas.Count}
                Tempo               : {cronometro.Elapsed:hh\:mm\:ss}
                """);

            foreach (var (motivo, ocorrencias) in saida.MotivosDeRejeicao)
            {
                Console.WriteLine($"  {ocorrencias,6}  {motivo}");
            }

            foreach (var alerta in saida.Alertas.Take(10))
            {
                Console.WriteLine($"  [ALERTA] {alerta}");
            }

            foreach (var falha in saida.Falhas.Take(10))
            {
                Console.WriteLine($"  [FALHA]  {falha}");
            }

            if (saida.InterrompidaPorFonteSuspeita)
            {
                Console.WriteLine();
                Console.WriteLine("*** COLETA INTERROMPIDA: a origem ficou suspeita. ***");
                Console.WriteLine("    Nada se perdeu; os checkpoints com alerta serao retentados.");
                return 2;
            }

            return saida.Falhas.Count > 0 ? 1 : 0;
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

            // Falha de regra e' defeito a investigar, nunca ruido: a passada fica
            // incompleta naquele codigo e ninguem descobre pelo total. Imprimir so' a
            // contagem, como era antes, escondia exatamente o que precisa ser lido.
            if (falhas.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("--- falhas de regra (a passada esta incompleta nelas) ---");

                foreach (var grupo in falhas
                    .GroupBy(falha => $"{falha.CodigoRegra}: {falha.Motivo}", StringComparer.Ordinal)
                    .OrderByDescending(grupo => grupo.Count()))
                {
                    Console.WriteLine($"  {grupo.Count(),5}x  {grupo.Key}");
                }
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

    case "ciclo-mensal":
        {
            // A passada completa e gratuita do ADR-0010 §4: coleta, varre, cruza a rede e
            // consolida. E' o comando que um agendador externo dispara — Cloud Scheduler
            // chamando um Cloud Run Job, que sobe, trabalha e morre. Um servico de pe
            // esperando um mes passar contradiz o scale-to-zero em que o custo do projeto
            // se apoia.
            var forcar = Array.IndexOf(args, "--forcar") >= 0;
            var ciclo = servicos.GetRequiredService<ServicoDeCicloMensal>();

            var relogio = new System.Diagnostics.Stopwatch();
            relogio.Start();

            var resultado = forcar
                ? await ciclo.ExecutarAsync(legislatura).ConfigureAwait(false)
                : await ciclo
                    .ExecutarSeVencidoAsync(legislatura, CicloDeAtualizacao.IntervaloPadrao)
                    .ConfigureAwait(false);

            relogio.Stop();

            if (!resultado.Executou)
            {
                Console.WriteLine($"""

                    === CICLO MENSAL ===
                    Nada a fazer: a base inteira foi varrida ha menos de {CicloDeAtualizacao.IntervaloPadrao.TotalDays:F0} dias.

                    O vencimento olha para o parlamentar MAIS ATRASADO, e nao para o mais
                    recente: com o mais recente, uma varredura paga por mes manteria a base
                    "atualizada" para sempre enquanto os outros 512 envelheciam.

                    Use --forcar para rodar assim mesmo.
                    """);

                return 0;
            }

            Console.WriteLine($"""

                === CICLO MENSAL ===
                Despesas novas      : {resultado.Coleta?.DespesasNovas ?? 0}
                Parlamentares       : {resultado.ParlamentaresVarridos}
                Flags emitidas      : {resultado.FlagsEmitidas}
                Consolidados        : {resultado.Retencao?.ConsolidadosCriados ?? 0} criados, {resultado.Retencao?.ConsolidadosAtualizados ?? 0} atualizados
                Falhas de regra     : {resultado.Falhas.Count}
                Tempo               : {relogio.Elapsed:hh\:mm\:ss}
                """);

            if (resultado.ColetaIncompleta)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "*** A COLETA FICOU INCOMPLETA. A varredura rodou sobre o que existe no "
                    + "banco, ***");
                Console.WriteLine(
                    "    mas o dado esta mais velho do que este resumo sugere. Rode de novo.");

                return 2;
            }

            return resultado.Falhas.Count > 0 ? 1 : 0;
        }

    case "consolidar":
        {
            // Sem --meses, so' consolida. A poda e' destrutiva e nunca acontece por
            // omissao: apagar gasto publico tem de ser um ato explicito.
            var meses = LerInteiro("--meses");
            var janela = meses is { } m ? JanelaDeRetencao.De(m) : JanelaDeRetencao.Ilimitada;

            var retencao = servicos.GetRequiredService<ServicoDeRetencao>();

            Console.WriteLine(janela.GuardaTudo
                ? "Consolidando o gasto mensal. Nenhuma despesa sera apagada."
                : $"Consolidando e podando: preserva {meses} meses de detalhe.");

            var resultado = await retencao.AplicarAsync(janela).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("=== RETENCAO ===");
            Console.WriteLine($"Parlamentares vistos    : {resultado.ParlamentaresVistos}");
            Console.WriteLine($"Consolidados criados    : {resultado.ConsolidadosCriados}");
            Console.WriteLine($"Consolidados atualizados: {resultado.ConsolidadosAtualizados}");

            if (resultado.Podou)
            {
                Console.WriteLine($"Meses sem detalhe       : {resultado.MesesSemDetalhe}");
                Console.WriteLine($"Despesas descartadas    : {resultado.DespesasDescartadas}");
                Console.WriteLine(
                    "  Os totais mensais sobrevivem; o detalhe nota a nota desses meses nao.");
            }

            return 0;
        }

    default:
        Console.Error.WriteLine($"""
            Comando desconhecido: '{comando}'.

            Uso:
              dotnet run -- servico                       roda como BackgroundService
              dotnet run -- coletar [--legislatura 57] [--limite N]
              dotnet run -- varrer  [--legislatura 57]
              dotnet run -- consolidar [--meses N]     sem --meses nao apaga nada
              dotnet run -- ciclo-mensal [--forcar]    a passada completa do ADR-0010 §4
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
