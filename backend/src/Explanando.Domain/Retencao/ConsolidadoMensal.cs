using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Retencao;

/// <summary>
/// Quanto tempo de despesa detalhada se guarda.
///
/// <para>Existe desligada. A medição contra dado real diz que a legislatura inteira cabe no
/// tier gratuito depois das otimizações de armazenamento — mas com folga finita, e a base
/// cresce sozinha: a legislatura 57 termina em janeiro de 2027 e a 58 começa em seguida.
/// Construir o mecanismo agora e mantê-lo desligado custa pouco; descobrir que ele falta
/// com o banco cheio custaria uma recoleta de horas contra uma API que devolve 429.</para>
/// </summary>
public sealed record JanelaDeRetencao
{
    /// <summary>Guarda tudo. É o valor do V1.</summary>
    public static JanelaDeRetencao Ilimitada { get; } = new() { Meses = null };

    /// <summary>Nulo guarda tudo. Caso contrário, quantos meses de detalhe preservar.</summary>
    public int? Meses { get; init; }

    public static JanelaDeRetencao De(int meses)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(meses);

        return new JanelaDeRetencao { Meses = meses };
    }

    public bool GuardaTudo => Meses is null;

    /// <summary>
    /// A competência mais antiga cujo detalhe se preserva, dada a data de referência.
    /// Nula quando a janela é ilimitada.
    /// </summary>
    public Competencia? PrimeiraCompetenciaPreservada(DateOnly referencia)
    {
        if (Meses is not { } meses)
        {
            return null;
        }

        // meses - 1 porque o mês da referência conta como o primeiro preservado: uma
        // janela de 1 mês guarda o mês corrente, não o corrente mais o anterior.
        var inicio = referencia.AddMonths(-(meses - 1));

        return Competencia.Criar(inicio.Year, inicio.Month);
    }
}

/// <summary>
/// O gasto de um parlamentar num mês, somado.
///
/// <para>Sobrevive à poda. É o que permite o ranking de volume continuar cobrindo o mandato
/// inteiro mesmo quando o detalhe nota a nota de meses antigos já não existe — e é por isso
/// que a consolidação é <b>mensal</b> e não um total único: uma linha por mês custa quase
/// nada (23 mil linhas para 513 deputados numa legislatura) e preserva a curva de gasto,
/// que um total único jogaria fora.</para>
///
/// <para><see cref="DetalheDisponivel"/> é a parte que não pode faltar. Sem ela a API
/// mostraria um mês consolidado como se as notas ainda pudessem ser abertas, e o leitor
/// concluiria que o documento foi retirado.</para>
/// </summary>
public sealed class ConsolidadoMensal : Entidade
{
    private ConsolidadoMensal()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    public Competencia Competencia { get; private set; }

    /// <summary>Soma do que o erário pagou, com estornos abatidos.</summary>
    public decimal TotalLiquido { get; private set; }

    public decimal TotalDocumento { get; private set; }

    public decimal TotalGlosa { get; private set; }

    public int QuantidadeDeDespesas { get; private set; }

    /// <summary>Quantas linhas do mês são devolução de valor. Ver <see cref="Despesa.EhEstorno"/>.</summary>
    public int QuantidadeDeEstornos { get; private set; }

    /// <summary>
    /// Se as despesas por trás deste total ainda estão no banco. Falso depois da poda.
    ///
    /// A API é obrigada a expor isto: "consolidado" e "sem indicadores" são coisas
    /// diferentes, e o motor de regras não roda sobre mês podado.
    /// </summary>
    public bool DetalheDisponivel { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public static ConsolidadoMensal Consolidar(
        Guid parlamentarId,
        Competencia competencia,
        IReadOnlyCollection<Despesa> despesasDoMes,
        DateTimeOffset momento)
    {
        var consolidado = new ConsolidadoMensal
        {
            ParlamentarId = parlamentarId,
            Competencia = competencia,
            DetalheDisponivel = true,
        };

        consolidado.Recalcular(despesasDoMes, momento);
        return consolidado;
    }

    /// <summary>
    /// Refaz as somas do zero. Nunca incrementa: a consolidação roda de novo a cada
    /// varredura sobre os mesmos meses, e acumular inflaria o volume sem que gasto novo
    /// algum tivesse existido.
    /// </summary>
    public void Recalcular(IReadOnlyCollection<Despesa> despesasDoMes, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(despesasDoMes);

        TotalLiquido = despesasDoMes.Sum(despesa => despesa.ValorLiquido);
        TotalDocumento = despesasDoMes.Sum(despesa => despesa.ValorDocumento);
        TotalGlosa = despesasDoMes.Sum(despesa => despesa.ValorGlosa);
        QuantidadeDeDespesas = despesasDoMes.Count;
        QuantidadeDeEstornos = despesasDoMes.Count(despesa => despesa.EhEstorno);
        AtualizadoEm = momento;
    }

    /// <summary>
    /// Marca que o detalhe deste mês foi descartado. Os totais permanecem — são a única
    /// coisa que sobra, e por isso não podem ser recalculados depois.
    /// </summary>
    public void MarcarDetalheDescartado(DateTimeOffset momento)
    {
        DetalheDisponivel = false;
        AtualizadoEm = momento;
    }
}
