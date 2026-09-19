using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Estado;

/// <summary>
/// O registro de uma alteração na estrutura do Estado.
///
/// <para>Existe porque <b>o SIORG não tem histórico</b> — medido: <c>operacao</c>,
/// <c>codigoUnidadePaiAnterior</c> e <c>dataFinalVersaoConsulta</c> chegam vazios, e os
/// parâmetros de versão são aceitos e ignorados. Sem esta tabela, o produto só saberia
/// dizer como o Estado está hoje, nunca o que mudou.</para>
/// </summary>
public sealed class MudancaEstruturalTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 19, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Criacao_de_unidade_nao_tem_campo_nem_valor_anterior()
    {
        var mudanca = MudancaEstrutural.Criacao(26, "Presidência da República", Agora);

        mudanca.Tipo.ShouldBe(TipoDeMudanca.Criada);
        mudanca.Campo.ShouldBeNull();
        mudanca.De.ShouldBeNull();
        mudanca.Para.ShouldBeNull();
        mudanca.DetectadaEm.ShouldBe(Agora);
    }

    [Fact]
    public void O_nome_fica_congelado_no_registro()
    {
        // Guardar só o código faria uma mudança de 2027 ficar ilegível assim que o órgão
        // fosse renomeado — a linha diria "a unidade 26 mudou de pai" e quem lê não saberia
        // qual órgão era à época. O nome do momento é parte do fato.
        var mudanca = MudancaEstrutural.Alteracao(
            26, "Secretaria de Comunicação Social", new MudancaDeCampo("Nome", "SECOM", "SECOM/PR"), Agora);

        mudanca.NomeNaEpoca.ShouldBe("Secretaria de Comunicação Social");
        mudanca.Tipo.ShouldBe(TipoDeMudanca.Alterada);
        mudanca.Campo.ShouldBe("Nome");
        mudanca.De.ShouldBe("SECOM");
        mudanca.Para.ShouldBe("SECOM/PR");
    }

    [Fact]
    public void Desaparecimento_e_reaparecimento_sao_tipos_proprios()
    {
        MudancaEstrutural.Desaparecimento(26, "Presidência da República", Agora)
            .Tipo.ShouldBe(TipoDeMudanca.Desaparecida);

        MudancaEstrutural.Reaparecimento(26, "Presidência da República", Agora)
            .Tipo.ShouldBe(TipoDeMudanca.Reaparecida);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Mudanca_sem_unidade_valida_e_recusada(int codigo)
    {
        Should.Throw<ArgumentException>(() =>
            MudancaEstrutural.Criacao(codigo, "Qualquer", Agora));
    }

    [Fact]
    public void Mudanca_sem_nome_e_recusada()
    {
        Should.Throw<ArgumentException>(() => MudancaEstrutural.Criacao(26, "   ", Agora));
    }

    [Fact]
    public void A_descricao_para_leitura_nao_usa_o_nome_interno_do_campo()
    {
        // ADR-0004 vale aqui como vale para as regras: "CodigoPai" é telemetria. Quem lê a
        // linha do tempo do Estado precisa de "passou a responder a outro órgão".
        var mudanca = MudancaEstrutural.Alteracao(
            26, "Advocacia-Geral da União", new MudancaDeCampo("CodigoPai", "26", "244322"), Agora);

        mudanca.Descrever().ShouldNotContain("CodigoPai");
        mudanca.Descrever().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Toda_mudanca_de_campo_conhecida_tem_descricao_propria()
    {
        // Trava de deriva: acrescentar um campo comparável em UnidadeDoEstado.Atualizar sem
        // dar a ele uma frase legível produziria uma linha que o leitor não entende.
        string[] campos =
        [
            "Nome", "Sigla", "CodigoPai", "CodigoOrgao",
            "Poder", "Esfera", "Tipo", "NaturezaJuridica", "Normatizacao",
        ];

        foreach (var campo in campos)
        {
            var mudanca = MudancaEstrutural.Alteracao(
                26, "Unidade", new MudancaDeCampo(campo, "a", "b"), Agora);

            mudanca.Descrever().ShouldNotContain(campo, Case.Sensitive,
                $"o campo {campo} está caindo no texto genérico");
        }
    }

    // ------------------------------------------------------------------
    //  O selo do cartao
    // ------------------------------------------------------------------

    [Fact]
    public void Cada_tipo_tem_chave_e_selo_proprios()
    {
        // Duas chaves iguais fariam dois tipos de mudanca virarem o mesmo selo; dois selos
        // iguais fariam "criada" e "desapareceu" parecerem a mesma noticia.
        var mudancas = new[]
        {
            MudancaEstrutural.Criacao(26, "X", Agora),
            MudancaEstrutural.Desaparecimento(26, "X", Agora),
            MudancaEstrutural.Reaparecimento(26, "X", Agora),
            MudancaEstrutural.Alteracao(26, "X", new MudancaDeCampo("Nome", "a", "b"), Agora),
        };

        mudancas.Select(m => m.Chave()).Distinct().Count().ShouldBe(4);
        mudancas.Select(m => m.Selo()).Distinct().Count().ShouldBe(4);

        foreach (var mudanca in mudancas)
        {
            mudanca.Chave().ShouldNotBe("outra");
            mudanca.Selo().ShouldNotBe("Mudou");
        }
    }

    [Fact]
    public void A_chave_nao_e_o_selo()
    {
        // E' o ponto de existirem as duas: a chave decide a cor, o selo e' lido.
        var mudanca = MudancaEstrutural.Criacao(26, "X", Agora);

        mudanca.Chave().ShouldNotBe(mudanca.Selo());
        mudanca.Chave().ShouldBe(mudanca.Chave().ToLowerInvariant());
    }

    [Fact]
    public void O_selo_e_curto_o_bastante_para_caber_ao_lado_do_nome()
    {
        // Selo mais longo que o nome do orgao inverte a hierarquia da leitura.
        foreach (var tipo in Enum.GetValues<TipoDeMudanca>())
        {
            var mudanca = tipo switch
            {
                TipoDeMudanca.Criada => MudancaEstrutural.Criacao(26, "X", Agora),
                TipoDeMudanca.Desaparecida => MudancaEstrutural.Desaparecimento(26, "X", Agora),
                TipoDeMudanca.Reaparecida => MudancaEstrutural.Reaparecimento(26, "X", Agora),
                _ => MudancaEstrutural.Alteracao(26, "X", new MudancaDeCampo("Nome", "a", "b"), Agora),
            };

            mudanca.Selo().Length.ShouldBeLessThanOrEqualTo(20);
        }
    }
}
