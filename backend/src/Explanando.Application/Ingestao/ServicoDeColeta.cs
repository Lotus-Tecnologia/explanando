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
    public IReadOnlyList<string> Alertas { get; init; } = [];
    public IReadOnlyList<string> Falhas { get; init; } = [];
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

    public async Task<ResultadoDaColeta> ColetarTudoAsync(
        int legislatura,
        CancellationToken cancelamento = default)
    {
        var alertas = new List<string>();
        var falhas = new List<string>();
        var visitados = 0;
        var pulados = 0;
        var novas = 0;
        var rejeitadas = 0;

        await foreach (var deputado in fonte.ListarDeputadosAsync(cancelamento).ConfigureAwait(false))
        {
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
            alertas.AddRange(resultado.AlertasDeSanidade);

            await RegistrarCheckpointAsync(
                checkpoint, deputado.IdExterno, legislatura, resultado, cancelamento)
                .ConfigureAwait(false);
        }

        return new ResultadoDaColeta
        {
            DeputadosVisitados = visitados,
            DeputadosPulados = pulados,
            DespesasNovas = novas,
            DespesasRejeitadas = rejeitadas,
            Alertas = alertas,
            Falhas = falhas,
        };
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
