using Explanando.Domain.Comum;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Auditoria;

/// <summary>
/// O que cada regra conseguiu avaliar num parlamentar, gravado.
///
/// <para>A <see cref="MetadadosDeCobertura"/> nasce em memória durante a varredura e morria
/// com ela. Sem esta entidade a API não tem como responder "esta regra se absteve, e por quê"
/// — e o <c>ADR-0013</c> §2 proíbe a interface de renderizar regra abstida como aprovada.</para>
///
/// <para>Uma linha por (parlamentar, regra): 11 regras × 513 parlamentares são 5.643 linhas,
/// custo irrelevante para a única coisa que distingue <b>"avaliei e está limpo"</b> de
/// <b>"não tive como avaliar"</b>.</para>
///
/// <para>Na base real isso não é hipótese: <c>REG_TX_002</c> ficou em zero nas 711.796
/// despesas porque a tabela de limites da CEAP só cobre 2026. Publicar esse zero como
/// silêncio conclusivo seria o produto afirmando mais do que sabe.</para>
/// </summary>
public sealed class CoberturaDoParlamentar : Entidade
{
    private CoberturaDoParlamentar()
    {
        // EF Core.
        CodigoRegra = string.Empty;
    }

    public Guid ParlamentarId { get; private set; }

    public string CodigoRegra { get; private set; }

    public SituacaoDaRegra Situacao { get; private set; }

    public int DespesasComDado { get; private set; }

    public int DespesasSemDado { get; private set; }

    public int FlagsEmitidas { get; private set; }

    /// <summary>Qual dado faltou. <see cref="DadoExigido.Nenhum"/> quando a cobertura foi total.</summary>
    public DadoExigido DadoAusente { get; private set; }

    /// <summary>Motivo da falha, quando a regra estourou. Nulo no caso normal.</summary>
    public string? MotivoDaFalha { get; private set; }

    /// <summary>Versão do motor que produziu esta medição. Ver <see cref="MotorDeRegras.VersaoAtual"/>.</summary>
    public int VersaoDoMotor { get; private set; }

    public DateTimeOffset AtualizadoEm { get; private set; }

    public int DespesasVistas => DespesasComDado + DespesasSemDado;

    /// <summary>
    /// Se o silêncio desta regra pode ser lido como "nada encontrado".
    ///
    /// É a propriedade que a borda consulta antes de desenhar. Falso significa que a
    /// interface tem de exibir a tag de <b>não avaliado</b>, e não a de aprovado.
    /// </summary>
    public bool SilencioEhConclusivo =>
        Situacao is SituacaoDaRegra.SemAchados or SituacaoDaRegra.Emitiu;

    public static CoberturaDoParlamentar Registrar(
        Guid parlamentarId,
        CoberturaDaRegra cobertura,
        int versaoDoMotor,
        DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(cobertura);

        var registro = new CoberturaDoParlamentar
        {
            ParlamentarId = parlamentarId,
            CodigoRegra = cobertura.Codigo,
        };

        registro.Atualizar(cobertura, versaoDoMotor, momento);
        return registro;
    }

    /// <summary>
    /// Regrava a medição. A varredura roda repetidas vezes sobre o mesmo parlamentar, e o
    /// índice é único por (parlamentar, regra): acumular criaria linha duplicada, e inserir
    /// de novo violaria a chave.
    /// </summary>
    public void Atualizar(CoberturaDaRegra cobertura, int versaoDoMotor, DateTimeOffset momento)
    {
        ArgumentNullException.ThrowIfNull(cobertura);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versaoDoMotor);

        if (!string.Equals(cobertura.Codigo, CodigoRegra, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Este registro e' da regra {CodigoRegra}; a medicao e' de {cobertura.Codigo}.",
                nameof(cobertura));
        }

        Situacao = cobertura.Situacao;
        DespesasComDado = cobertura.DespesasComDado;
        DespesasSemDado = cobertura.DespesasSemDado;
        FlagsEmitidas = cobertura.FlagsEmitidas;
        DadoAusente = cobertura.DadoAusente;
        MotivoDaFalha = cobertura.MotivoDaFalha;
        VersaoDoMotor = versaoDoMotor;
        AtualizadoEm = momento;
    }
}
