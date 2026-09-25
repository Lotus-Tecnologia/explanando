using Explanando.Domain.Comum;
using Explanando.Domain.Contestacoes;

namespace Explanando.Domain.Tests.Comum;

/// <summary>
/// O momento da criação, lido do UUID v7. É a data que o feed de alertas publica — e por
/// isso não pode errar nem inventar.
/// </summary>
public class EntidadeTests
{
    [Fact]
    public void Le_o_momento_de_um_id_v7_conhecido()
    {
        // Um id real da base local: a despesa entrou em 21/08/2026, 13:25:15.834 UTC.
        var momento = Entidade.MomentoDaCriacao(Guid.Parse("01a0247f-45ba-768c-be73-4f13b3d25e40"));

        momento.ShouldBe(new DateTimeOffset(2026, 8, 21, 13, 25, 15, 834, TimeSpan.Zero));
    }

    [Fact]
    public void Uma_entidade_nova_nasce_com_o_momento_de_agora()
    {
        var antes = DateTimeOffset.UtcNow.AddSeconds(-1);

        var contestacao = ContestacaoDespesa.Criar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "a@camara.leg.br", "texto", DateTimeOffset.UtcNow);

        var momento = Entidade.MomentoDaCriacao(contestacao.Id);

        momento.ShouldNotBeNull();
        momento.Value.ShouldBeInRange(antes, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void Id_que_nao_e_v7_nao_ganha_data_inventada()
    {
        Entidade.MomentoDaCriacao(Guid.Parse("9f1c2b3a-0000-4000-8000-000000000000")).ShouldBeNull();
        Entidade.MomentoDaCriacao(Guid.Empty).ShouldBeNull();
    }
}
