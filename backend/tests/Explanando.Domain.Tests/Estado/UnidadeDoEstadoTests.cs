using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Estado;

/// <summary>
/// O nó do grafo do Estado.
///
/// <para>É a primeira entidade de um contexto novo: enquanto o resto do produto responde
/// "quanto se gastou", esta responde <b>"que órgãos existem, de quem cada um depende e sob
/// que norma"</b>. O grafo é a adaptação ao Brasil do CivLab, e a fonte é o SIORG.</para>
///
/// <para>As invariantes aqui são poucas de propósito. O SIORG é cadastro oficial, não
/// formulário aberto — inventar validação sobre dado que o Estado publica sobre si mesmo
/// produziria rejeição de registro legítimo, que é o oposto do que um mapa do Estado deve
/// fazer. O que se valida é o que <b>quebraria o grafo</b>: nó sem identidade, nó sem
/// rótulo, e aresta que aponta para si mesma.</para>
/// </summary>
public sealed class UnidadeDoEstadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 19, 3, 0, 0, TimeSpan.Zero);

    private static NovaUnidadeDoEstado Valida(Action<NovaUnidadeDoEstadoConstrutor>? ajustar = null)
    {
        var construtor = new NovaUnidadeDoEstadoConstrutor();
        ajustar?.Invoke(construtor);
        return construtor.Construir();
    }

    internal sealed class NovaUnidadeDoEstadoConstrutor
    {
        public int Codigo { get; set; } = 26;
        public int? CodigoPai { get; set; }
        public int? CodigoOrgao { get; set; } = 26;
        public string Nome { get; set; } = "Presidência da República";
        public string? Sigla { get; set; } = "PR";
        public Poder? Poder { get; set; } = Domain.Estado.Poder.Executivo;
        public EsferaDeGoverno? Esfera { get; set; } = EsferaDeGoverno.Federal;
        public NaturezaJuridica? Natureza { get; set; } = NaturezaJuridica.AdministracaoDireta;
        public TipoDeUnidade Tipo { get; set; } = TipoDeUnidade.Orgao;
        public NivelDeNormatizacao? Normatizacao { get; set; } = NivelDeNormatizacao.LeiOuDecreto;

        public NovaUnidadeDoEstado Construir() => new()
        {
            Codigo = Codigo,
            CodigoPai = CodigoPai,
            CodigoOrgao = CodigoOrgao,
            Nome = Nome,
            Sigla = Sigla,
            Poder = Poder,
            Esfera = Esfera,
            NaturezaJuridica = Natureza,
            Tipo = Tipo,
            Normatizacao = Normatizacao,
        };
    }

    // ------------------------------------------------------------------
    //  O que quebraria o grafo
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Codigo_nao_positivo_e_recusado(int codigo)
    {
        // Sem código não há nó, e não há como uma aresta apontar para ele.
        Should.Throw<ArgumentException>(() =>
            UnidadeDoEstado.Criar(Valida(c => c.Codigo = codigo), Agora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_em_branco_e_recusado(string nome)
    {
        // Nó sem rótulo é um ponto no desenho que ninguém consegue ler nem procurar.
        Should.Throw<ArgumentException>(() =>
            UnidadeDoEstado.Criar(Valida(c => c.Nome = nome), Agora));
    }

    [Fact]
    public void Unidade_nao_pode_ser_pai_de_si_mesma()
    {
        // O ciclo mais curto possível. Um grafo com ele trava qualquer travessia em
        // profundidade — e a fonte é cadastro humano, então isto não é hipótese remota.
        Should.Throw<ArgumentException>(() =>
            UnidadeDoEstado.Criar(Valida(c =>
            {
                c.Codigo = 26;
                c.CodigoPai = 26;
            }), Agora));
    }

    // ------------------------------------------------------------------
    //  O que a fonte manda, a entidade guarda
    // ------------------------------------------------------------------

    [Fact]
    public void Guarda_o_que_o_siorg_publica()
    {
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        unidade.Codigo.ShouldBe(26);
        unidade.Nome.ShouldBe("Presidência da República");
        unidade.Sigla.ShouldBe("PR");
        unidade.Poder.ShouldBe(Poder.Executivo);
        unidade.Esfera.ShouldBe(EsferaDeGoverno.Federal);
        unidade.NaturezaJuridica.ShouldBe(Domain.Estado.NaturezaJuridica.AdministracaoDireta);
        unidade.Tipo.ShouldBe(TipoDeUnidade.Orgao);
        unidade.Normatizacao.ShouldBe(NivelDeNormatizacao.LeiOuDecreto);
    }

    [Fact]
    public void Nome_e_sigla_chegam_aparados()
    {
        var unidade = UnidadeDoEstado.Criar(Valida(c =>
        {
            c.Nome = "  Presidência da República  ";
            c.Sigla = " PR ";
        }), Agora);

        unidade.Nome.ShouldBe("Presidência da República");
        unidade.Sigla.ShouldBe("PR");
    }

    [Fact]
    public void Sem_pai_e_raiz()
    {
        UnidadeDoEstado.Criar(Valida(c => c.CodigoPai = null), Agora).EhRaiz.ShouldBeTrue();
        UnidadeDoEstado.Criar(Valida(c => c.CodigoPai = 1), Agora).EhRaiz.ShouldBeFalse();
    }

    [Fact]
    public void A_raiz_do_grafo_nao_tem_poder_nem_esfera_e_mesmo_assim_entra()
    {
        // Medido na fonte: a unidade 200000, "República Federativa do Brasil", devolve
        // `codigoPoder: ""` e `codigoEsfera: ""`; a União (200001) devolve poder e natureza
        // nulos. Exigir esses campos recusaria a raiz da árvore inteira — e um grafo do
        // Estado que começa no segundo nível não é um mapa, é um recorte.
        var raiz = UnidadeDoEstado.Criar(Valida(c =>
        {
            c.Codigo = 200000;
            c.CodigoPai = null;
            c.Nome = "República Federativa do Brasil";
            c.Sigla = " ";
            c.Poder = null;
            c.Esfera = null;
            c.Natureza = null;
            c.Tipo = TipoDeUnidade.Ente;
        }), Agora);

        raiz.EhRaiz.ShouldBeTrue();
        raiz.Poder.ShouldBeNull();
        raiz.Esfera.ShouldBeNull();

        // A sigla da raiz e' um espaco em branco na origem, nao uma sigla de um caractere.
        raiz.Sigla.ShouldBeNull();
    }

    [Fact]
    public void Natureza_e_normatizacao_podem_faltar_sem_recusa()
    {
        // A postura permissiva do ADR-0007 vale aqui também: o SIORG deixa campos vazios em
        // parte do cadastro, e recusar o registro por isso apagaria do mapa um órgão que
        // existe. Ausência vira nulo declarado, não exceção.
        var unidade = UnidadeDoEstado.Criar(Valida(c =>
        {
            c.Natureza = null;
            c.Normatizacao = null;
            c.Sigla = null;
        }), Agora);

        unidade.NaturezaJuridica.ShouldBeNull();
        unidade.Normatizacao.ShouldBeNull();
        unidade.Sigla.ShouldBeNull();
    }

    // ------------------------------------------------------------------
    //  A atualização, que é a semente do rastreador de mudanças
    // ------------------------------------------------------------------

    [Fact]
    public void Atualizar_sem_diferenca_nao_reporta_mudanca()
    {
        // O SIORG não tem endpoint de histórico — medido: `operacao` e
        // `codigoUnidadePaiAnterior` chegam vazios, e os parâmetros de versão são
        // ignorados. Então a mudança estrutural é DEDUZIDA comparando coletas, e uma
        // coleta idêntica precisa reportar silêncio.
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        unidade.Atualizar(Valida(), Agora).ShouldBeEmpty();
    }

    [Fact]
    public void Atualizar_reporta_o_que_mudou_e_aplica()
    {
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        var mudancas = unidade.Atualizar(Valida(c =>
        {
            c.Nome = "Presidência da República Federativa";
            c.CodigoPai = 1;
        }), Agora);

        mudancas.Count.ShouldBe(2);
        mudancas.ShouldContain(m => m.Campo == "Nome" && m.De == "Presidência da República");
        mudancas.ShouldContain(m => m.Campo == "CodigoPai" && m.De == null && m.Para == "1");

        unidade.Nome.ShouldBe("Presidência da República Federativa");
        unidade.CodigoPai.ShouldBe(1);
    }

    [Fact]
    public void Atualizar_com_outro_codigo_e_recusado()
    {
        // O código é a identidade do nó. Trocá-lo numa atualização não seria mudança da
        // unidade — seria confundir duas unidades diferentes numa linha só.
        var unidade = UnidadeDoEstado.Criar(Valida(c => c.Codigo = 26), Agora);

        Should.Throw<ArgumentException>(() => unidade.Atualizar(Valida(c => c.Codigo = 27), Agora));
    }

    [Fact]
    public void Mudanca_de_pai_e_a_que_redesenha_o_grafo()
    {
        // Renomear um órgão muda um rótulo. Mudar o pai move um ramo inteiro de lugar — e é
        // o tipo de alteração que uma reforma administrativa produz sem alarde.
        var unidade = UnidadeDoEstado.Criar(Valida(c => c.CodigoPai = 1), Agora);

        var mudancas = unidade.Atualizar(Valida(c => c.CodigoPai = 2), Agora);

        mudancas.ShouldHaveSingleItem();
        mudancas[0].Campo.ShouldBe("CodigoPai");
        mudancas[0].De.ShouldBe("1");
        mudancas[0].Para.ShouldBe("2");
    }

    // ------------------------------------------------------------------
    //  Sumir da fonte nao e' ser extinta
    // ------------------------------------------------------------------

    [Fact]
    public void Unidade_que_some_fica_inativa_mas_permanece_na_base()
    {
        // O SIORG nao distingue extincao de incorporacao nem de "o cadastro parou de
        // publicar". Apagar a linha perderia o pedaco do mapa que explica de onde veio o
        // que existe hoje — e afirmar extincao seria inventar um fato juridico a partir de
        // um silencio de API.
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        unidade.Ativa.ShouldBeTrue();
        unidade.MarcarDesaparecida(Agora).ShouldBeTrue();
        unidade.Ativa.ShouldBeFalse();
        unidade.Nome.ShouldBe("Presidência da República");
    }

    [Fact]
    public void Desaparecimento_repetido_nao_vira_noticia_nova()
    {
        // Sem isto, toda coleta mensal repetiria para sempre a mesma linha sobre o mesmo
        // orgao, e a tela de mudancas ficaria inutil depois do terceiro mes.
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        unidade.MarcarDesaparecida(Agora).ShouldBeTrue();
        unidade.MarcarDesaparecida(Agora.AddDays(30)).ShouldBeFalse();
    }

    [Fact]
    public void Unidade_que_volta_reaparece_uma_vez_so()
    {
        var unidade = UnidadeDoEstado.Criar(Valida(), Agora);

        unidade.MarcarReaparecida(Agora).ShouldBeFalse();

        unidade.MarcarDesaparecida(Agora);

        unidade.MarcarReaparecida(Agora.AddDays(30)).ShouldBeTrue();
        unidade.MarcarReaparecida(Agora.AddDays(60)).ShouldBeFalse();
        unidade.Ativa.ShouldBeTrue();
    }
}
