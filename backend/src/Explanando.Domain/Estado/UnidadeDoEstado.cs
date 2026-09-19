using Explanando.Domain.Comum;

namespace Explanando.Domain.Estado;

/// <summary>
/// Os quatro poderes como o SIORG os classifica.
///
/// <para>Os valores são os códigos da fonte, não uma numeração nossa: gravar um número
/// diferente do que o Estado publica obrigaria a uma tradução em toda leitura, e é nela que
/// um mapeamento erra em silêncio.</para>
///
/// <para>"Funções Essenciais à Justiça" — Ministério Público, Defensoria, Advocacia Pública —
/// não é um quarto poder na Constituição, mas é uma quarta categoria no cadastro. O grafo
/// mostra o que a fonte diz.</para>
/// </summary>
public enum Poder
{
    Executivo = 1,
    Legislativo = 2,
    Judiciario = 3,
    FuncoesEssenciaisAJustica = 4,
}

/// <summary>
/// Federal, estadual/distrital ou municipal.
///
/// <para>Existe porque <b>o SIORG não é só federal</b>: o Governo do Distrito Federal está
/// na lista dos 335 órgãos. Guardar a esfera é o que permite ao grafo decidir
/// explicitamente o que desenha, em vez de ganhar nós de outro ente sem ninguém ter
/// escolhido isso.</para>
/// </summary>
public enum EsferaDeGoverno
{
    Federal = 1,
    EstadualOuDistrital = 2,
    Municipal = 3,
}

/// <summary>
/// A forma jurídica da entidade. São os códigos do SIORG.
///
/// <para>O <b>5 não existe</b>, e a lacuna é da fonte: a tabela de referência devolve
/// 1, 2, 3, 4, 6 e 7. Preencher o buraco com um valor inventado faria um código futuro da
/// origem colidir com ele.</para>
/// </summary>
public enum NaturezaJuridica
{
    EmpresaPublica = 1,
    FundacaoPublica = 2,
    AdministracaoDireta = 3,
    Autarquia = 4,
    SociedadeDeEconomiaMista = 6,
    OrgaoPublicoAutonomoFederal = 7,
}

/// <summary>
/// O que a unidade é dentro da estrutura. No SIORG vem como sigla de duas letras.
/// </summary>
public enum TipoDeUnidade
{
    Ente = 1,
    Entidade = 2,
    Orgao = 3,
    UnidadeAdministrativa = 4,
    UnidadeColegiada = 5,
}

/// <summary>
/// Sob que norma a unidade existe.
///
/// <para><b>É informação de peso para quem lê</b>, e não metadado: a diferença entre um
/// órgão criado por lei e um criado por portaria interna é a diferença entre algo que exige
/// o Congresso para desaparecer e algo que some com uma assinatura. Na Presidência a divisão
/// é quase meio a meio — 59 contra 63.</para>
/// </summary>
public enum NivelDeNormatizacao
{
    LeiOuDecreto = 1,
    AtoInterno = 2,
}

/// <summary>Um campo que mudou entre duas coletas, com o antes e o depois.</summary>
public sealed record MudancaDeCampo(string Campo, string? De, string? Para);

/// <summary>Dados de criação de uma unidade, vindos do SIORG.</summary>
public sealed record NovaUnidadeDoEstado
{
    public required int Codigo { get; init; }

    /// <summary>Nulo na raiz. É a aresta do grafo.</summary>
    public int? CodigoPai { get; init; }

    /// <summary>O órgão-raiz a que a unidade pertence. Nulo quando ela mesma é o órgão.</summary>
    public int? CodigoOrgao { get; init; }

    public required string Nome { get; init; }
    public string? Sigla { get; init; }

    public required TipoDeUnidade Tipo { get; init; }

    /// <summary>
    /// Nulos quando a fonte não informa — e ela deixa de informar, inclusive nos nós mais
    /// importantes do grafo.
    ///
    /// <para><b>Medido, não suposto.</b> A raiz 200000 (República Federativa do Brasil)
    /// devolve <c>codigoPoder: ""</c> e <c>codigoEsfera: ""</c>; a União (200001) devolve
    /// poder e natureza nulos. Exigir esses campos recusaria justamente a raiz da árvore, e
    /// um grafo sem raiz não é um grafo.</para>
    /// </summary>
    public Poder? Poder { get; init; }
    public EsferaDeGoverno? Esfera { get; init; }
    public NaturezaJuridica? NaturezaJuridica { get; init; }
    public NivelDeNormatizacao? Normatizacao { get; init; }
}

