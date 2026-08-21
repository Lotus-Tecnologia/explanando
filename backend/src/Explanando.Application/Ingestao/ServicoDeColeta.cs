using Explanando.Domain.Ingestao;

namespace Explanando.Application.Ingestao;

/// <summary>Porta de leitura e escrita dos checkpoints de coleta.</summary>
public interface IRepositorioDeCheckpoints
{
    Task<CheckpointDeIngestao?> ObterAsync(
        int idExternoParlamentar,
        int legislatura,
        CancellationToken cancelamento = default);

    void Adicionar(CheckpointDeIngestao checkpoint);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>Relogio injetavel: coleta que le o relogio direto nao e' testavel.</summary>
public interface IRelogio
{
    DateTimeOffset Agora { get; }
}

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}

/// <summary>Resumo de uma passada completa pelos deputados.</summary>
public sealed record ResultadoDaColeta
{
    public int DeputadosVisitados { get; init; }
    public int DeputadosPulados { get; init; }
    public int DespesasNovas { get; init; }
    public int DespesasRejeitadas { get; init; }

    /// <summary>Consultas de cadastro que falharam. Ver ResultadoDaIngestao.</summary>
    public int FornecedoresNaoEnriquecidos { get; init; }
    public IReadOnlyList<string> Alertas { get; init; } = [];
    public IReadOnlyList<string> Falhas { get; init; } = [];

    /// <summary>
    /// Motivos de rejeicao agrupados, do mais frequente para o menos.
    ///
    /// Contagem sozinha nao serve: a primeira coleta real recusou 9% das despesas e o
    /// resumo nao dizia por que. Sem o motivo, nao da' para saber se o dado da origem e'
    /// ruim ou se o dominio esta rigido demais.
    /// </summary>
    public IReadOnlyList<(string Motivo, int Ocorrencias)> MotivosDeRejeicao { get; init; } = [];

    /// <summary>
    /// A passada foi <b>interrompida</b> porque a fonte parecia fora do ar.
    ///
    /// <para>Verdadeiro quando um numero seguido de deputados voltou com alerta de sanidade
    /// — em exercicio e zero despesas. Um deputado assim e' plausivel; dez seguidos nao
    /// sao, e significam que o problema esta na origem.</para>
    ///
    /// <para>Nao e' o mesmo que <see cref="Falhas"/>: ali estao erros que a fonte admitiu.
    /// Aqui a fonte respondeu 200 e mentiu.</para>
    /// </summary>
    public bool InterrompidaPorFonteSuspeita { get; init; }

    /// <summary>
    /// A <b>listagem</b> de deputados falhou no meio, e a passada cobriu menos gente do que
    /// deveria.
    ///
    /// <para>Diferente de <see cref="Falhas"/> em deputado individual: ali um deputado se
    /// perde e os outros seguem. Aqui a fonte parou de entregar a propria lista, entao nao
    /// se sabe sequer quem ficou de fora.</para>
    ///
    /// <para>Nao e' hipotese: em 2026-08-21 o circuit breaker do Polly abriu durante a
    /// paginacao da lista, a excecao escapou pela enumeracao — que era o unico ponto do laco
    /// sem protecao — e derrubou a coleta em 482 de 513, descartando ate' o resumo final.</para>
    /// </summary>
    public bool ListagemIncompleta { get; init; }
}

