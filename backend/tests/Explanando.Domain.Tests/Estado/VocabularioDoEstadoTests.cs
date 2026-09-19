using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Estado;

/// <summary>
/// A trava de deriva do vocabulário do grafo.
///
/// <para>Um valor novo num enum não quebra compilação — cai no <c>_</c> e vira "Unidade" ou
/// "Não classificado" na tela, em silêncio. Estes testes transformam esse silêncio em
/// falha de build, que é o mesmo cuidado que o <c>CatalogoDeRegras</c> já recebe.</para>
/// </summary>
public sealed class VocabularioDoEstadoTests
{
    [Fact]
    public void Todo_poder_tem_nome_proprio()
    {
        foreach (var poder in Enum.GetValues<Poder>())
        {
            VocabularioDoEstado.De(poder)
                .ShouldNotBe(VocabularioDoEstado.NaoClassificado, $"o poder {poder} caiu no padrão");
        }
    }

    [Fact]
    public void Todo_tipo_de_unidade_tem_nome_proprio()
    {
        foreach (var tipo in Enum.GetValues<TipoDeUnidade>())
        {
            VocabularioDoEstado.De(tipo).ShouldNotBe("Unidade", $"o tipo {tipo} caiu no padrão");
        }
    }

    [Fact]
    public void Toda_natureza_juridica_tem_nome_proprio()
    {
        foreach (var natureza in Enum.GetValues<NaturezaJuridica>())
        {
            VocabularioDoEstado.De(natureza)
                .ShouldNotContain("não informada", Case.Insensitive,
                    $"a natureza {natureza} caiu no padrão");
        }
    }

    [Fact]
    public void A_base_legal_diz_o_que_e_preciso_para_a_unidade_sumir()
    {
        // E' a traducao de maior peso da lista: "ATO_INTERNO" e' jargao, e a diferenca
        // entre "existe por lei" e "existe por portaria" e' informacao que muda a leitura.
        VocabularioDoEstado.De(NivelDeNormatizacao.LeiOuDecreto).ShouldContain("lei");
        VocabularioDoEstado.De(NivelDeNormatizacao.AtoInterno).ShouldContain("ato interno");
    }

    [Fact]
    public void Nenhum_rotulo_vaza_nome_interno_de_enum()
    {
        // Um rotulo que saisse como "UnidadeColegiada" ou "FuncoesEssenciaisAJustica"
        // poria nome de identificador na legenda de um grafico publico. O sinal e' a
        // maiuscula no meio da palavra.
        IEnumerable<string> rotulos =
        [
            .. Enum.GetValues<Poder>().Select(VocabularioDoEstado.De),
            .. Enum.GetValues<TipoDeUnidade>().Select(VocabularioDoEstado.De),
            .. Enum.GetValues<NaturezaJuridica>().Select(VocabularioDoEstado.De),
            .. Enum.GetValues<NivelDeNormatizacao>().Select(VocabularioDoEstado.De),
            .. Enum.GetValues<EsferaDeGoverno>().Select(VocabularioDoEstado.De),
        ];

        foreach (var rotulo in rotulos)
        {
            // Maiuscula colada em minuscula: "UnidadeColegiada" cai, "Funcoes essenciais
            // a Justica" passa — a maiuscula depois do espaco e' nome proprio, nao codigo.
            var pascalCase = rotulo
                .Zip(rotulo.Skip(1))
                .Any(par => char.IsLower(par.First) && char.IsUpper(par.Second));

            pascalCase.ShouldBeFalse(
                $"'{rotulo}' parece nome de identificador, nao texto para leitura");
        }
    }
}
