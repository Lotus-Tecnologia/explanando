using Explanando.Domain.Comum;

namespace Explanando.Domain.Tests.Comum;

/// <summary>
/// A API da Camara ja entrega `ano` e `mes` prontos em cada despesa. Esse par e' a unidade
/// de agregacao do CEAP e a base de REG_TX_001 (fracionamento), REG_TX_002 (cravado no
/// limite) e REG_TX_003 (padrao mensal identico) — todas elas raciocinam "dentro do mesmo mes".
/// </summary>
public sealed class CompetenciaTests
{
    [Fact]
    public void Guarda_ano_e_mes()
    {
        var competencia = Competencia.Criar(2026, 7);

        competencia.Ano.ShouldBe(2026);
        competencia.Mes.ShouldBe(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void Rejeita_mes_fora_do_calendario(int mesInvalido)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Competencia.Criar(2026, mesInvalido));
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(0)]
    public void Rejeita_ano_implausivel(int anoInvalido)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Competencia.Criar(anoInvalido, 7));
    }

    [Fact]
    public void Competencias_iguais_sao_iguais()
    {
        Competencia.Criar(2026, 7).ShouldBe(Competencia.Criar(2026, 7));
    }

    [Fact]
    public void Ordena_cronologicamente_atravessando_a_virada_do_ano()
    {
        var dezembro = Competencia.Criar(2025, 12);
        var janeiro = Competencia.Criar(2026, 1);

        dezembro.CompareTo(janeiro).ShouldBeLessThan(0);
        janeiro.CompareTo(dezembro).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Sabe_dizer_quantos_meses_a_separam_de_outra()
    {
        // Base de REG_TX_004 (spike): comparar um mes com a media dos anteriores.
        Competencia.Criar(2026, 3).MesesDesde(Competencia.Criar(2025, 12)).ShouldBe(3);
        Competencia.Criar(2026, 1).MesesDesde(Competencia.Criar(2026, 1)).ShouldBe(0);
    }

    [Fact]
    public void Converte_a_data_de_um_documento_em_competencia()
    {
        var data = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

        Competencia.De(data).ShouldBe(Competencia.Criar(2026, 7));
    }

    [Fact]
    public void Exibe_no_formato_aaaa_mm()
    {
        Competencia.Criar(2026, 7).ToString().ShouldBe("2026-07");
    }
}
