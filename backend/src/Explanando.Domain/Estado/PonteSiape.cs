namespace Explanando.Domain.Estado;

/// <summary>
/// Liga o cadastro de pessoal do Executivo (SIAPE) ao grafo do Estado (SIORG).
///
/// <para><b>Não existe tabela oficial ligando os dois, e isso foi procurado.</b> O SIAPE
/// identifica o órgão pelo código SIAFI — <c>25201</c> é o Banco Central —, o SIORG pelo
/// <c>codigoUnidade</c> dele. O parâmetro <c>codigoSIAFI</c> é aceito e ignorado pela API do
/// SIORG, não há campo de código SIAFI em resposta nenhuma, e o serviço não publica
/// documentação de esquema.</para>
///
/// <para>Então a ponte é <b>pelo nome</b>, e o risco disso é específico: um par errado não
/// produz erro visível — produz o quadro de pessoal de um órgão exibido dentro de outro. Por
/// isso valem três regras, nesta ordem:</para>
///
/// <list type="number">
/// <item><b>Normalizar só o que é notação, nunca o que é identidade.</b> Acento, caixa,
/// pontuação, o sufixo de UF e as três variações de forma jurídica que a fonte usa para a
/// mesma casa. Nada de distância de edição, nada de "parecido o bastante".</item>
/// <item><b>Exigir unicidade dos dois lados.</b> Nome que casa com dois nós não casa com
/// nenhum.</item>
/// <item><b>O que sobra fica sem par, e aparece como sem par.</b></item>
/// </list>
///
/// <para>Medido contra a base real de 2026-07, com 354 órgãos no SIAPE e 96.658 unidades no
/// SIORG: as regras cobrem <b>225 órgãos e 88,8% dos 778.337 vínculos</b>, sem nenhum caso
/// ambíguo. As exceções abaixo levam isso a <b>~96%</b>, e o resto é ausência real.</para>
/// </summary>
public static class PonteSiape
{
    /// <summary>
    /// A tabela de acentos, escrita à mão — e <b>é obrigatório que seja</b>.
    ///
    /// <para>O caminho natural seria <c>string.Normalize(NormalizationForm.FormD)</c> e
    /// descartar as marcas de combinação. Neste projeto isso <b>não funciona e não falha</b>:
    /// o <c>Directory.Build.props</c> liga <c>InvariantGlobalization</c> (decisão da Etapa
    /// 00, que também dá as imagens <i>chiseled</i> sem ICU), e sem ICU o
    /// <c>Normalize</c> devolve a string intacta <b>sem lançar exceção</b>.</para>
    ///
    /// <para>Descoberto aqui: os testes de ponte acusaram
    /// <c>"MINISTÉRIO MEIO AMBIENTE"</c> onde se esperava <c>"MINISTERIO MEIO AMBIENTE"</c>.
    /// Se a ponte tivesse sido escrita sem teste, o efeito seria silencioso — um punhado de
    /// órgãos simplesmente não casaria, e a cobertura cairia sem ninguém saber por quê.</para>
    /// </summary>
    private const string ComAcento = "ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑáàâãäéèêëíìîïóòôõöúùûüçñ";
    private const string SemAcento = "AAAAAEEEEIIIIOOOOOUUUUCNaaaaaeeeeiiiiooooouuuucn";

    private static readonly string[] Ufs =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG",
        "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    /// <summary>
    /// Palavras que não distinguem uma casa de outra.
    ///
    /// <para>Só preposições e artigos. Tirar substantivo desta lista seria o começo do
    /// palpite: "Ministério da Educação" e "Ministério da Cultura" só se distinguem pela
    /// última palavra.</para>
    /// </summary>
    private static readonly string[] Paradas =
        ["DE", "DA", "DO", "DAS", "DOS", "E", "EM", "A", "O", "AS", "OS"];

