namespace Explanando.Domain.Regras;

/// <summary>
/// Quando a atualização global de todos os parlamentares vence.
///
/// <para>O <c>ADR-0010</c> §4 exige uma passada completa e gratuita de toda a base uma vez
/// por mês. O motivo não é operacional, é de justiça: o dado também é atualizado por
/// varredura paga sob demanda, então dois parlamentares podem ter frescor diferente. Num
/// ranking público com nome e foto, quem foi varrido mais recentemente aparece pior — não
/// por ser, mas por ter sido olhado mais perto. E isso é explorável: basta o adversário de
/// alguém pagar varreduras repetidas para manter aquele dado fresco enquanto os aliados
/// envelhecem.</para>
/// </summary>
public static class CicloDeAtualizacao
{
    /// <summary>Uma vez por mês, como o <c>ADR-0010</c> §4 define.</summary>
    public static TimeSpan IntervaloPadrao => TimeSpan.FromDays(30);

    /// <summary>
    /// Se a base inteira precisa ser varrida de novo.
    /// </summary>
    ///
    /// <param name="varreduraMaisAntiga">
    /// A varredura <b>mais antiga</b> da base — não a mais recente.
    ///
    /// <para>É a diferença que faz a regra funcionar. Olhando para a mais recente, uma única
    /// varredura paga por mês adiaria o ciclo global <i>para sempre</i>: a base ficaria
    /// eternamente "atualizada" por causa de um parlamentar, enquanto os outros 512
    /// envelhecem — que é exatamente a distorção que esta passada existe para corrigir.</para>
    ///
    /// <para>Nulo quando nenhum parlamentar foi varrido ainda.</para>
    /// </param>
    /// <param name="agora">O instante da decisão.</param>
    /// <param name="intervalo">Quanto tempo o dado pode envelhecer.</param>
    public static bool EstaVencido(
        DateTimeOffset? varreduraMaisAntiga,
        DateTimeOffset agora,
        TimeSpan intervalo)
    {
        if (varreduraMaisAntiga is not { } maisAntiga)
        {
            // Base nunca varrida: adiar deixaria o ranking sem score nenhum.
            return true;
        }

        // Data no futuro não dispara nada. Relógio torto na origem ou registro adulterado
        // não pode pôr uma coleta de doze horas em laço.
        if (maisAntiga > agora)
        {
            return false;
        }

        // "Ou igual" porque um agendamento que dispara na hora cheia, contra um registro
        // gravado na hora cheia anterior, atrasaria um ciclo inteiro por uma fração de
        // segundo.
        return agora - maisAntiga >= intervalo;
    }
}
