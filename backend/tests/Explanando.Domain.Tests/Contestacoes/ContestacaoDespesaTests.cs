using Explanando.Domain.Contestacoes;

namespace Explanando.Domain.Tests.Contestacoes;

/// <summary>
/// O direito de resposta do gabinete. A regra central sob teste e' negativa: a contestacao
/// <b>nao</b> passa pelo filtro de vocabulario que governa o texto do produto.
/// </summary>
public sealed class ContestacaoDespesaTests
{
    private static readonly Guid DespesaId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ParlamentarId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static ContestacaoDespesa Contestacao(
        string texto = "A despesa refere-se a locacao de veiculo para agenda parlamentar.",
        string email = "dep.fulano@camara.leg.br") =>
        ContestacaoDespesa.Criar(DespesaId, ParlamentarId, email, texto, Agora);

    [Fact]
    public void Nasce_publicada_e_visivel()
    {
        // Nao ha fila de moderacao: o produto nao julga o merito da defesa.
        var contestacao = Contestacao();

        contestacao.Situacao.ShouldBe(SituacaoDaContestacao.Publicada);
        contestacao.EstaVisivel.ShouldBeTrue();
        contestacao.EnviadaEm.ShouldBe(Agora);
    }

    [Fact]
    public void Registra_o_email_que_comprovou_a_posse()
    {
        // Fica no registro porque a autoria e' o unico controle de acesso que existe aqui.
        Contestacao(email: "  Dep.Fulano@Camara.Leg.BR  ")
            .EmailDoGabinete.ShouldBe("dep.fulano@camara.leg.br");
    }

    [Fact]
    public void Pode_responder_a_despesa_inteira_sem_apontar_flag()
    {
        Contestacao().FlagAnomaliaId.ShouldBeNull();
    }

    [Fact]
    public void Pode_responder_a_uma_flag_especifica()
    {
        var flagId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        var contestacao = ContestacaoDespesa.Criar(
            DespesaId,
            ParlamentarId,
            "gab@camara.leg.br",
            "O CNAE secundario do fornecedor cobre o servico contratado.",
            Agora,
            flagAnomaliaId: flagId);

        contestacao.FlagAnomaliaId.ShouldBe(flagId);
    }

    // ------------------------------------------------------------------
    //  A assimetria deliberada com o ADR-0004
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("A acusacao de fraude e' caluniosa e sera' levada ao Judiciario.")]
    [InlineData("Nao houve desvio algum; o site mente sobre o meu gabinete.")]
    [InlineData("Isso e' uma tentativa de lavagem de reputacao contra mim.")]
    public void Aceita_vocabulario_que_o_produto_nao_poderia_usar(string defesa)
    {
        // FlagAnomalia recusaria cada uma destas palavras. Aqui elas passam de proposito:
        // o VocabularioDeCompliance limita o que o PRODUTO afirma, e censurar a resposta
        // oficial de quem foi sinalizado seria pior que qualquer palavra que ela contenha.
        Should.NotThrow(() => Contestacao(texto: defesa));
    }

    [Fact]
    public void Recusa_texto_vazio()
    {
        Should.Throw<ArgumentException>(() => Contestacao(texto: "   "));
    }

    [Fact]
    public void Recusa_texto_acima_do_limite()
    {
        var enorme = new string('a', ContestacaoDespesa.TamanhoMaximoDoTexto + 1);

        var erro = Should.Throw<ArgumentException>(() => Contestacao(texto: enorme));

        erro.Message.ShouldContain(
            ContestacaoDespesa.TamanhoMaximoDoTexto.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Recusa_email_vazio()
    {
        Should.Throw<ArgumentException>(() => Contestacao(email: ""));
    }

    // ------------------------------------------------------------------
    //  Retirada
    // ------------------------------------------------------------------

    [Fact]
    public void Retirada_esconde_mas_nao_apaga()
    {
        // A pagina pode ter sido citada enquanto a resposta estava no ar; o registro fica.
        var contestacao = Contestacao();
        var depois = Agora.AddDays(3);

        contestacao.Retirar(depois);

        contestacao.EstaVisivel.ShouldBeFalse();
        contestacao.Situacao.ShouldBe(SituacaoDaContestacao.Retirada);
        contestacao.RetiradaEm.ShouldBe(depois);
        contestacao.Texto.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Retirar_duas_vezes_preserva_a_data_da_primeira()
    {
        var contestacao = Contestacao();
        var primeira = Agora.AddDays(1);

        contestacao.Retirar(primeira);
        contestacao.Retirar(Agora.AddDays(9));

        contestacao.RetiradaEm.ShouldBe(primeira);
    }
}
