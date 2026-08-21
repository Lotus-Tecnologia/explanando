using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Retencao;

namespace Explanando.Application.Retencao;

/// <summary>Porta da consolidação e da poda.</summary>
public interface IRepositorioDeRetencao
{
    Task<IReadOnlyList<Guid>> ObterIdsDeParlamentaresAsync(
        CancellationToken cancelamento = default);

    Task<IReadOnlyList<Despesa>> ObterDespesasAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    Task<IReadOnlyList<ConsolidadoMensal>> ObterConsolidadosAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    void Adicionar(ConsolidadoMensal consolidado);

    /// <summary>
    /// Apaga as despesas do parlamentar anteriores à competência informada, e com elas as
    /// flags que dependem dessas despesas.
    /// </summary>
    Task<int> RemoverDespesasAnterioresAsync(
        Guid parlamentarId,
        Competencia primeiraPreservada,
        CancellationToken cancelamento = default);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>O que uma passada de retenção fez.</summary>
public sealed record ResultadoDaRetencao
{
    public int ParlamentaresVistos { get; init; }
    public int ConsolidadosCriados { get; init; }
    public int ConsolidadosAtualizados { get; init; }
    public int DespesasDescartadas { get; init; }
    public int MesesSemDetalhe { get; init; }

    /// <summary>Falso quando a janela é ilimitada — a passada só consolidou, não podou.</summary>
    public bool Podou { get; init; }
}

/// <summary>
/// Consolida o gasto mensal e, quando há janela, descarta o detalhe antigo.
///
/// <para><b>Consolidar e podar são passos separados de propósito.</b> A consolidação roda
/// sempre e é reversível — recalcula do zero a partir das despesas. A poda é destrutiva e
/// só acontece quando alguém pede uma janela. Fundir as duas faria toda ingestão apagar
/// dado como efeito colateral de somar.</para>
///
/// <para>A ordem também importa: consolida primeiro, apaga depois. O inverso apagaria as
/// despesas antes de somá-las, e o total daquele mês seria perdido para sempre.</para>
/// </summary>
public sealed class ServicoDeRetencao(
    IRepositorioDeRetencao repositorio,
    IRelogioDaRetencao relogio)
{
    public async Task<ResultadoDaRetencao> AplicarAsync(
        JanelaDeRetencao janela,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(janela);

        var momento = relogio.Agora;
        var corte = janela.PrimeiraCompetenciaPreservada(DateOnly.FromDateTime(momento.UtcDateTime));

        var ids = await repositorio.ObterIdsDeParlamentaresAsync(cancelamento).ConfigureAwait(false);

        var criados = 0;
        var atualizados = 0;
        var descartadas = 0;
        var semDetalhe = 0;

        foreach (var parlamentarId in ids)
        {
            var despesas = await repositorio.ObterDespesasAsync(parlamentarId, cancelamento)
                .ConfigureAwait(false);

            var existentes = (await repositorio.ObterConsolidadosAsync(parlamentarId, cancelamento)
                    .ConfigureAwait(false))
                .ToDictionary(consolidado => consolidado.Competencia);

            foreach (var mes in despesas.GroupBy(despesa => despesa.Competencia))
            {
                var doMes = mes.ToList();

                if (existentes.TryGetValue(mes.Key, out var consolidado))
                {
                    consolidado.Recalcular(doMes, momento);
                    atualizados++;
                }
                else
                {
                    consolidado = ConsolidadoMensal.Consolidar(parlamentarId, mes.Key, doMes, momento);
                    repositorio.Adicionar(consolidado);
                    existentes[mes.Key] = consolidado;
                    criados++;
                }
            }

            if (corte is not { } primeiraPreservada)
            {
                continue;
            }

            // Marca antes de apagar. Se a remocao falhar no meio, sobra um mes marcado
            // como sem detalhe cujo detalhe ainda existe — inconveniente, mas honesto. O
            // inverso publicaria notas de um mes que a API diz estar consolidado.
            foreach (var consolidado in existentes.Values
                .Where(c => c.Competencia < primeiraPreservada && c.DetalheDisponivel))
            {
                consolidado.MarcarDetalheDescartado(momento);
                semDetalhe++;
            }

            descartadas += await repositorio
                .RemoverDespesasAnterioresAsync(parlamentarId, primeiraPreservada, cancelamento)
                .ConfigureAwait(false);
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return new ResultadoDaRetencao
        {
            ParlamentaresVistos = ids.Count,
            ConsolidadosCriados = criados,
            ConsolidadosAtualizados = atualizados,
            DespesasDescartadas = descartadas,
            MesesSemDetalhe = semDetalhe,
            Podou = corte is not null,
        };
    }
}

/// <summary>Relógio da retenção, separado para poder ser congelado nos testes.</summary>
public interface IRelogioDaRetencao
{
    DateTimeOffset Agora { get; }
}

public sealed class RelogioDaRetencaoDoSistema : IRelogioDaRetencao
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
