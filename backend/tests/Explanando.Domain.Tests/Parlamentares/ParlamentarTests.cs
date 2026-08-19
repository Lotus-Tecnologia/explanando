using Explanando.Domain.Parlamentares;

namespace Explanando.Domain.Tests.Parlamentares;

/// <summary>
/// Modelado a partir de docs/contratos/camara-deputados-lista.json e
/// camara-deputado-detalhe.json.
///
/// Decisao de LGPD registrada na Etapa 01: o endpoint de detalhe devolve o CPF completo
/// do parlamentar, a data de nascimento e o municipio de nascimento. Nada disso alimenta
/// regra do V1, entao nada disso entra no modelo (principio da necessidade, art. 6 III).
/// </summary>
public sealed class ParlamentarTests
{
    private static Parlamentar Deputado(Action<NovoParlamentarBuilder>? ajuste = null)
    {
        var builder = new NovoParlamentarBuilder();
        ajuste?.Invoke(builder);
        return Parlamentar.Criar(builder.Construir());
    }

    [Fact]
    public void Guarda_os_dois_nomes_que_a_api_entrega()
    {
        // `nomeCivil` no detalhe, `nome` na listagem (que e' o nome eleitoral).
        var deputado = Deputado();

        deputado.NomeCivil.ShouldBe("ACÁCIO DA SILVA FAVACHO NETO");
        deputado.NomeParlamentar.ShouldBe("Acácio Favacho");
    }

    [Fact]
    public void Normaliza_a_uf_para_maiusculas()
    {
        Deputado(b => b.Uf = "ap").Uf.ShouldBe("AP");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("APX")]
    [InlineData("")]
    public void Rejeita_uf_que_nao_tem_duas_letras(string invalida)
    {
        Should.Throw<ArgumentException>(() => Deputado(b => b.Uf = invalida));
    }

    [Fact]
    public void Rejeita_id_externo_nao_positivo()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Deputado(b => b.IdExterno = 0));
    }

    [Fact]
    public void Nasce_como_deputado_federal_no_v1()
    {
        // O Senado esta fora do V1 (ADR-0002), mas o tipo ja existe para que a
        // entrada dele depois nao exija migracao de dados.
        Deputado().Tipo.ShouldBe(TipoParlamentar.DeputadoFederal);
    }

    [Fact]
    public void Identidade_externa_combina_id_e_casa()
    {
        // O id 204379 da Camara e o id 204379 do Senado seriam pessoas diferentes.
        var deputado = Deputado();

        deputado.IdExterno.ShouldBe(204379);
        deputado.Tipo.ShouldBe(TipoParlamentar.DeputadoFederal);
    }

    [Fact]
    public void Acumula_mandatos()
    {
        var deputado = Deputado();

        deputado.AdicionarMandato(legislatura: 57, inicio: new DateOnly(2023, 2, 1), fim: null);

        deputado.Mandatos.Count.ShouldBe(1);
        deputado.Mandatos.Single().Legislatura.ShouldBe(57);
        deputado.Mandatos.Single().Ativo.ShouldBeTrue("mandato sem data de fim esta em curso");
    }

    [Fact]
    public void Nao_duplica_o_mesmo_mandato_em_reingestao()
    {
        // A ingestao roda repetidas vezes sobre os mesmos dados.
        var deputado = Deputado();

        deputado.AdicionarMandato(57, new DateOnly(2023, 2, 1), null);
        deputado.AdicionarMandato(57, new DateOnly(2023, 2, 1), null);

        deputado.Mandatos.Count.ShouldBe(1);
    }

    [Fact]
    public void Mandato_encerrado_nao_fica_ativo()
    {
        var deputado = Deputado();

        deputado.AdicionarMandato(56, new DateOnly(2019, 2, 1), new DateOnly(2023, 1, 31));

        deputado.Mandatos.Single().Ativo.ShouldBeFalse();
    }

    private sealed class NovoParlamentarBuilder
    {
        public int IdExterno { get; set; } = 204379;
        public string NomeCivil { get; set; } = "ACÁCIO DA SILVA FAVACHO NETO";
        public string NomeParlamentar { get; set; } = "Acácio Favacho";
        public string SiglaPartido { get; set; } = "MDB";
        public string Uf { get; set; } = "AP";
        public TipoParlamentar Tipo { get; set; } = TipoParlamentar.DeputadoFederal;
        public string? FotoUrl { get; set; } =
            "https://www.camara.leg.br/internet/deputado/bandep/204379.jpg";

        public NovoParlamentar Construir() => new()
        {
            IdExterno = IdExterno,
            NomeCivil = NomeCivil,
            NomeParlamentar = NomeParlamentar,
            SiglaPartido = SiglaPartido,
            Uf = Uf,
            Tipo = Tipo,
            FotoUrl = FotoUrl,
        };
    }
}
