using System.Globalization;
using Explanando.Application.Auditoria;
using Explanando.Application.Ciclo;
using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Explanando.Application.Estado;
using Explanando.Application.Retencao;
using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Estado;
using Explanando.Domain.Regras;
using Explanando.Domain.Retencao;
using Explanando.Infrastructure;
using Explanando.Infrastructure.Integracoes.Senado;
using Explanando.Infrastructure.Integracoes.Siorg;
using Explanando.Infrastructure.Persistencia;
using Explanando.Domain.Parlamentares;
using Explanando.Worker;
using Microsoft.EntityFrameworkCore;

var comando = args.Length > 0 ? args[0].ToLowerInvariant() : "servico";

var builder = Host.CreateApplicationBuilder(args);

// O padrao de desenvolvimento so' vale em Development — ver o mesmo trecho na API.
var conexao = Environment.GetEnvironmentVariable("EXPLANANDO_CONEXAO")
    ?? (builder.Environment.IsDevelopment()
        ? "Host=localhost;Port=5432;Database=explanando;Username=explanando;Password=explanando_dev"
        : throw new InvalidOperationException("Sem conexao com o banco: defina EXPLANANDO_CONEXAO."));
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
    case "migrar":
        {
            // So' o esquema. As migrations ja rodaram acima, na subida de qualquer comando;
            // este existe para o compose de producao subir a API DEPOIS do esquema, sem
            // pagar uma consolidacao ou uma coleta so' para isso (ver deploy/compose.yml e
            // desdobramento.md: a API nao aplica migrations, de proposito).
            Console.WriteLine("Esquema do banco em dia.");
            return 0;
        }

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

    case "coletar-cadeiras":
        {
            // Quem ocupa as cadeiras de comando. Tres fontes independentes — o portal da
            // Presidencia (HTML), a API da Camara e a de liderancas do Senado — e cada uma
            // cai por sua conta: a falha de uma nao apaga o que as outras ja' gravaram.
            var coletaDeCadeiras = servicos.GetRequiredService<ServicoDeColetaDeCadeiras>();

            // --se-vencido e' o que a agenda usa: so' rele' as fontes paradas ha' mais de
            // uma semana. Sem a bandeira, forca tudo — que e' o que se quer ao depurar.
            var soSeVencido = Array.IndexOf(args, "--se-vencido") >= 0;

            var resumoDasCadeiras = soSeVencido
                ? await coletaDeCadeiras.ColetarSeVencidoAsync(ValidadeDaCadeira.Padrao)
                : await coletaDeCadeiras.ColetarAsync();

            if (resumoDasCadeiras.PorFonte.Count == 0)
            {
                Console.WriteLine("Nenhuma fonte vencida: nada a reler.");
                return 0;
            }

            foreach (var porFonte in resumoDasCadeiras.PorFonte)
            {
                Console.WriteLine(
                    porFonte.Falha is null
                        ? $"  {porFonte.Fonte,-12} {porFonte.Gravadas,4} cadeiras"
                        : $"  {porFonte.Fonte,-12} FALHOU: {porFonte.Falha}");
            }

            Console.WriteLine();
            Console.WriteLine(
                $"{resumoDasCadeiras.Gravadas} cadeiras gravadas, "
                + $"{resumoDasCadeiras.Falhas} fonte(s) com falha.");

            // Falha parcial nao e' sucesso, mas tambem nao apagou nada: o codigo de saida
            // avisa a agenda sem sugerir que o mapa ficou vazio.
            return resumoDasCadeiras.Falhas > 0 ? 1 : 0;
        }

    case "importar-tse":
        {
            // A eleicao, a partir dos arquivos do TSE numa pasta local. O CDN do TSE recusa
            // cliente automatizado; os arquivos se baixam com ferramentas/baixar-tse.mjs.
            var anoDaEleicao = LerInteiro("--ano") ?? 2022;
            var indiceDaPasta = Array.IndexOf(args, "--pasta");
            var pastaDoTse = indiceDaPasta >= 0 && indiceDaPasta + 1 < args.Length
                ? args[indiceDaPasta + 1]
                : Path.Combine("..", "dados", "tse");

            Console.WriteLine($"Importando a eleicao de {anoDaEleicao} de {Path.GetFullPath(pastaDoTse)}...");
            Console.WriteLine("A votacao sao ~4 GB descompactados; a leitura leva alguns minutos.");

            var importacao = new Explanando.Application.Eleicoes.ServicoDeImportacaoDoTse(
                new Explanando.Infrastructure.Integracoes.Tse.LeitorDoTse(pastaDoTse),
                servicos.GetRequiredService<Explanando.Application.Eleicoes.IRepositorioDoTse>(),
                servicos.GetRequiredService<IFonteDeParlamentares>());

            var cronometroDoTse = System.Diagnostics.Stopwatch.StartNew();
            var resultadoDoTse = await importacao.ImportarAsync(anoDaEleicao).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine($"Parlamentares na base         : {resultadoDoTse.Parlamentares}");
            Console.WriteLine($"Nomes civis completados       : {resultadoDoTse.NomesCivisCompletados}");
            Console.WriteLine($"Candidaturas ligadas          : {resultadoDoTse.CandidaturasLigadas}");
            Console.WriteLine($"Ambiguos (ficaram de fora)    : {resultadoDoTse.Ambiguos}");
            Console.WriteLine($"Municipios com voto gravados  : {resultadoDoTse.MunicipiosComVoto}");
            Console.WriteLine($"Receitas de pessoa fisica     : {resultadoDoTse.ReceitasDePessoaFisicaLidas}");
            Console.WriteLine($"  doacoes a parlamentares     : {resultadoDoTse.DoacoesParaParlamentares}");
            Console.WriteLine($"  doacoes de socios           : {resultadoDoTse.DoacoesDeSocios}");
            Console.WriteLine($"Tempo                         : {cronometroDoTse.Elapsed.TotalMinutes:F1} min");

            if (resultadoDoTse.SemCandidatura.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"--- {resultadoDoTse.SemCandidatura.Count} sem candidatura ligada em {anoDaEleicao} ---");

                foreach (var nome in resultadoDoTse.SemCandidatura)
                {
                    Console.WriteLine($"  {nome}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Rode `varrer` em seguida: REG_NET_001 so' aparece na proxima varredura.");

            return 0;
        }

    case "coletar-estado":
        {
            // O grafo do Estado — a adaptacao do CivLab ao Brasil. Nao tem legislatura nem
            // parlamentar: percorre o SIORG, monta os nos e as arestas, e deduz o que mudou
            // comparando com a coleta anterior, porque a fonte nao publica historico.
            var fonteDoEstado = servicos.GetRequiredService<ClienteSiorg>();

            var coletaDoEstado = new ServicoDeColetaDoEstado(
                fonteDoEstado,
                servicos.GetRequiredService<IRepositorioDoEstado>(),
                servicos.GetRequiredService<IRelogio>());

            // --todas-as-esferas abre o grafo para o Distrito Federal e os orgaos estaduais
            // que o SIORG tambem publica. Fica fora por padrao: sao cinco nos de outro ente,
            // e absorve-los sem escolha explicita confunde o mapa.
            var todasAsEsferas = Array.IndexOf(args, "--todas-as-esferas") >= 0;

            var escopoDeEsferas = todasAsEsferas
                ? new HashSet<EsferaDeGoverno>
                {
                    EsferaDeGoverno.Federal,
                    EsferaDeGoverno.EstadualOuDistrital,
                    EsferaDeGoverno.Municipal,
                }
                : null;

            Console.WriteLine(todasAsEsferas
                ? "Coletando a estrutura do Estado em TODAS as esferas..."
                : "Coletando a estrutura do Estado (esfera federal)...");
            Console.WriteLine(
                "A fonte aceita UMA requisicao por IP de cada vez, entao a varredura e'");
            Console.WriteLine("sequencial por natureza. Sao ~335 subarvores.");

            var cronometroDoEstado = System.Diagnostics.Stopwatch.StartNew();

            var estado = await coletaDoEstado.ColetarAsync(escopoDeEsferas).ConfigureAwait(false);

            cronometroDoEstado.Stop();

            Console.WriteLine($"""

                === ESTRUTURA DO ESTADO ===
                Orgaos visitados      : {estado.OrgaosVisitados}
                Orgaos com falha      : {estado.OrgaosComFalha}
                Orgaos fora do escopo : {estado.OrgaosForaDoEscopo}
                Unidades vistas       : {estado.UnidadesVistas}
                  novas               : {estado.UnidadesNovas}
                  atualizadas         : {estado.UnidadesAtualizadas}
                  desaparecidas       : {estado.UnidadesDesaparecidas}
                  reaparecidas        : {estado.UnidadesReaparecidas}
                  recusadas           : {estado.UnidadesRecusadas}
                Ancestrais sem ficha  : {estado.AncestraisNaoResolvidos}
                Mudancas registradas  : {estado.Mudancas}
                Repeticoes por recusa : {fonteDoEstado.RepeticoesPorRecusa}
                Tempo                 : {cronometroDoEstado.Elapsed:hh\:mm\:ss}
                """);

            if (estado.EhLinhaDeBase)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "Esta foi a LINHA DE BASE: o banco estava vazio, entao nenhuma mudanca");
                Console.WriteLine(
                    "foi anunciada. A primeira noticia so' pode existir na proxima coleta.");
            }

            foreach (var falha in estado.Falhas.Take(10))
            {
                Console.WriteLine($"  [FALHA]  {falha}");
            }

            if (!estado.Completa)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "*** COLETA INCOMPLETA: nenhum orgao foi marcado como desaparecido. ***");
                Console.WriteLine(
                    "    Subarvore que falha some da resposta igual a orgao extinto — e");
                Console.WriteLine(
                    "    publicar extincao por causa de um timeout seria manchete falsa.");
                return 2;
            }

            return 0;
        }

    case "coletar-pessoal":
        {
            // O quadro de pessoal do Executivo, do Portal da Transparencia. E' dado aberto
            // e NAO exige cadastro — ao contrario do que a Etapa 11 afirmou.
            //
            // A agregacao acontece na leitura: nenhum nome, nenhum CPF e nenhuma linha
            // nominal chega ao banco. O que se grava sao ~350 contagens por mes.
            var ano = LerInteiro("--ano");
            var mes = LerInteiro("--mes");

            if (ano is null || mes is null)
            {
                Console.Error.WriteLine(
                    "Informe --ano e --mes. Nao ha' padrao de proposito: a fonte publica o "
                    + "arquivo de um mes semanas depois do fechamento dele, e adivinhar a "
                    + "competencia daria 403 sem dizer por que.");
                return 2;
            }

            Competencia competencia;

            try
            {
                competencia = Competencia.Criar(ano.Value, mes.Value);
            }
            catch (ArgumentOutOfRangeException erro)
            {
                Console.Error.WriteLine($"Competencia invalida: {erro.Message}");
                return 2;
            }

            // --meses N recolhe tambem os N-1 meses ANTERIORES. E' ferramenta de carga
            // inicial: a serie historica so' existe se alguem for buscar o passado, e a
            // origem guarda arquivo ate' 2024-12. O agendamento mensal usa o padrao, 1.
            var meses = Math.Clamp(LerInteiro("--meses") ?? 1, 1, 60);

            var aColetar = new List<Competencia>();

            for (var recuo = meses - 1; recuo >= 0; recuo--)
            {
                var alvo = new DateOnly(competencia.Ano, competencia.Mes, 1).AddMonths(-recuo);

                aColetar.Add(Competencia.Criar(alvo.Year, alvo.Month));
            }

            Console.WriteLine(meses == 1
                ? $"Coletando o quadro de pessoal de {competencia}..."
                : $"Coletando {meses} competencias, de {aColetar[0]} a {competencia}...");
            Console.WriteLine(
                "Sao ~83 MB compactados e ~778 mil vinculos por mes. A contagem acontece");
            Console.WriteLine("durante a leitura: nada nominal e' guardado.");

            var cronometroDePessoal = System.Diagnostics.Stopwatch.StartNew();
            var servicoDePessoal = servicos.GetRequiredService<ServicoDeColetaDePessoal>();

            ResultadoDaColetaDePessoal? pessoal = null;
            var mesesComFalha = new List<string>();

            // Cortesia entre meses, e nao paranoia: medido, uma carga de 19 competencias
            // seguidas fez a origem comecar a responder 405 a partir da decima primeira,
            // inclusive para um mes que tinha baixado minutos antes. Sao 83 MB por mes, e
            // cinco segundos de pausa custam menos que uma carga pela metade.
            var pausaEntreMeses = TimeSpan.FromSeconds(5);
            var primeiro = true;

            foreach (var mesAlvo in aColetar)
            {
                if (!primeiro)
                {
                    await Task.Delay(pausaEntreMeses).ConfigureAwait(false);
                }

                primeiro = false;

                try
                {
                    pessoal = await servicoDePessoal.ColetarAsync(mesAlvo).ConfigureAwait(false);

                    if (meses > 1)
                    {
                        Console.WriteLine(
                            $"  {mesAlvo}  {pessoal.VinculosLidos,8} vinculos  "
                            + $"{pessoal.CoberturaDosVinculos,7:P1} ligados");
                    }
                }
                catch (InvalidOperationException erro)
                {
                    // Uma competencia que a origem ainda nao publicou responde 403. Numa
                    // carga de vinte meses, derrubar tudo por causa do mes mais recente
                    // desperdicaria os dezenove que ja' entraram.
                    mesesComFalha.Add($"{mesAlvo}: {erro.Message}");
                    Console.WriteLine($"  {mesAlvo}  [FALHOU] {erro.Message}");
                }
            }

            cronometroDePessoal.Stop();

            if (pessoal is null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("*** Nenhuma competencia foi coletada. ***");

                foreach (var falha in mesesComFalha)
                {
                    Console.Error.WriteLine($"    {falha}");
                }

                return 1;
            }

            Console.WriteLine($"""

                === QUADRO DE PESSOAL — {pessoal.Competencia} ===
                Vinculos lidos        : {pessoal.VinculosLidos}
                Orgaos encontrados    : {pessoal.OrgaosEncontrados}
                  ligados ao grafo    : {pessoal.OrgaosLigadosAoGrafo}
                  por tabela curada   : {pessoal.LigadosPorExcecaoCurada}
                Agregados descartados : {pessoal.AgregadosDescartados}
                Vinculos de orgao     : {pessoal.VinculosDeOrgaos}
                  ligados ao grafo    : {pessoal.VinculosLigadosAoGrafo}
                Cobertura             : {pessoal.CoberturaDosVinculos:P1}
                Lotacoes gravadas     : {pessoal.LotacoesGravadas}
                Tempo                 : {cronometroDePessoal.Elapsed:hh\:mm\:ss}
                """);

            if (pessoal.SemPar.Count > 0)
            {
                Console.WriteLine("--- maiores orgaos sem par no grafo ---");
                Console.WriteLine(
                    "    Nao casar e' desfecho legitimo: os governos dos ex-territorios e as");
                Console.WriteLine(
                    "    rubricas de totalizacao nao existem no SIORG. Mas um MINISTERIO");
                Console.WriteLine(
                    "    nesta lista e' sinal de que a ponte precisa de mais uma entrada.");
                Console.WriteLine();

                foreach (var (codigo, nome, vinculos) in pessoal.SemPar)
                {
                    Console.WriteLine($"  {vinculos,7}  [{codigo,6}] {nome}");
                }
            }

            if (mesesComFalha.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"*** {mesesComFalha.Count} de {aColetar.Count} competencias falharam. ***");
                Console.WriteLine(
                    "    O que entrou esta' salvo. Competencia ainda nao publicada responde");
                Console.WriteLine("    403, e e' o caso esperado para o mes mais recente.");

                return 3;
            }

            return 0;
        }

    case "recoletar-rejeitadas":
        {
            // So' os deputados com despesa rejeitada no checkpoint, e so' ate a competencia
            // --ate (AAAAMM): a glosa da Camara passou a ser conferida pela precisao da
            // origem (08/11/2026) e as rejeitadas agora entram, sem dar a esses deputados
            // meses que o resto da base congelada nao tem.
            //
            // A primeira rodada (08/11/2026) duplicou 28.630 passagens SIGEPA: a Camara passou
            // a publicar o CNPJ nelas e o documento estava no hash. Corrigido no hash (A15,
            // ver docs/registro/etapa-17-revisao-das-regras.md).
            var ate = LerInteiro("--ate")
                ?? throw new InvalidOperationException("Informe --ate AAAAMM.");
            var teto = Competencia.Criar(ate / 100, ate % 100);

            var comRejeicao = (await contexto.CheckpointsDeIngestao
                .AsNoTracking()
                .Where(checkpoint => checkpoint.Legislatura == legislatura
                    && checkpoint.DespesasRejeitadas > 0)
                .Select(checkpoint => checkpoint.IdExternoParlamentar)
                .ToListAsync()
                .ConfigureAwait(false))
                .ToHashSet();

            var ingestao = servicos.GetRequiredService<ServicoDeIngestao>();
            int visitados = 0, novas = 0, aindaRejeitadas = 0;

            await foreach (var deputado in servicos.GetRequiredService<IFonteDeParlamentares>()
                .ListarDeputadosAsync()
                .ConfigureAwait(false))
            {
                if (!comRejeicao.Contains(deputado.IdExterno))
                {
                    continue;
                }

                ResultadoDaIngestao resultado;

                try
                {
                    resultado = await ingestao
                        .IngerirDeputadoAsync(deputado, legislatura, teto)
                        .ConfigureAwait(false);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    // Como na coleta: um deputado que falha nao derruba os outros; rodar de
                    // novo e' seguro, o hash nao deixa duplicar.
                    Console.Error.WriteLine($"Deputado {deputado.IdExterno}: {erro.GetBaseException().Message}");
                    continue;
                }

                visitados++;
                novas += resultado.DespesasNovas;
                aindaRejeitadas += resultado.Rejeitadas.Count;
            }

            Console.WriteLine($"Deputados com rejeicao: {comRejeicao.Count}; visitados: {visitados}; "
                + $"despesas novas ate {teto}: {novas}; ainda rejeitadas: {aindaRejeitadas}.");
            return 0;
        }

    case "reconsultar-mei":
        {
            // Uma vez, para a base que ja existia: as datas da opcao pelo MEI eram
            // descartadas na ingestao ate 08/11/2026. Fornecedor novo ja entra com elas.
            var cadastro = servicos.GetRequiredService<IFonteDeCadastroEmpresarial>();
            var meis = await contexto.Fornecedores
                .Where(fornecedor => fornecedor.OptantePeloMei == true
                    && fornecedor.DataOpcaoPeloMei == null)
                .ToListAsync()
                .ConfigureAwait(false);

            int comData = 0, semResposta = 0;

            var vistos = 0;

            foreach (var fornecedor in meis)
            {
                EmpresaDaFonte? empresa;

                try
                {
                    empresa = await cadastro.ConsultarAsync(fornecedor.Documento.Numero).ConfigureAwait(false);
                }
                catch (HttpRequestException)
                {
                    // Falha transitoria da fonte: fica sem data e a proxima execucao tenta de novo.
                    empresa = null;
                }

                if (empresa is null)
                {
                    semResposta++;
                }
                else
                {
                    fornecedor.RegistrarDatasDoMei(
                        empresa.OptantePeloMei, empresa.DataOpcaoPeloMei, empresa.DataExclusaoDoMei);
                    comData += empresa.DataOpcaoPeloMei is null ? 0 : 1;
                }

                // Salva aos poucos: uma queda no meio nao perde o que ja foi consultado.
                if (++vistos % 50 == 0)
                {
                    await contexto.SaveChangesAsync().ConfigureAwait(false);
                }
            }

            await contexto.SaveChangesAsync().ConfigureAwait(false);

            Console.WriteLine($"MEI reconsultados: {meis.Count}; com data da opcao: {comData}; "
                + $"sem resposta da fonte: {semResposta}.");
            return 0;
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

            // O score conta as flags de rede, que acabaram de mudar. Ver o CicloMensal.
            foreach (var afetado in resultadoDeRede.ParlamentaresAfetados)
            {
                await varredura.VarrerAsync(afetado).ConfigureAwait(false);
            }

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

            Console.WriteLine(
                $"Scores recalculados com a rede : {resultadoDeRede.ParlamentaresAfetados.Count}");

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
                Despesas novas      : {resultado.DespesasNovas}
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
              dotnet run -- migrar                        so' aplica as migrations e sai
              dotnet run -- coletar [--legislatura 57] [--limite N]
              dotnet run -- varrer  [--legislatura 57]
              dotnet run -- consolidar [--meses N]     sem --meses nao apaga nada
              dotnet run -- reconsultar-mei            datas da opcao pelo MEI na BrasilAPI
              dotnet run -- recoletar-rejeitadas --ate AAAAMM
                                                          so' os deputados com despesa rejeitada
              dotnet run -- ciclo-mensal [--forcar]    a passada completa do ADR-0010 §4
              dotnet run -- coletar-estado [--todas-as-esferas]   o grafo do Estado (SIORG)
              dotnet run -- coletar-cadeiras [--se-vencido]
                                                          quem ocupa os cargos de comando;
                                                          --se-vencido rele' so' as fontes
                                                          paradas ha mais de uma semana
              dotnet run -- importar-tse [--ano 2022] [--pasta ../dados/tse]
                                                          candidaturas, votos por municipio
                                                          e doacoes, dos arquivos do TSE
              dotnet run -- coletar-pessoal --ano 2026 --mes 7 [--meses N]
                                                          o quadro de pessoal; --meses N
                                                          recolhe tambem os anteriores
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
