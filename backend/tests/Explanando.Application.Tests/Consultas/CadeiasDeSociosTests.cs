using Explanando.Application.Consultas;

namespace Explanando.Application.Tests.Consultas;

/// <summary>
/// "Quem beneficia quem" — a Fase 5 do plano de execução.
///
/// <para>"Pronto quando os 75 nomes ambíguos comprovadamente não ligam nada em teste, e o sócio
/// de alcance alto não aparece."</para>
/// </summary>
public class CadeiasDeSociosTests
{
    private static readonly Guid Eu = Guid.NewGuid();
    private static readonly Guid Outro = Guid.NewGuid();
    private static readonly Guid Fornecedor = Guid.NewGuid();
    private static readonly Guid OutraEmpresa = Guid.NewGuid();

    private static readonly Dictionary<Guid, EmpresaDaCadeia> Empresas = new()
    {
        [Fornecedor] = new(Fornecedor, "11111111000111", "11.111.111/0001-11", "AGENCIA A LTDA"),
        [OutraEmpresa] = new(OutraEmpresa, "22222222000122", "22.222.222/0001-22", "AGENCIA B LTDA"),
    };

    private static Dictionary<Guid, ParlamentarDaDespesa> Parlamentares(params Guid[] ids) =>
        ids.ToDictionary(id => id, id => new ParlamentarDaDespesa
        {
            Id = id,
            NomeParlamentar = $"Parlamentar {id.ToString()[..4]}",
            SiglaPartido = "XYZ",
            Uf = "SP",
            Casa = "Câmara dos Deputados",
        });

    private static VinculoDeSocio[] SocioNasDuas(string nome = "FULANO DE TAL", string cpf = "***123456**") =>
    [
        new(Fornecedor, nome, cpf, "Sócio-Administrador"),
        new(OutraEmpresa, nome, cpf, "Sócio-Administrador"),
    ];

    private static QuemBeneficiaQuem Montar(
        IReadOnlyCollection<PagamentoAgregado> pagamentos,
        IReadOnlyCollection<VinculoDeSocio> vinculos,
        IReadOnlySet<string>? ambiguos = null,
        IReadOnlyDictionary<Guid, EmpresaDaCadeia>? empresas = null,
        params Guid[] parlamentares) =>
        CadeiasDeSocios.Montar(
            Eu,
            pagamentos,
            vinculos,
            ambiguos ?? new HashSet<string>(),
            empresas ?? Empresas,
            Parlamentares([Eu, Outro, .. parlamentares]));

    private static readonly PagamentoAgregado[] Basico =
    [
        new(Eu, Fornecedor, 80_000m),
        new(Outro, OutraEmpresa, 60_000m),
    ];

    [Fact]
    public void Liga_os_dois_parlamentares_pelo_socio_em_comum()
    {
        var resultado = Montar(Basico, SocioNasDuas());

        var cadeia = resultado.Cadeias.ShouldHaveSingleItem();
        cadeia.Fornecedor.RazaoSocial.ShouldBe("AGENCIA A LTDA");
        cadeia.Fornecedor.Valor.ShouldBe(80_000m);
        cadeia.OutraEmpresa.RazaoSocial.ShouldBe("AGENCIA B LTDA");
        cadeia.SociosEmComum.ShouldBe(1);
        var socio = cadeia.Socios.ShouldHaveSingleItem();
        socio.Nome.ShouldBe("FULANO DE TAL");
        socio.Qualificacao.ShouldBe("Sócio-Administrador nas duas");
        cadeia.OutrosParlamentares.ShouldHaveSingleItem().Parlamentar.Id.ShouldBe(Outro);
    }

    [Fact]
    public void O_nome_sai_e_o_cpf_nao()
    {
        // Decisão de 2026-09-25: o nome do QSA é publicado, para a ligação ser verificável.
        // O CPF mascarado serve para casar, e não vai para a resposta.
        var resultado = Montar(Basico, SocioNasDuas("BELTRANO DE TAL", "***987654**"));

        var json = System.Text.Json.JsonSerializer.Serialize(resultado);
        json.ShouldContain("BELTRANO DE TAL");
        json.ShouldNotContain("987654");
    }

