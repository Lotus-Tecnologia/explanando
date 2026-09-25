using Explanando.Application.Consultas;

namespace Explanando.Application.Tests.Consultas;

/// <summary>
/// De onde vieram os votos, e o gasto com fornecedores sediados ali.
///
/// <para>A "base eleitoral" é aritmética — os municípios que somam metade dos votos — e o
/// cruzamento com a sede do fornecedor é por nome de município normalizado e UF.</para>
/// </summary>
public class MontagemDaVotacaoTests
{
    private static readonly (int, string, string, string, string, long) Candidatura =
        (2022, "ACACIO FAVACHO", "Deputado Federal", "MDB", "ELEITO POR QP", 0);

    [Fact]
    public void A_base_e_o_menor_conjunto_que_soma_metade_dos_votos()
    {
        (string, string, long)[] votos =
        [
            ("MACAPÁ", "AP", 600),
            ("SANTANA", "AP", 250),
            ("OIAPOQUE", "AP", 100),
            ("LARANJAL DO JARI", "AP", 50),
        ];

        var votacao = MontagemDaVotacao.Montar(Candidatura, votos, []);

        votacao.TotalDeVotos.ShouldBe(1_000);
        votacao.MunicipiosDaMetadeDosVotos.ShouldBe(1, "Macapá sozinha já passa de metade");
        votacao.Principais[0].Nome.ShouldBe("MACAPÁ");
        votacao.Principais[0].Fatia.ShouldBe(0.6m);
    }

    [Fact]
    public void O_gasto_na_base_casa_a_sede_por_nome_normalizado_e_uf()
    {
        (string, string, long)[] votos = [("MACAPÁ", "AP", 600), ("SANTANA", "AP", 400)];

        (string?, string?, decimal)[] pagamentos =
        [
            ("Macapa", "AP", 1_000m),     // sem acento e em caixa baixa: é a mesma cidade
            ("MACAPÁ", "SP", 500m),       // outra UF: não é
            ("SANTANA", "AP", 300m),      // fora da metade
            (null, null, 200m),            // sem endereço: fica fora do denominador
        ];

        var votacao = MontagemDaVotacao.Montar(Candidatura, votos, pagamentos);

        votacao.PagoAFornecedoresDaBase.ShouldBe(1_000m);
        votacao.PagoAFornecedoresComEndereco.ShouldBe(1_800m);
    }

    [Fact]
    public void Municipio_sem_voto_nao_conta_nem_aparece()
    {
        (string, string, long)[] votos = [("MACAPÁ", "AP", 10), ("SANTANA", "AP", 0)];

        var votacao = MontagemDaVotacao.Montar(Candidatura, votos, []);

        votacao.MunicipiosComVoto.ShouldBe(1);
        votacao.Principais.ShouldHaveSingleItem();
    }

    [Fact]
    public void Mostra_no_maximo_dez_municipios()
    {
        var votos = Enumerable.Range(1, 30).Select(i => ($"CIDADE {i}", "AP", (long)i)).ToArray();

        MontagemDaVotacao.Montar(Candidatura, votos, []).Principais.Count.ShouldBe(10);
    }
}
