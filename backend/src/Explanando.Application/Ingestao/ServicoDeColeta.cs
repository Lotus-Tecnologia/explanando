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

    /// <summary>
    /// Motivos de rejeicao agrupados, do mais frequente para o menos.
    ///
    /// Contagem sozinha nao serve: a primeira coleta real recusou 9% das despesas e o
    /// resumo nao dizia por que. Sem o motivo, nao da' para saber se o dado da origem e'
    /// ruim ou se o dominio esta rigido demais.
    /// </summary>
    public IReadOnlyList<(string Motivo, int Ocorrencias)> MotivosDeRejeicao { get; init; } = [];
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
        var motivos = new Dictionary<string, int>(StringComparer.Ordinal);

        await foreach (var deputado in fonte.ListarDeputadosAsync(cancelamento).ConfigureAwait(false))
        {
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
            alertas.AddRange(resultado.AlertasDeSanidade);

            foreach (var rejeicao in resultado.Rejeitadas)
            {
                var chave = Resumir(rejeicao.Motivo);
                motivos[chave] = motivos.GetValueOrDefault(chave) + 1;
            }

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