    [Fact]
    public void Nome_ambiguo_nao_liga_nada()
    {
        // O mesmo nome com CPFs diferentes na base: não há como saber se é a mesma pessoa.
        var resultado = Montar(Basico, SocioNasDuas(), ambiguos: new HashSet<string> { "FULANO DE TAL" });

        resultado.Cadeias.ShouldBeEmpty();
    }

    [Fact]
    public void Mesmo_nome_com_cpf_diferente_nao_e_o_mesmo_socio()
    {
        VinculoDeSocio[] vinculos =
        [
            new(Fornecedor, "FULANO DE TAL", "***111111**", "Sócio"),
            new(OutraEmpresa, "FULANO DE TAL", "***999999**", "Sócio"),
        ];

        Montar(Basico, vinculos).Cadeias.ShouldBeEmpty();
    }

    [Fact]
    public void Socio_de_alcance_alto_nao_aparece()
    {
        // Seis parlamentares pagam empresas dele: é infraestrutura, não relação escolhida.
        var outros = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var pagamentos = Basico
            .Concat(outros.Select(id => new PagamentoAgregado(id, OutraEmpresa, 60_000m)))
            .ToList();

        Montar(pagamentos, SocioNasDuas(), parlamentares: outros).Cadeias.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(49_999, 60_000)]
    [InlineData(80_000, 49_999)]
    public void Cada_ponta_precisa_de_cinquenta_mil(double doParlamentar, double doOutro)
    {
        PagamentoAgregado[] pagamentos =
        [
            new(Eu, Fornecedor, (decimal)doParlamentar),
            new(Outro, OutraEmpresa, (decimal)doOutro),
        ];

        Montar(pagamentos, SocioNasDuas()).Cadeias.ShouldBeEmpty();
    }

    [Fact]
    public void Filial_nao_e_outra_empresa()
    {
        var empresas = new Dictionary<Guid, EmpresaDaCadeia>
        {
            [Fornecedor] = new(Fornecedor, "11111111000111", "11.111.111/0001-11", "AGENCIA A LTDA"),
            [OutraEmpresa] = new(OutraEmpresa, "11111111000292", "11.111.111/0002-92", "AGENCIA A LTDA"),
        };

        Montar(Basico, SocioNasDuas(), empresas: empresas).Cadeias.ShouldBeEmpty();
    }

    [Fact]
    public void O_proprio_parlamentar_nao_e_a_outra_ponta()
    {
        PagamentoAgregado[] pagamentos =
        [
            new(Eu, Fornecedor, 80_000m),
            new(Eu, OutraEmpresa, 80_000m),
            new(Outro, Fornecedor, 1_000m),
        ];

        Montar(pagamentos, SocioNasDuas()).Cadeias.ShouldBeEmpty();
    }

    [Fact]
    public void Dois_socios_nas_mesmas_duas_empresas_sao_uma_cadeia_com_dois_socios()
    {
        var vinculos = SocioNasDuas().Concat(SocioNasDuas("CICLANO", "***654321**")).ToList();

        var cadeia = Montar(Basico, vinculos).Cadeias.ShouldHaveSingleItem();

        cadeia.SociosEmComum.ShouldBe(2);
        cadeia.OutrosParlamentares.Count.ShouldBe(1);
    }

    [Fact]
    public void Traz_as_maiores_e_conta_todas()
    {
        var resultado = CadeiasDeSocios.Montar(
            Eu, Basico, SocioNasDuas(), new HashSet<string>(), Empresas, Parlamentares(Eu, Outro), quantas: 0);

        resultado.Cadeias.ShouldBeEmpty();
        resultado.TotalDeCadeias.ShouldBe(1);
    }
}