    /// <summary>
    /// O nome legal inteiro dos institutos federais, que o SIAPE encurta.
    ///
    /// <para>"Instituto Federal de São Paulo" no SIAPE é "Instituto Federal de Educação,
    /// Ciência e Tecnologia de São Paulo" no SIORG. São 38 casas, e o trecho removido é
    /// literal — não é abreviação inferida.</para>
    /// </summary>
    private const string NomeLongoDosInstitutos = " EDUCACAO CIENCIA E TECNOLOGIA ";

    /// <summary>
    /// Rubricas de totalização do SIAPE que <b>não são órgãos</b>.
    ///
    /// <para>Medido: <c>99991</c> "Estados / Municípios / Empresas" sozinha carrega 16.689
    /// vínculos, e <c>-20</c> "Poder Executivo Federal" — com código negativo — carrega
    /// 2.061. Ligá-las a um nó do grafo poria milhares de servidores dentro de uma caixa
    /// que não os tem.</para>
    /// </summary>
    private static readonly int[] Agregados = [-20, -1, 99990, 99991, 99992];

    /// <summary>
    /// Os pares que o nome não alcança, resolvidos um a um contra a base.
    ///
    /// <para><b>Cada linha é uma verificação manual, não um palpite.</b> Elas caem em quatro
    /// famílias, e a família está no comentário porque é ela que diz se a entrada continua
    /// válida quando a fonte mudar:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Nome truncado em caixa alta</b> pelo SIAPE ("MIN GESTAO E INOV EM SERV
    /// PUBLICOS"). Some quando a fonte parar de truncar.</item>
    /// <item><b>Órgão renomeado</b>, com o SIAPE ainda no nome antigo. Some quando a fonte
    /// atualizar — ou vira erro se o órgão for de fato outro.</item>
    /// <item><b>Erro de digitação na origem</b> ("Função Escola Nacional…").</item>
    /// <item><b>Abreviação</b> ("UNIV. FEDERAL DO…").</item>
    /// </list>
    ///
    /// <para>É a mesma natureza da tabela CNAE da Etapa 10: curadoria minha, medida, e que
    /// merece uma revisão do Gustavo.</para>
    /// </summary>
    public static IReadOnlyDictionary<int, int> Excecoes { get; } = new Dictionary<int, int>
    {
        // --- nome truncado em caixa alta pelo SIAPE ---
        [17500] = 308803,  // MIN GESTAO E INOV EM SERV PUBLICOS → Ministério da Gestão e da Inovação em Serviços Públicos
        [55100] = 308796,  // MIN DESENV ASSIS SOCI FAMIL COMBATE FOME → Min. do Desenvolvimento e Assistência Social, Família e Combate à Fome
        [40100] = 308799,  // MIN DA INTEG E DO DESENV REGIONAL → Ministério da Integração e do Desenvolvimento Regional
        [17400] = 308814,  // MIN DESENVOLV IND COMERCIO E SERVICOS → Min. do Desenvolvimento, Indústria, Comércio e Serviços
        [13100] = 308794,  // MIN DO DESENV AGR E AGRIC FAMILIAR → Min. do Desenvolvimento Agrário e Agricultura Familiar
        [81100] = 308816,  // MIN DOS DIR HUM E DA CIDADANIA → Ministério dos Direitos Humanos e da Cidadania
        [17700] = 330683,  // MIN EMPREEND MICROEMP EMP PEQUENO PORTE → Min. do Empreendedorismo, da Microempresa e da Empresa de Pequeno Porte

        // --- orgao renomeado; o SIAPE ainda usa o nome anterior ---
        [40108] = 267175,  // Min. da Ciência, Tecnologia, Inovações e Comunicações → Ministério da Ciência, Tecnologia e Inovação
        [40211] = 1927,    // Ministério do Meio Ambiente → Ministério do Meio Ambiente e Mudança do Clima
        [30202] = 173,     // Fundação Nacional do Índio → Fundação Nacional dos Povos Indígenas
        [44205] = 46876,   // Agência Nacional de Águas → Agência Nacional de Águas e Saneamento Básico
        [26107] = 249,     // Inst. Nacional de Estudos e Pesquisas Educacionais → …Anísio Teixeira

        // --- erro de digitacao na origem ---
        [40202] = 956,     // "Função Escola Nacional de Administração Pública" → Fundação Escola Nacional de Administração Pública

        // --- abreviacao ---
        [26455] = 227867,  // UNIV. FEDERAL DO DELTA DO PARNAIBA → Universidade Federal do Delta do Parnaíba
        [26456] = 227868,  // UNIVER. FEDERAL DO AGRESTE DE PERNAMBUCO → Universidade Federal do Agreste de Pernambuco
        [26270] = 465,     // Fundação Universidade Federal do Amazonas → Fundação Universidade do Amazonas

        // --- "Departamento de X" no SIAPE, so' "X" no SIORG ---
        [20115] = 324,     // Departamento de Polícia Federal → Polícia Federal (17.311 vínculos)
        [30802] = 704,     // Departamento de Polícia Rodoviária Federal → Polícia Rodoviária Federal (14.026)

        // --- razao social abreviada ---
        [20415] = 86567,   // Empresa Brasil de Comunicação → Empresa Brasil de Comunicação S.A.
        [20302] = 48739,   // Nuclebrás Equipamentos Pesados → Nuclebrás Equipamentos Pesados S/A
    };

