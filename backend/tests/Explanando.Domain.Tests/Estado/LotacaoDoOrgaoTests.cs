using System.Reflection;
using Explanando.Domain.Comum;
using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Estado;

/// <summary>
/// O quadro de pessoal de um órgão, em números.
///
/// <para><b>A decisão que define esta entidade é o que ela NÃO guarda.</b> A fonte traz
/// 778.337 vínculos nominais, com nome e CPF mascarado. O produto precisa de "quantas
/// pessoas, em que funções" — e essa pergunta se responde com contagens. Guardar a lista
/// nominal traria dado pessoal em escala, 444 MB por mês e uma avaliação de legítimo
/// interesse, para entregar exatamente a mesma tela.</para>
///
/// <para>Então a agregação acontece <b>na ingestão</b>, antes de qualquer gravação, e a
/// entidade é incapaz de representar uma pessoa. O último teste aqui é o que impede isso de
/// mudar por descuido.</para>
/// </summary>
public sealed class LotacaoDoOrgaoTests
{
    private static readonly Competencia Julho = Competencia.Criar(2026, 7);

    private static NovaLotacao Valida(Action<NovaLotacaoConstrutor>? ajustar = null)
    {
        var c = new NovaLotacaoConstrutor();
        ajustar?.Invoke(c);
        return c.Construir();
    }

    internal sealed class NovaLotacaoConstrutor
    {
        public int CodigoSiafi { get; set; } = 26298;
        public string NomeNaOrigem { get; set; } = "Universidade Federal de Sergipe";
        public int? CodigoSiorg { get; set; } = 471;
        public int Vinculos { get; set; } = 3_000;
        public IReadOnlyDictionary<string, int> PorFuncao { get; set; } =
            new Dictionary<string, int> { ["CD"] = 12, ["FG"] = 30 };

        public NovaLotacao Construir() => new()
        {
            Competencia = Julho,
            CodigoSiafi = CodigoSiafi,
            NomeNaOrigem = NomeNaOrigem,
            CodigoSiorg = CodigoSiorg,
            Vinculos = Vinculos,
            PorFuncao = PorFuncao,
        };
    }

    // ------------------------------------------------------------------
    //  O que a fonte manda, a entidade guarda — em numeros
    // ------------------------------------------------------------------

    [Fact]
    public void Guarda_a_contagem_e_deriva_quem_tem_funcao()
    {
        var lotacao = LotacaoDoOrgao.Criar(Valida());

        lotacao.CodigoSiafi.ShouldBe(26298);
        lotacao.Vinculos.ShouldBe(3_000);

        // Derivado, e nao recebido: somar as funcoes aqui e' uma conta so', e recebe-la
        // pronta abriria a porta para um total que nao bate com as partes.
        lotacao.ComFuncaoDeConfianca.ShouldBe(42);
        lotacao.Funcoes.Count.ShouldBe(2);
    }

    [Fact]
    public void Orgao_sem_ninguem_em_funcao_e_registro_valido()
    {
        // A maioria dos orgaos pequenos nao tem cargo comissionado nenhum, e zero aqui e'
        // zero medido — nao e' ausencia de dado.
        var lotacao = LotacaoDoOrgao.Criar(Valida(c => c.PorFuncao = new Dictionary<string, int>()));

        lotacao.ComFuncaoDeConfianca.ShouldBe(0);
        lotacao.Funcoes.ShouldBeEmpty();
    }

    [Fact]
    public void Orgao_sem_par_no_grafo_entra_assim_mesmo()
    {
        // Medido: ~4% dos vinculos estao em rubricas que o SIORG nao tem — os governos dos
        // ex-territorios, por exemplo. Recusar o registro apagaria o numero; guarda-lo com
        // o par nulo deixa a tela dizer "sem correspondencia no grafo", que e' a verdade.
        var lotacao = LotacaoDoOrgao.Criar(Valida(c => c.CodigoSiorg = null));

        lotacao.CodigoSiorg.ShouldBeNull();
        lotacao.Vinculos.ShouldBe(3_000);
    }

    // ------------------------------------------------------------------
    //  O que quebraria a contagem
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Codigo_de_orgao_nao_positivo_e_recusado(int codigo)
    {
        Should.Throw<ArgumentException>(() =>
            LotacaoDoOrgao.Criar(Valida(c => c.CodigoSiafi = codigo)));
    }

    [Fact]
    public void Vinculos_negativos_sao_recusados()
    {
        Should.Throw<ArgumentException>(() =>
            LotacaoDoOrgao.Criar(Valida(c => c.Vinculos = -1)));
    }

    [Fact]
    public void Mais_gente_em_funcao_do_que_no_orgao_e_recusado()
    {
        // Nao e' zelo excessivo: e' a unica checagem que pega um erro de agregacao — somar
        // a mesma pessoa duas vezes, ou contar funcao de quem esta' lotado noutro lugar.
        // Sem ela, a tela mostraria "120% em cargo de confianca" sem ninguem estranhar.
        Should.Throw<ArgumentException>(() => LotacaoDoOrgao.Criar(Valida(c =>
        {
            c.Vinculos = 10;
            c.PorFuncao = new Dictionary<string, int> { ["CD"] = 11 };
        })));
    }

    [Fact]
    public void Funcao_com_quantidade_negativa_e_recusada()
    {
        Should.Throw<ArgumentException>(() => LotacaoDoOrgao.Criar(Valida(c =>
            c.PorFuncao = new Dictionary<string, int> { ["CD"] = -2 })));
    }

    [Fact]
    public void Sigla_de_funcao_em_branco_e_descartada_sem_derrubar_o_registro()
    {
        // A fonte usa "-1" e vazio para "sem funcao". Sao ruido de codificacao, nao uma
        // funcao chamada "-1" — e recusar o orgao inteiro por causa disso perderia o
        // quadro de pessoal de quem tem muita gente sem cargo comissionado.
        var lotacao = LotacaoDoOrgao.Criar(Valida(c => c.PorFuncao = new Dictionary<string, int>
        {
            ["CD"] = 5,
            ["-1"] = 900,
            ["  "] = 3,
        }));

        lotacao.Funcoes.Select(f => f.Sigla).ShouldBe(["CD"]);
        lotacao.ComFuncaoDeConfianca.ShouldBe(5);
    }

    // ------------------------------------------------------------------
    //  A invariante que protege 778 mil pessoas
    // ------------------------------------------------------------------

    [Fact]
    public void A_entidade_e_incapaz_de_guardar_uma_pessoa()
    {
        // Esta e' a garantia estrutural, e nao uma promessa em comentario: se alguem
        // acrescentar Nome, Cpf ou Matricula aqui, o build fica vermelho antes de o
        // primeiro dado pessoal chegar ao banco.
        string[] proibidos = ["NOME", "CPF", "MATRICULA", "SERVIDOR", "PESSOA", "REMUNERA"];

        var propriedades = typeof(LotacaoDoOrgao)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToUpperInvariant())
            // NomeNaOrigem e' o nome do ORGAO como o SIAPE o escreve, e e' o que permite
            // auditar a ponte. Nao e' nome de pessoa.
            .Where(nome => nome != "NOMENAORIGEM")
            .ToList();

        foreach (var propriedade in propriedades)
        {
            foreach (var proibido in proibidos)
            {
                propriedade.ShouldNotContain(proibido, Case.Sensitive,
                    $"a propriedade {propriedade} sugere dado de pessoa numa entidade de contagem");
            }
        }
    }
}
