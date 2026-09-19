namespace Explanando.Domain.Estado;

/// <summary>
/// Como cada classificação do SIORG se chama na tela.
///
/// <para><b>Mesma razão do <c>CatalogoDeRegras</c> e do ADR-0004.</b> O produto é para quem
/// não trabalha com administração pública: <c>UnidadeColegiada</c> e
/// <c>FuncoesEssenciaisAJustica</c> são nomes internos, e uma legenda de gráfico escrita
/// assim não explica nada a ninguém.</para>
///
/// <para>Fica no domínio, e não na camada de leitura, porque é vocabulário do negócio: a
/// mesma frase vale para a API, para o card compartilhável e para qualquer relatório
/// futuro. Duplicá-la em cada consumidor é como as traduções divergem.</para>
/// </summary>
public static class VocabularioDoEstado
{
    public static string De(Poder poder) => poder switch
    {
        Poder.Executivo => "Executivo",
        Poder.Legislativo => "Legislativo",
        Poder.Judiciario => "Judiciário",
        Poder.FuncoesEssenciaisAJustica => "Funções essenciais à Justiça",
        _ => "Não classificado",
    };

    public static string De(TipoDeUnidade tipo) => tipo switch
    {
        TipoDeUnidade.Ente => "Ente federativo",
        TipoDeUnidade.Entidade => "Entidade",
        TipoDeUnidade.Orgao => "Órgão",
        TipoDeUnidade.UnidadeAdministrativa => "Unidade administrativa",
        TipoDeUnidade.UnidadeColegiada => "Colegiado",
        _ => "Unidade",
    };

    public static string De(NaturezaJuridica natureza) => natureza switch
    {
        NaturezaJuridica.EmpresaPublica => "Empresa pública",
        NaturezaJuridica.FundacaoPublica => "Fundação pública",
        NaturezaJuridica.AdministracaoDireta => "Administração direta",
        NaturezaJuridica.Autarquia => "Autarquia",
        NaturezaJuridica.SociedadeDeEconomiaMista => "Sociedade de economia mista",
        NaturezaJuridica.OrgaoPublicoAutonomoFederal => "Órgão público autônomo federal",
        _ => "Forma jurídica não informada",
    };

    /// <summary>
    /// A frase que diz o que é preciso para a unidade deixar de existir.
    ///
    /// <para>É a tradução com mais peso da lista: "ato interno" é jargão, e "criada por
    /// portaria" diz ao leitor que aquela caixa some sem passar por ninguém.</para>
    /// </summary>
    public static string De(NivelDeNormatizacao normatizacao) => normatizacao switch
    {
        NivelDeNormatizacao.LeiOuDecreto => "Criada por lei ou decreto",
        NivelDeNormatizacao.AtoInterno => "Criada por ato interno",
        _ => "Base legal não informada",
    };

    public static string De(EsferaDeGoverno esfera) => esfera switch
    {
        EsferaDeGoverno.Federal => "Federal",
        EsferaDeGoverno.EstadualOuDistrital => "Estadual ou distrital",
        EsferaDeGoverno.Municipal => "Municipal",
        _ => "Esfera não informada",
    };

    /// <summary>
    /// O que se diz quando a fonte não classificou.
    ///
    /// <para>Não é preenchimento: a raiz do grafo e os entes vêm mesmo sem poder e sem
    /// esfera, e escrever "Executivo" ali por padrão inventaria uma classificação que o
    /// Estado não fez sobre si mesmo.</para>
    /// </summary>
    public const string NaoClassificado = "Não classificado";
}
