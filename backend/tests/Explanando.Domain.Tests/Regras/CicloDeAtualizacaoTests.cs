using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// Quando a atualização global vence.
///
/// <para>A regra existe por causa de uma injustiça concreta: o dado é atualizado por
/// varredura paga sob demanda, então dois parlamentares podem ter frescor diferente. Num
/// ranking público com nome e foto, quem foi varrido mais recentemente aparece pior — não
/// por ser, mas por ter sido olhado mais perto. A passada mensal existe para nivelar isso, e
/// só nivela se olhar para <b>quem está mais atrasado</b>.</para>
/// </summary>
public sealed class CicloDeAtualizacaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan UmMes = TimeSpan.FromDays(30);

    [Fact]
    public void Base_nunca_varrida_esta_vencida()
    {
        // Primeira execução: não há o que comparar, e adiar seria deixar a base sem score.
        CicloDeAtualizacao.EstaVencido(null, Agora, UmMes).ShouldBeTrue();
    }

    [Fact]
    public void Vence_quando_o_mais_atrasado_passou_do_intervalo()
    {
        var haTrintaEUmDias = Agora.AddDays(-31);

        CicloDeAtualizacao.EstaVencido(haTrintaEUmDias, Agora, UmMes).ShouldBeTrue();
    }

    [Fact]
    public void Nao_vence_enquanto_o_mais_atrasado_esta_dentro_do_intervalo()
    {
        var haDezDias = Agora.AddDays(-10);

        CicloDeAtualizacao.EstaVencido(haDezDias, Agora, UmMes).ShouldBeFalse();
    }

    [Fact]
    public void Vence_exatamente_no_intervalo()
    {
        // Sem o "ou igual", um agendamento que dispara na hora cheia atrasaria um ciclo
        // inteiro por uma fração de segundo.
        CicloDeAtualizacao.EstaVencido(Agora - UmMes, Agora, UmMes).ShouldBeTrue();
    }

    [Fact]
    public void Data_no_futuro_nao_vence()
    {
        // Relógio torto na origem ou registro adulterado não pode disparar coleta de 12
        // horas em laço.
        CicloDeAtualizacao.EstaVencido(Agora.AddDays(5), Agora, UmMes).ShouldBeFalse();
    }

    [Fact]
    public void O_intervalo_padrao_e_mensal_como_o_ADR_0010_define()
    {
        CicloDeAtualizacao.IntervaloPadrao.ShouldBe(TimeSpan.FromDays(30));
    }
}
