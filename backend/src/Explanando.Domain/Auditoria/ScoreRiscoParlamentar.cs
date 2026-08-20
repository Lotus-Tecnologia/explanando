using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Auditoria;

/// <summary>
/// Os números públicos de um parlamentar. Relação 1:1 com <c>Parlamentar</c>.
///
/// <para><b>São dois números independentes, não um</b> (ADR-0009), porque o produto tem dois
/// compradores com necessidades opostas: o cidadão quer saber <i>quanto</i> se gastou, e o
/// departamento de compliance quer saber se a <i>proporção</i> de indicadores e' anomala.</para>
///
/// <para>A versao anterior guardava so' a soma bruta dos pesos das flags. Somar sem dividir
/// mede atividade administrativa, nao risco: na base real, um parlamentar com 1 flag vermelha
/// em 3.575 notas aparecia acima de outro com 14 em 752.</para>
///
/// <para>Tudo aqui e' <b>recalculado do zero</b> a cada varredura, nunca incrementado: a
/// varredura roda repetidas vezes sobre o mesmo parlamentar, e acumular inflaria o ranking
/// sem que nenhum gasto novo tivesse existido.</para>
/// </summary>
public sealed class ScoreRiscoParlamentar : Entidade
{
    private ScoreRiscoParlamentar()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>
    /// <b>O número do B2C.</b> Soma do que o erário efetivamente pagou, com estornos abatidos.
    ///
    /// E' o "Ranking de Volume de Gastos" — a peca publica e viral. Tambem e' o numero mais
    /// seguro juridicamente de todo o produto: gastar a cota inteira e' legal, e publicar
    /// quanto se gastou nao imputa conduta nenhuma.
    /// </summary>
    public decimal VolumeTotalGasto { get; private set; }

    /// <summary>Denominador do indice, e o que decide se ha amostra suficiente.</summary>
    public int DespesasAvaliadas { get; private set; }

    /// <summary>
    /// Soma bruta dos pesos das flags. <b>Contexto, nunca ordenacao.</b>
    ///
    /// Ordenar por isto e' o que produzia a distorcao descrita no cabecalho da classe.
    /// </summary>
    public decimal PesoDasFlags { get; private set; }

    /// <summary>Quantas flags de gravidade alta. Contexto na pagina, nunca ordenacao.</summary>
    public int TotalFlagsVermelhas { get; private set; }

    /// <summary>
    /// <b>O número do B2B.</b> Densidade de indicadores, em pontos por 100 despesas
    /// avaliadas.
    ///
    /// E' o ativo da API de due diligence de PEPs. O ranking de volume qualquer um extrai
    /// dos dados abertos numa tarde; a densidade exige o motor de regras — e' ela que
    /// justifica a cobranca.
    /// </summary>
    public decimal IndiceDeRisco { get; private set; }

    /// <summary>
    /// Se o parlamentar tem despesas bastantes para o indice significar alguma coisa.
    ///
    /// Densidade pura cria a distorcao inversa da soma bruta: 10 despesas com 2 flags dao
    /// 20% e liderariam o ranking com prova fina. Abaixo do piso, o parlamentar fica
    /// <b>fora do ranking de risco</b> — mas continua no de volume, que nao depende de
    /// amostra. Mesma logica do ADR-0007: na duvida, o motor se cala.
    /// </summary>
    public bool AmostraSuficiente { get; private set; }

    /// <summary>Exibida em destaque na pagina publica — mostra se o dado esta fresco.</summary>
    public DateTimeOffset DataUltimaVarredura { get; private set; }

    public static ScoreRiscoParlamentar Criar(Guid parlamentarId) => new()
    {
        ParlamentarId = parlamentarId,
        VolumeTotalGasto = 0m,
        DespesasAvaliadas = 0,
        PesoDasFlags = 0m,
        TotalFlagsVermelhas = 0,
        IndiceDeRisco = 0m,
        AmostraSuficiente = false,
        DataUltimaVarredura = DateTimeOffset.MinValue,
    };

    public void Recalcular(
        IReadOnlyCollection<Despesa> despesas,
        IReadOnlyCollection<FlagAnomalia> flags,
        DateTimeOffset momento,
        int pisoAmostral)
    {
        ArgumentNullException.ThrowIfNull(despesas);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentOutOfRangeException.ThrowIfNegative(pisoAmostral);

        var volume = 0m;

        foreach (var despesa in despesas)
        {
            volume += despesa.ValorParaScore;
        }

        var peso = 0m;
        var vermelhas = 0;

        foreach (var flag in flags)
        {
            peso += flag.Peso;

            if (flag.EhVermelha)
            {
                vermelhas++;
            }
        }

        VolumeTotalGasto = volume;
        DespesasAvaliadas = despesas.Count;
        PesoDasFlags = peso;
        TotalFlagsVermelhas = vermelhas;

        // Pontos por 100 despesas. Sem despesa nao ha denominador — e dividir por zero
        // seria o jeito mais bobo de derrubar a varredura.
        IndiceDeRisco = despesas.Count == 0
            ? 0m
            : peso / despesas.Count * 100m;

        AmostraSuficiente = despesas.Count >= pisoAmostral && despesas.Count > 0;

        DataUltimaVarredura = momento;
    }
}
