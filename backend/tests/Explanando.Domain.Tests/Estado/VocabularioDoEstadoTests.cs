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

    // ------------------------------------------------------------------
    //  As chaves estaveis, que o desenho consome
    // ------------------------------------------------------------------

    [Fact]
    public void Toda_chave_de_tipo_e_unica_e_estavel()
    {
        // Duas chaves iguais fariam dois tipos virarem a mesma forma no grafo.
        var chaves = Enum.GetValues<TipoDeUnidade>().Select(VocabularioDoEstado.ChaveDe).ToList();

        chaves.Distinct().Count().ShouldBe(chaves.Count);
        chaves.ShouldNotContain("desconhecido");
    }

    [Fact]
    public void Toda_chave_de_poder_e_unica_e_estavel()
    {
        var chaves = Enum.GetValues<Poder>().Select(VocabularioDoEstado.ChaveDe).ToList();

        chaves.Distinct().Count().ShouldBe(chaves.Count);
        chaves.ShouldNotContain("desconhecido");
    }

    [Fact]
    public void A_chave_nao_e_o_rotulo_de_tela()
    {
        // E' o ponto inteiro de existirem as duas. Se a chave FOSSE o rotulo, melhorar a
        // redacao mudaria o desenho — sem erro e sem teste vermelho.
        foreach (var tipo in Enum.GetValues<TipoDeUnidade>())
        {
            VocabularioDoEstado.ChaveDe(tipo).ShouldNotBe(VocabularioDoEstado.De(tipo));
        }
    }

    [Fact]
    public void A_chave_nao_tem_acento_nem_maiuscula()
    {
        // Ela vai para atributo de CSS e para chave de dicionario no navegador.
        foreach (var tipo in Enum.GetValues<TipoDeUnidade>())
        {
            var chave = VocabularioDoEstado.ChaveDe(tipo);

            chave.ShouldBe(chave.ToLowerInvariant());
            chave.ShouldAllBe(c => char.IsAsciiLetterLower(c) || c == '-');
        }
    }

    // ------------------------------------------------------------------
    //  O anel: o raio como natureza juridica
    // ------------------------------------------------------------------

    [Fact]
    public void O_anel_separa_direta_de_indireta()
    {
        // A distincao nao e' de tamanho, e' de natureza: orgao e' braco do proprio ente,
        // sem personalidade juridica; entidade tem personalidade e patrimonio proprios.
        // E' o equivalente brasileiro do "nivel de autoridade" da referencia.
        VocabularioDoEstado.AnelDe(TipoDeUnidade.Ente)
            .ShouldBeLessThan(VocabularioDoEstado.AnelDe(TipoDeUnidade.Orgao));

        VocabularioDoEstado.AnelDe(TipoDeUnidade.Orgao)
            .ShouldBeLessThan(VocabularioDoEstado.AnelDe(TipoDeUnidade.Entidade));

        VocabularioDoEstado.AnelDe(TipoDeUnidade.Entidade)
            .ShouldBeLessThan(VocabularioDoEstado.AnelDe(TipoDeUnidade.UnidadeColegiada));
    }

    [Fact]
    public void Todo_anel_tem_rotulo_proprio()
    {
        var rotulos = Enum.GetValues<TipoDeUnidade>()
            .Select(VocabularioDoEstado.AnelDe)
            .Distinct()
            .Select(VocabularioDoEstado.RotuloDoAnel)
            .ToList();

        rotulos.Distinct().Count().ShouldBe(rotulos.Count);
        rotulos.ShouldAllBe(rotulo => rotulo.Length > 0);
    }
}