/// <summary>
/// Percorre todos os deputados, delegando a coleta de cada um ao
/// <see cref="ServicoDeIngestao"/> e registrando checkpoint.
///
/// Duas politicas explicitas:
///
/// 1. <b>Deputado ja coletado recentemente e' pulado.</b> Economiza tempo, nao corretude —
///    reprocessar tudo tambem funcionaria, so' custaria as ~6.000 requisicoes de novo.
/// 2. <b>Falha num deputado nao interrompe os outros.</b> A Camara pode devolver 500 para
///    um id especifico; derrubar a coleta inteira por causa disso desperdicaria todo o
///    trabalho ja feito na mesma passada.
/// </summary>
public sealed class ServicoDeColeta(
    IFonteDeParlamentares fonte,
    ServicoDeIngestao ingestao,
    IRepositorioDeCheckpoints checkpoints,
    IRelogio relogio)
{
    /// <summary>
    /// Quanto tempo um checkpoint bem-sucedido dispensa nova coleta. Um dia: os dados da
    /// cota sao publicados em lote e nao mudam de hora em hora.
    /// </summary>
    public static TimeSpan ValidadeDoCheckpoint => TimeSpan.FromHours(24);

    /// <summary>
    /// Quantos alertas de sanidade <b>seguidos</b> derrubam a passada.
    ///
    /// <para>Existe por um episodio real. Em 2026-08-21 a rota <c>/despesas</c> da Camara
    /// passou a responder <c>200</c> com lista vazia para todo deputado, enquanto as demais
    /// rotas da mesma API seguiam normais. A coleta marchou por 508 deputados em 26 minutos,
    /// gravou 497 checkpoints com alerta e martelou uma API que ja estava caida.</para>
    ///
    /// <para>Dez e' folgado de proposito: um deputado recem-empossado com zero despesas e'
    /// plausivel, dois em sequencia sao possiveis, dez seguidos nao acontecem por acaso. Com
    /// este limite, aquele episodio teria parado em ~30 segundos em vez de 26 minutos.</para>
    /// </summary>
    public const int MaximoDeAlertasSeguidos = 10;

    /// <summary>
    /// Percorre os deputados da fonte.
    ///
    /// <paramref name="maxDeputados"/> limita quantos sao visitados. Existe para amostragem
    /// de calibragem: a passada completa gera dezenas de milhares de consultas a servicos
    /// publicos e gratuitos, e uma amostra responde as mesmas perguntas sobre limiares com
    /// uma fracao da carga.
    /// </summary>
    public async Task<ResultadoDaColeta> ColetarTudoAsync(
        int legislatura,
        int? maxDeputados = null,
        CancellationToken cancelamento = default)
    {
        var alertas = new List<string>();
        var falhas = new List<string>();
        var visitados = 0;
        var pulados = 0;
        var tentados = 0;
        var novas = 0;
        var rejeitadas = 0;
        var naoEnriquecidos = 0;
        var alertasSeguidos = 0;
        var interrompida = false;
        var listagemIncompleta = false;
        var motivos = new Dictionary<string, int>(StringComparer.Ordinal);

        // Enumeracao manual, e nao `await foreach`, porque a falha precisa ser capturada
        // separadamente do corpo do laco. Um `try` em volta do foreach inteiro nao distingue
        // "a fonte parou de listar" de "este deputado deu problema", e as duas exigem
        // desfechos diferentes: a primeira encerra a passada, a segunda pula um.
        var enumerador = fonte.ListarDeputadosAsync(cancelamento).GetAsyncEnumerator(cancelamento);

        try
        {
        while (true)
        {
            DeputadoDaFonte deputado;

            try
            {
                if (!await enumerador.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                deputado = enumerador.Current;
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // A fonte parou de entregar a lista. Encerrar a passada aqui preserva tudo
                // que ja' foi coletado e o resumo — antes desta guarda, a excecao subia ate'
                // Main e a coleta inteira morria sem sequer imprimir o que tinha feito.
                listagemIncompleta = true;
                falhas.Add(
                    $"A listagem de deputados falhou apos {tentados} tentativas: "
                    + $"{erro.Message} Os deputados ja' coletados estao salvos; os demais "
                    + "serao alcancados na proxima passada.");
                break;
            }

            // Conta TENTATIVAS, nao sucessos: um deputado que falha tambem consumiu
            // requisicoes da fonte. Contar so' os visitados fazia o laco seguir alem do
            // teto — a primeira coleta real pediu 2 deputados e tentou 3.
            if (maxDeputados is { } teto && tentados >= teto)
            {
                break;
            }

            tentados++;

            var checkpoint = await checkpoints
                .ObterAsync(deputado.IdExterno, legislatura, cancelamento)
                .ConfigureAwait(false);

            if (checkpoint?.AindaVale(relogio.Agora, ValidadeDoCheckpoint) == true)
            {
                pulados++;
                continue;
            }

            ResultadoDaIngestao resultado;

            try
            {
                resultado = await ingestao
                    .IngerirDeputadoAsync(deputado, legislatura, cancelamento)
                    .ConfigureAwait(false);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // Sem checkpoint: a proxima passada tenta de novo este deputado.
                falhas.Add($"Deputado {deputado.IdExterno} ({deputado.NomeParlamentar}): {erro.Message}");
                continue;
            }

            visitados++;
            novas += resultado.DespesasNovas;
            rejeitadas += resultado.Rejeitadas.Count;
            naoEnriquecidos += resultado.FornecedoresNaoEnriquecidos;
            alertas.AddRange(resultado.AlertasDeSanidade);

            foreach (var rejeicao in resultado.Rejeitadas)
            {
                var chave = Resumir(rejeicao.Motivo);
                motivos[chave] = motivos.GetValueOrDefault(chave) + 1;
            }

            await RegistrarCheckpointAsync(
                checkpoint, deputado.IdExterno, legislatura, resultado, cancelamento)
                .ConfigureAwait(false);

            // O checkpoint com alerta nao blinda o deputado — AindaVale o recusa —, entao
            // parar aqui nao perde nada: a proxima passada retenta todos eles.
            alertasSeguidos = resultado.TemAlerta ? alertasSeguidos + 1 : 0;

            if (alertasSeguidos >= MaximoDeAlertasSeguidos)
            {
                interrompida = true;
                falhas.Add(
                    $"Coleta interrompida: {alertasSeguidos} deputados seguidos voltaram em "
                    + "exercicio e sem nenhuma despesa. Isso e' falha da origem, nao ausencia "
                    + "de gasto — insistir so' produz checkpoint ruim e carga numa API que ja "
                    + "esta degradada.");
                break;
            }
        }
        }
        finally
        {
            await enumerador.DisposeAsync().ConfigureAwait(false);
        }

        return new ResultadoDaColeta
        {
            InterrompidaPorFonteSuspeita = interrompida,
            ListagemIncompleta = listagemIncompleta,
            DeputadosVisitados = visitados,
            DeputadosPulados = pulados,
            DespesasNovas = novas,
            DespesasRejeitadas = rejeitadas,
            FornecedoresNaoEnriquecidos = naoEnriquecidos,
            Alertas = alertas,
            Falhas = falhas,
            MotivosDeRejeicao = [.. motivos
                .OrderByDescending(par => par.Value)
                .Select(par => (par.Key, par.Value))],
        };
    }

    /// <summary>
    /// Reduz a mensagem a uma familia de motivo, para que valores diferentes do mesmo
    /// defeito agrupem juntos em vez de virar milhares de linhas distintas.
    /// </summary>
    private static string Resumir(string motivo)
    {
        if (motivo.Contains("glosa", StringComparison.OrdinalIgnoreCase))
        {
            return "Valor de glosa inconsistente com documento menos liquido";
        }

        if (motivo.Contains("negative", StringComparison.OrdinalIgnoreCase)
            || motivo.Contains("negativ", StringComparison.OrdinalIgnoreCase))
        {
            return "Valor negativo";
        }

        var corte = motivo.IndexOf(':', StringComparison.Ordinal);
        return corte > 0 ? motivo[..corte] : motivo;
    }

    private async Task RegistrarCheckpointAsync(
        CheckpointDeIngestao? existente,
        int idExterno,
        int legislatura,
        ResultadoDaIngestao resultado,
        CancellationToken cancelamento)
    {
        if (existente is null)
        {
            checkpoints.Adicionar(CheckpointDeIngestao.Criar(
                idExterno,
                legislatura,
                relogio.Agora,
                resultado.DespesasNovas,
                resultado.Rejeitadas.Count,
                resultado.TemAlerta));
        }
        else
        {
            existente.Atualizar(
                relogio.Agora,
                resultado.DespesasNovas,
                resultado.Rejeitadas.Count,
                resultado.TemAlerta);
        }

        await checkpoints.SalvarAsync(cancelamento).ConfigureAwait(false);
    }
}
