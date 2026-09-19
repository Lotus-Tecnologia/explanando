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
    /// A chave estável de um tipo de unidade, para quem precisa <b>decidir</b> e não exibir.
    ///
    /// <para>Existe porque desenho não pode depender de texto de tela. O grafo escolhe a
    /// forma do nó pelo tipo, e se essa escolha casasse com <c>"Órgão"</c> bastaria alguém
    /// melhorar a redação para todos os órgãos virarem a forma padrão — sem erro, sem
    /// teste vermelho, sem ninguém perceber.</para>
    /// </summary>
    public static string ChaveDe(TipoDeUnidade tipo) => tipo switch
    {
        TipoDeUnidade.Ente => "ente",
        TipoDeUnidade.Entidade => "entidade",
        TipoDeUnidade.Orgao => "orgao",
        TipoDeUnidade.UnidadeAdministrativa => "unidade-administrativa",
        TipoDeUnidade.UnidadeColegiada => "colegiada",
        _ => "desconhecido",
    };

    /// <summary>A chave estável de um poder. Mesma razão de <see cref="ChaveDe(TipoDeUnidade)"/>.</summary>
    public static string ChaveDe(Poder poder) => poder switch
    {
        Poder.Executivo => "executivo",
        Poder.Legislativo => "legislativo",
        Poder.Judiciario => "judiciario",
        Poder.FuncoesEssenciaisAJustica => "essenciais",
        _ => "desconhecido",
    };

    /// <summary>
    /// Em que anel do grafo a unidade aparece.
    ///
    /// <para><b>É a adaptação brasileira da ideia central do desenho de referência</b>, e
    /// não uma cópia dela. O CivLab põe no raio o nível de autoridade — <i>highest
    /// authority</i>, <i>oversight</i>, <i>administration</i>. Aqui o equivalente existe e é
    /// jurídico, não editorial: a Constituição separa <b>administração direta</b> de
    /// <b>indireta</b>, e o SIORG carrega essa distinção no tipo da unidade.</para>
    ///
    /// <para>Ente federativo é a autoridade que funda; órgão é administração direta —
    /// braço do próprio ente, sem personalidade jurídica; entidade é administração indireta
    /// — autarquia, fundação, empresa pública, com personalidade e patrimônio próprios. A
    /// diferença entre um órgão e uma entidade não é de tamanho, é de natureza, e o raio
    /// passa a dizer isso.</para>
    /// </summary>
    public static int AnelDe(TipoDeUnidade tipo) => tipo switch
    {
        TipoDeUnidade.Ente => 0,
        TipoDeUnidade.Orgao => 1,
        TipoDeUnidade.Entidade => 2,
        TipoDeUnidade.UnidadeColegiada => 3,
        _ => 4,
    };

    /// <summary>O rótulo do anel, como aparece curvado no desenho.</summary>
    public static string RotuloDoAnel(int anel) => anel switch
    {
        0 => "Entes federativos",
        1 => "Administração direta",
        2 => "Administração indireta",
        3 => "Colegiados",
        _ => "Unidades administrativas",
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
