using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// REG_NET_004, a única regra que cruza parlamentares. Nenhuma das três condições isolada
/// significa coisa alguma — o teste existe para provar que ela só fala quando as três
/// coincidem, e que se cala em cada caso em que uma delas cai.
/// </summary>
public sealed class RegrasDeRedeTests
{
    private const string CnpjNovo = "36826246000164";
    private const string CnpjOutro = "45997418000153";

    private static readonly DateTimeOffset PrimeiraNota =
        new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private static int _sequencia;

    private static Parlamentar Deputado(string partido, string uf = "AP") =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = Interlocked.Increment(ref _sequencia),
            NomeCivil = $"DEPUTADO {_sequencia}",
            NomeParlamentar = $"Deputado {_sequencia}",
            SiglaPartido = partido,
            Uf = uf,
            Tipo = TipoParlamentar.DeputadoFederal,
        });

    private static Parlamentar Senador(string partido, string uf = "AP") =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = Interlocked.Increment(ref _sequencia),
            NomeCivil = $"SENADOR {_sequencia}",
            NomeParlamentar = $"Senador {_sequencia}",
            SiglaPartido = partido,
            Uf = uf,
            Tipo = TipoParlamentar.Senador,
        });

    private static Fornecedor Fornecedora(DateOnly abertura, string cnpj = CnpjNovo) =>
        Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(cnpj),
            RazaoSocial = "CONSULTORIA NOVA LTDA",
            CnaePrincipal = "7020400",
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataSituacaoCadastral = abertura,
            DataInicioAtividade = abertura,
            Endereco = new EnderecoFornecedor { Uf = "DF", Municipio = "BRASILIA" },
        });

    private static Despesa Nota(
        Guid parlamentarId,
        decimal valor,
        string cnpj = CnpjNovo,
        DateTimeOffset? quando = null) =>
        Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar(cnpj),
            NomeFornecedor = "CONSULTORIA NOVA LTDA",
            Competencia = Competencia.Criar(2026, 3),
            TipoDespesa = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.",
            CodigoDocumento = $"net-{Interlocked.Increment(ref _sequencia)}",
            NumeroDocumento = "1234",
            DataDocumento = quando ?? PrimeiraNota,
            ValorDocumento = valor,
            ValorLiquido = valor,
        });

    /// <summary>Três deputados do mesmo partido, um fornecedor aberto pouco antes.</summary>
    private static ContextoDeRede CenarioConcentrado(
        string partidoDoTerceiro = "MDB",
        DateOnly? abertura = null,
        decimal valorDoTerceiro = 5_000m)
    {
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado(partidoDoTerceiro);

        return new ContextoDeRede(
            [a, b, c],
            [
                Nota(a.Id, 5_000m),
                Nota(b.Id, 5_000m),
                Nota(c.Id, valorDoTerceiro),
            ],
            [Fornecedora(abertura ?? new DateOnly(2026, 1, 15))]);
    }

    private static List<FlagAnomalia> Rodar(ContextoDeRede contexto) =>
        new RegraConcentracaoPartidaria().Avaliar(contexto).ToList();

    // ------------------------------------------------------------------
    //  Quando as três condições coincidem
    // ------------------------------------------------------------------

    [Fact]
    public void Fala_quando_fornecedor_recem_aberto_atende_tres_deputados_do_mesmo_partido()
    {
        var flags = Rodar(CenarioConcentrado());

        flags.Count.ShouldBe(3, "uma flag por parlamentar, na maior nota de cada um");
        flags.ShouldAllBe(flag => flag.CodigoRegra == "REG_NET_004");
    }

    [Fact]
    public void Nao_afirma_certeza()
    {
        // A flag fala de um arranjo entre pessoas, não de um documento. Grau de confiança
        // 1.0 aqui seria o produto dizendo que sabe o que não sabe.
        var flag = Rodar(CenarioConcentrado())[0];

        flag.EhDeterministica.ShouldBeFalse();
        flag.GrauDeConfianca.ShouldBeLessThan(1.0m);
        flag.Gravidade.ShouldBe(Gravidade.Media);
    }

    [Fact]
    public void A_descricao_diz_que_a_coincidencia_nao_caracteriza_irregularidade()
    {
        // Sem esta ressalva, a frase mais próxima do que o leitor lê seria uma acusação.
        Rodar(CenarioConcentrado())[0]
            .Descricao.ShouldContain("não caracteriza");
    }

    [Fact]
    public void Marca_uma_despesa_por_parlamentar_e_nao_todas()
    {
        // Marcar todas transformaria um achado só em centenas de linhas idênticas e
        // inflaria o peso do parlamentar no índice por um único fato.
        var deputadoA = Deputado("MDB");
        var deputadoB = Deputado("MDB");
        var deputadoC = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [deputadoA, deputadoB, deputadoC],
            [
                Nota(deputadoA.Id, 2_000m),
                Nota(deputadoA.Id, 9_000m),
                Nota(deputadoA.Id, 3_000m),
                Nota(deputadoB.Id, 5_000m),
                Nota(deputadoC.Id, 5_000m),
            ],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        Rodar(contexto).Count.ShouldBe(3);
    }

    // ------------------------------------------------------------------
    //  Quando alguma condição cai
    // ------------------------------------------------------------------

    [Fact]
    public void Cala_quando_um_dos_parlamentares_e_de_outro_partido()
    {
        // Exclusividade literal, como no texto da especificação: um único deputado de
        // fora desliga a regra.
        Rodar(CenarioConcentrado(partidoDoTerceiro: "PT")).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_com_menos_de_tres_parlamentares()
    {
        // Dois gabinetes vizinhos usando a mesma gráfica é coincidência banal.
        var a = Deputado("MDB");
        var b = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [a, b],
            [Nota(a.Id, 5_000m), Nota(b.Id, 5_000m)],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        Rodar(contexto).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_quando_o_fornecedor_e_antigo()
    {
        // Empresa consolidada atendendo vários deputados do mesmo partido é fornecedor
        // do meio político, não sinal.
        Rodar(CenarioConcentrado(abertura: new DateOnly(2010, 5, 1))).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_quando_a_relacao_com_um_deles_e_de_valor_irrisorio()
    {
        // Sem o piso de valor, uma nota de R$ 30 de estacionamento faria o fornecedor
        // "atender" aquele gabinete e completaria o trio sozinha.
        Rodar(CenarioConcentrado(valorDoTerceiro: 30m)).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_quando_a_data_de_abertura_e_desconhecida()
    {
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado("MDB");

        var semAbertura = Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(CnpjNovo),
            RazaoSocial = "CONSULTORIA NOVA LTDA",
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataInicioAtividade = null,
            Endereco = new EnderecoFornecedor { Uf = "DF" },
        });

        var contexto = new ContextoDeRede(
            [a, b, c],
            [Nota(a.Id, 5_000m), Nota(b.Id, 5_000m), Nota(c.Id, 5_000m)],
            [semAbertura]);

        Rodar(contexto).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_quando_o_fornecedor_nao_foi_resolvido()
    {
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [a, b, c],
            [Nota(a.Id, 5_000m), Nota(b.Id, 5_000m), Nota(c.Id, 5_000m)],
            []);

        Rodar(contexto).ShouldBeEmpty();
    }

    [Fact]
    public void Cala_quando_o_partido_de_um_deles_e_desconhecido()
    {
        // Ele pode ser justamente o de fora; afirmar exclusividade seria inventar.
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [a, b],
            [Nota(a.Id, 5_000m), Nota(b.Id, 5_000m), Nota(c.Id, 5_000m)],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        Rodar(contexto).ShouldBeEmpty();
    }

    [Fact]
    public void Estorno_nao_conta_como_relacao()
    {
        // Bilhete aéreo cancelado tem valor líquido negativo. Contá-lo faria uma viagem
        // desfeita fundar uma "relação" com o fornecedor.
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [a, b, c],
            [
                Nota(a.Id, 5_000m),
                Nota(b.Id, 5_000m),
                Nota(c.Id, -5_000m),
            ],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        Rodar(contexto).ShouldBeEmpty();
    }

    [Fact]
    public void Fornecedores_distintos_nao_se_somam()
    {
        var a = Deputado("MDB");
        var b = Deputado("MDB");
        var c = Deputado("MDB");

        var contexto = new ContextoDeRede(
            [a, b, c],
            [
                Nota(a.Id, 5_000m),
                Nota(b.Id, 5_000m),
                Nota(c.Id, 5_000m, cnpj: CnpjOutro),
            ],
            [Fornecedora(new DateOnly(2026, 1, 15)), Fornecedora(new DateOnly(2026, 1, 15), CnpjOutro)]);

        Rodar(contexto).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  A regra atravessa as duas casas
    // ------------------------------------------------------------------

    [Fact]
    public void Concentracao_atravessa_camara_e_senado()
    {
        // A regra agrupa por fornecedor e le o partido; nunca olha a casa. E' o que permite
        // a passada de rede rodar sobre os 594 parlamentares juntos — necessario, porque o
        // Senado sozinho tem 82 e o piso da regra e' 100.
        var deputadoA = Deputado("MDB");
        var deputadoB = Deputado("MDB");
        var senador = Senador("MDB");

        var contexto = new ContextoDeRede(
            [deputadoA, deputadoB, senador],
            [
                Nota(deputadoA.Id, 5_000m),
                Nota(deputadoB.Id, 5_000m),
                Nota(senador.Id, 5_000m),
            ],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        var flags = Rodar(contexto);

        flags.Count.ShouldBe(3);
        flags[0].Descricao.ShouldContain("2 deputados e 1 senador");
    }

    [Fact]
    public void Descricao_diz_so_deputados_quando_nao_atravessa()
    {
        // "3 parlamentares (3 deputados e 0 senadores)" seria ruido. Quando a concentracao
        // fica numa casa so', a frase nao inventa uma travessia que nao houve.
        Rodar(CenarioConcentrado())[0]
            .Descricao.ShouldContain("3 deputados");
    }

    [Fact]
    public void Concentracao_so_entre_senadores_tambem_fala()
    {
        var a = Senador("PT");
        var b = Senador("PT");
        var c = Senador("PT");

        var contexto = new ContextoDeRede(
            [a, b, c],
            [Nota(a.Id, 5_000m), Nota(b.Id, 5_000m), Nota(c.Id, 5_000m)],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        var flags = Rodar(contexto);

        flags.Count.ShouldBe(3);
        flags[0].Descricao.ShouldContain("3 senadores");
    }

    [Fact]
    public void Partido_diferente_desliga_a_regra_mesmo_atravessando_as_casas()
    {
        // A exclusividade partidaria continua valendo: casa diferente nao afrouxa nada.
        var deputadoA = Deputado("MDB");
        var deputadoB = Deputado("MDB");
        var senador = Senador("PT");

        var contexto = new ContextoDeRede(
            [deputadoA, deputadoB, senador],
            [
                Nota(deputadoA.Id, 5_000m),
                Nota(deputadoB.Id, 5_000m),
                Nota(senador.Id, 5_000m),
            ],
            [Fornecedora(new DateOnly(2026, 1, 15))]);

        Rodar(contexto).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  O motor de rede
    // ------------------------------------------------------------------

    [Fact]
    public void Motor_carimba_a_versao_nas_flags_de_rede()
    {
        var resultado = MotorDeRede.Padrao().Avaliar(CenarioConcentrado());

        resultado.Flags.ShouldNotBeEmpty();
        resultado.Flags.ShouldAllBe(flag => flag.VersaoDoMotor == MotorDeRegras.VersaoAtual);
        resultado.VersaoDoMotor.ShouldBe(MotorDeRegras.VersaoAtual);
    }

    [Fact]
    public void Motor_declara_que_a_base_e_pequena_demais_para_a_regra_valer()
    {
        // Com 3 deputados na base, "exclusividade partidária" é aritmética, não padrão.
        // A Câmara tem 513: o número viaja junto para ninguém publicar achado de amostra.
        var resultado = MotorDeRede.Padrao().Avaliar(CenarioConcentrado());

        resultado.ParlamentaresNaBase.ShouldBe(3);
        resultado.PisoDeParlamentares.ShouldBe(MotorDeRede.PisoDeParlamentaresParaRede);
        resultado.BaseSuficiente.ShouldBeFalse();
    }

    [Fact]
    public void Motor_isola_falha_de_uma_regra_de_rede()
    {
        var motor = new MotorDeRede([new RegraDeRedeQuebrada(), new RegraConcentracaoPartidaria()]);

        var resultado = motor.Avaliar(CenarioConcentrado());

        resultado.Falhas.Count.ShouldBe(1);
        resultado.Falhas[0].CodigoRegra.ShouldBe("REG_XXX_997");
        resultado.Flags.Count.ShouldBe(3, "a regra boa continuou rodando");
    }

    [Fact]
    public void Motor_padrao_traz_a_concentracao_partidaria()
    {
        MotorDeRede.Padrao().Regras.Select(regra => regra.Codigo).ShouldBe(["REG_NET_004"]);
    }

    private sealed class RegraDeRedeQuebrada : IRegraDeRede
    {
        public string Codigo => "REG_XXX_997";

        public string Descricao => "Regra de rede que sempre estoura.";

        public IEnumerable<FlagAnomalia> Avaliar(ContextoDeRede contexto) =>
            throw new InvalidOperationException("Defeito proposital.");
    }
}