    /// <summary>
    /// A forma comparável de um nome de órgão.
    ///
    /// <para>Devolve string vazia quando não sobra nada — e vazio <b>nunca casa</b>, o que
    /// impede dois registros sem nome de virarem o mesmo órgão.</para>
    /// </summary>
    public static string Normalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return string.Empty;
        }

        var simples = new char[nome.Length];

        for (var i = 0; i < nome.Length; i++)
        {
            var letra = nome[i];
            var acento = ComAcento.IndexOf(letra, StringComparison.Ordinal);

            if (acento >= 0)
            {
                letra = SemAcento[acento];
            }

            // Tudo que nao e' letra ou digito vira separador: virgula, hifen, ponto e
            // barra aparecem nos dois cadastros e nao distinguem casa nenhuma.
            simples[i] = char.IsLetterOrDigit(letra) ? char.ToUpperInvariant(letra) : ' ';
        }

        // O espaco nas pontas nao e' cosmetico: e' o que deixa a troca do nome longo dos
        // institutos casar tambem quando ele esta' no comeco ou no fim.
        var texto = $" {string.Join(' ', new string(simples).Split(' ', StringSplitOptions.RemoveEmptyEntries))} ";

        texto = texto.Replace(NomeLongoDosInstitutos, " ", StringComparison.Ordinal);

        var palavras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        // O sufixo de UF sai do fim, e so' do fim: "Universidade Federal de Sergipe - SE"
        // e' a mesma casa que "Universidade Federal de Sergipe", mas "SP" no meio de um
        // nome pode ser parte dele.
        while (palavras.Count > 0 && Ufs.Contains(palavras[^1], StringComparer.Ordinal))
        {
            palavras.RemoveAt(palavras.Count - 1);
        }

        // A forma juridica no comeco do nome: a fonte usa "Fundacao Universidade Federal X"
        // e "Universidade Federal de X" para a mesma casa.
        if (palavras.Count > 1
            && palavras[0] == "FUNDACAO"
            && palavras[1] is "UNIVERSIDADE" or "INSTITUTO")
        {
            palavras.RemoveAt(0);
        }

        return string.Join(' ', palavras.Where(p => !Paradas.Contains(p, StringComparer.Ordinal)));
    }

    /// <summary>
    /// Se o código é uma rubrica de totalização, e não um órgão.
    ///
    /// <para>Códigos não positivos entram junto: <c>-20</c> e <c>-1</c> aparecem na fonte, e
    /// nenhum órgão de verdade tem código negativo.</para>
    /// </summary>
    public static bool EhAgregado(int codigoSiafi) =>
        codigoSiafi <= 0 || Agregados.Contains(codigoSiafi);
}