/// <summary>
/// Um nó do grafo do Estado: um órgão, entidade ou unidade da administração pública.
///
/// <para>É a primeira entidade de um contexto novo. Enquanto o resto do produto responde
/// "quanto se gastou", esta responde <b>"que órgãos existem, de quem cada um depende e sob
/// que norma"</b>. A fonte é o SIORG; o contrato medido está em
/// <c>docs/contratos/siorg.md</c>.</para>
///
/// <para><b>As invariantes são poucas de propósito.</b> O SIORG é cadastro oficial, não
/// formulário aberto — inventar validação sobre o que o Estado publica sobre si mesmo
/// produziria rejeição de registro legítimo, e um mapa do Estado com buracos é pior que um
/// mapa com um campo vazio. Valida-se o que quebraria o grafo: nó sem identidade, nó sem
/// rótulo, e aresta apontando para si mesma.</para>
///
/// <para><b>Por que a entidade sabe se atualizar.</b> O SIORG não tem endpoint de histórico
/// — medido: <c>operacao</c> e <c>codigoUnidadePaiAnterior</c> chegam vazios, e os
/// parâmetros de versão são aceitos e ignorados. A mudança estrutural, então, é deduzida
/// comparando coletas, e <see cref="Atualizar"/> é onde essa comparação acontece.</para>
/// </summary>
public sealed class UnidadeDoEstado : Entidade
{
    private UnidadeDoEstado()
    {
        // EF Core.
        Nome = string.Empty;
    }

    /// <summary>O código no SIORG. É a identidade do nó, e não muda.</summary>
    public int Codigo { get; private set; }

    /// <summary>Nulo na raiz.</summary>
    public int? CodigoPai { get; private set; }

    public int? CodigoOrgao { get; private set; }

    public string Nome { get; private set; }
    public string? Sigla { get; private set; }

    public TipoDeUnidade Tipo { get; private set; }

    /// <summary>Nulos na raiz e nos entes. Ver <see cref="NovaUnidadeDoEstado.Poder"/>.</summary>
    public Poder? Poder { get; private set; }
    public EsferaDeGoverno? Esfera { get; private set; }

    public NaturezaJuridica? NaturezaJuridica { get; private set; }
    public NivelDeNormatizacao? Normatizacao { get; private set; }

    /// <summary>Quando esta unidade foi vista pela primeira e pela última vez numa coleta.</summary>
    public DateTimeOffset PrimeiraColeta { get; private set; }
    public DateTimeOffset UltimaColeta { get; private set; }

    /// <summary>
    /// Se a unidade <b>ainda aparece</b> na estrutura publicada.
    ///
    /// <para>Falso não significa extinta. Some da resposta do SIORG quem foi extinto, quem
    /// foi incorporado e quem o cadastro simplesmente deixou de publicar — e a fonte não
    /// distingue os três casos. A unidade continua na base, com a última fotografia que
    /// tivemos dela: apagá-la seria perder o pedaço do mapa que explica de onde veio o que
    /// existe hoje.</para>
    /// </summary>
    public bool Ativa { get; private set; } = true;

    public bool EhRaiz => CodigoPai is null;

    /// <summary>
    /// Registra que a unidade não veio na coleta.
    ///
    /// <para>Devolve <c>true</c> só quando houve transição — chamar duas vezes seguidas não
    /// gera um segundo desaparecimento, senão toda coleta mensal repetiria a mesma notícia
    /// sobre o mesmo órgão para sempre.</para>
    /// </summary>
    public bool MarcarDesaparecida(DateTimeOffset quando)
    {
        if (!Ativa)
        {
            return false;
        }

        Ativa = false;
        UltimaColeta = quando;

        return true;
    }

    /// <summary>
    /// Registra que a unidade voltou a aparecer. Devolve <c>true</c> só na transição.
    /// </summary>
    public bool MarcarReaparecida(DateTimeOffset quando)
    {
        if (Ativa)
        {
            return false;
        }

        Ativa = true;
        UltimaColeta = quando;

        return true;
    }

    public static UnidadeDoEstado Criar(NovaUnidadeDoEstado nova, DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(nova);

        var unidade = new UnidadeDoEstado
        {
            Codigo = ExigirCodigo(nova.Codigo),
            PrimeiraColeta = quando,
            UltimaColeta = quando,
        };

        unidade.Aplicar(nova);

        return unidade;
    }

    /// <summary>
    /// Aplica uma coleta nova e devolve <b>o que mudou</b>.
    ///
    /// <para>Lista vazia é o caso comum e significa silêncio — a estrutura do Estado muda
    /// devagar. Cada item é uma mudança que vale contar a quem acompanha.</para>
    ///
    /// <para>Mudança de <c>CodigoPai</c> merece atenção especial de quem consome isto:
    /// renomear um órgão muda um rótulo, mas mudar o pai <b>move um ramo inteiro</b> — é o
    /// que uma reforma administrativa produz, muitas vezes sem alarde.</para>
    /// </summary>
    public IReadOnlyList<MudancaDeCampo> Atualizar(NovaUnidadeDoEstado nova, DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(nova);

        if (nova.Codigo != Codigo)
        {
            throw new ArgumentException(
                $"A unidade {Codigo} não pode ser atualizada com os dados da {nova.Codigo}: "
                + "o código é a identidade do nó, e trocá-lo confundiria duas unidades numa "
                + "linha só.",
                nameof(nova));
        }

        var mudancas = new List<MudancaDeCampo>();

        Comparar(mudancas, nameof(Nome), Nome, Aparar(nova.Nome, nameof(nova.Nome)));
        Comparar(mudancas, nameA: nameof(Sigla), De: Sigla, Para: Opcional(nova.Sigla));
        Comparar(mudancas, nameof(CodigoPai), Texto(CodigoPai), Texto(nova.CodigoPai));
        Comparar(mudancas, nameof(CodigoOrgao), Texto(CodigoOrgao), Texto(nova.CodigoOrgao));
        Comparar(mudancas, nameof(Poder), Poder?.ToString(), nova.Poder?.ToString());
        Comparar(mudancas, nameof(Esfera), Esfera?.ToString(), nova.Esfera?.ToString());
        Comparar(mudancas, nameof(Tipo), Tipo.ToString(), nova.Tipo.ToString());
        Comparar(mudancas, nameof(NaturezaJuridica), NaturezaJuridica?.ToString(), nova.NaturezaJuridica?.ToString());
        Comparar(mudancas, nameof(Normatizacao), Normatizacao?.ToString(), nova.Normatizacao?.ToString());

        Aplicar(nova);
        UltimaColeta = quando;

        return mudancas;
    }

    private void Aplicar(NovaUnidadeDoEstado nova)
    {
        var pai = nova.CodigoPai;

        if (pai == nova.Codigo)
        {
            throw new ArgumentException(
                $"A unidade {nova.Codigo} não pode ser pai de si mesma: é o ciclo mais curto "
                + "possível, e ele trava qualquer travessia do grafo em profundidade.",
                nameof(nova));
        }

        Nome = Aparar(nova.Nome, nameof(nova.Nome));
        Sigla = Opcional(nova.Sigla);
        CodigoPai = pai;
        CodigoOrgao = nova.CodigoOrgao;
        Poder = nova.Poder;
        Esfera = nova.Esfera;
        Tipo = nova.Tipo;
        NaturezaJuridica = nova.NaturezaJuridica;
        Normatizacao = nova.Normatizacao;
    }

    private static void Comparar(
        List<MudancaDeCampo> mudancas,
        string nameA,
        string? De,
        string? Para)
    {
        if (!string.Equals(De, Para, StringComparison.Ordinal))
        {
            mudancas.Add(new MudancaDeCampo(nameA, De, Para));
        }
    }

    private static int ExigirCodigo(int codigo) =>
        codigo > 0
            ? codigo
            : throw new ArgumentException(
                $"Código de unidade precisa ser positivo; recebido {codigo}. Sem código não "
                + "há nó, e nenhuma aresta consegue apontar para ele.",
                nameof(codigo));

    private static string Aparar(string valor, string nome) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException(
                "Unidade sem nome é um ponto no desenho que ninguém consegue ler nem "
                + "procurar.",
                nome)
            : valor.Trim();

    private static string? Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Texto(int? valor) =>
        valor?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
