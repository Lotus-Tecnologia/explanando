using Explanando.Domain.Comum;

namespace Explanando.Domain.Fornecedores;

/// <summary>
/// Situacao cadastral do CNPJ na Receita Federal, como a BrasilAPI a publica em
/// <c>descricao_situacao_cadastral</c>.
/// </summary>
public enum SituacaoCadastral
{
    Desconhecida = 0,
    Nula = 1,
    Ativa = 2,
    Suspensa = 3,
    Inapta = 4,
    Baixada = 8,
}

/// <summary>Endereco estruturado do fornecedor. Base de REG_GEO_002 e de REG_NET_003.</summary>
public sealed record EnderecoFornecedor
{
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Bairro { get; init; }
    public string? Municipio { get; init; }
    public int? CodigoMunicipioIbge { get; init; }
    public string? Uf { get; init; }
    public string? Cep { get; init; }
}

/// <summary>Dados de criacao de um fornecedor, vindos da BrasilAPI.</summary>
public sealed record NovoFornecedor
{
    public required CpfCnpj Documento { get; init; }
    public required string RazaoSocial { get; init; }
    public string? NomeFantasia { get; init; }
    public string? CnaePrincipal { get; init; }
    public IReadOnlyList<string> CnaesSecundarios { get; init; } = [];
    public SituacaoCadastral SituacaoCadastral { get; init; } = SituacaoCadastral.Desconhecida;
    public DateOnly? DataSituacaoCadastral { get; init; }
    public DateOnly? DataInicioAtividade { get; init; }
    public bool? OptantePeloMei { get; init; }
    public string? Porte { get; init; }
    public EnderecoFornecedor Endereco { get; init; } = new();
}

/// <summary>
/// Empresa (ou pessoa fisica) que emitiu a nota paga pela cota parlamentar.
///
/// Os metodos de consulta aqui sao as perguntas que o motor de regras faz. Elas vivem no
/// dominio, sem banco nem HTTP, para que a Etapa 04 apenas as encadeie — e para que cada
/// uma seja testavel isoladamente.
/// </summary>
public sealed class Fornecedor : Entidade
{
    private readonly List<string> _cnaesSecundarios = [];
    private readonly List<Sociedade> _quadroSocietario = [];

    private Fornecedor()
    {
        // EF Core.
        Documento = CpfCnpj.Criar(null);
        RazaoSocial = string.Empty;
        Endereco = new EnderecoFornecedor();
    }

    public CpfCnpj Documento { get; private set; }

    public string RazaoSocial { get; private set; }

    public string? NomeFantasia { get; private set; }

    public string? CnaePrincipal { get; private set; }

    public IReadOnlyList<string> CnaesSecundarios => _cnaesSecundarios;

    public SituacaoCadastral SituacaoCadastral { get; private set; }

    public DateOnly? DataSituacaoCadastral { get; private set; }

    public DateOnly? DataInicioAtividade { get; private set; }

    /// <summary>
    /// Nulo quando a fonte nao informou. No CNPJ real da amostra veio nulo — tratar
    /// isso como "nao e' MEI" faria REG_KYB_004 silenciar sem ninguem perceber.
    /// </summary>
    public bool? OptantePeloMei { get; private set; }

    public string? Porte { get; private set; }

    public EnderecoFornecedor Endereco { get; private set; }

    public IReadOnlyCollection<Sociedade> QuadroSocietario => _quadroSocietario;

    public bool EhMei => OptantePeloMei == true;

    public bool OpcaoPeloMeiConhecida => OptantePeloMei.HasValue;

    public static Fornecedor Criar(NovoFornecedor dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (!dados.Documento.PodeSerEnriquecido)
        {
            throw new ArgumentException(
                $"Documento '{dados.Documento.Numero}' nao identifica uma pessoa juridica ou "
                + "fisica valida; nao ha fornecedor a registrar.",
                nameof(dados));
        }

        var fornecedor = new Fornecedor
        {
            Documento = dados.Documento,
            RazaoSocial = dados.RazaoSocial.Trim(),
            NomeFantasia = dados.NomeFantasia?.Trim(),
            CnaePrincipal = NormalizarCnae(dados.CnaePrincipal),
            SituacaoCadastral = dados.SituacaoCadastral,
            DataSituacaoCadastral = dados.DataSituacaoCadastral,
            DataInicioAtividade = dados.DataInicioAtividade,
            OptantePeloMei = dados.OptantePeloMei,
            Porte = dados.Porte?.Trim(),
            Endereco = NormalizarEndereco(dados.Endereco),
        };

        foreach (var cnae in dados.CnaesSecundarios)
        {
            var normalizado = NormalizarCnae(cnae);

            if (normalizado is not null)
            {
                fornecedor._cnaesSecundarios.Add(normalizado);
            }
        }

        return fornecedor;
    }

    /// <summary>
    /// REG_KYB_001. Considera principal <b>e</b> secundarios: ignorar os secundarios
    /// geraria falso positivo — o posto da amostra vende combustivel pelo principal,
    /// mas tambem tem "bares" e "distribuicao de agua" como secundarios.
    /// </summary>
    public bool PossuiCnae(string? cnae)
    {
        var procurado = NormalizarCnae(cnae);

        if (procurado is null)
        {
            return false;
        }

        return string.Equals(CnaePrincipal, procurado, StringComparison.Ordinal)
            || _cnaesSecundarios.Contains(procurado, StringComparer.Ordinal);
    }

    /// <summary>
    /// REG_KYB_002. Verdadeiro somente se a nota e' <b>posterior</b> ao evento cadastral:
    /// uma empresa que fechou depois emitiu notas legitimas antes disso, e sinaliza-las
    /// seria acusar retroativamente (ADR-0004).
    /// </summary>
    public bool EstavaIrregularEm(DateTimeOffset dataDoDocumento)
    {
        if (SituacaoCadastral is SituacaoCadastral.Ativa or SituacaoCadastral.Desconhecida)
        {
            return false;
        }

        if (DataSituacaoCadastral is not { } dataDoEvento)
        {
            return false;
        }

        return DateOnly.FromDateTime(dataDoDocumento.UtcDateTime) > dataDoEvento;
    }

    /// <summary>
    /// REG_KYB_003. Nulo quando a fonte nao informou a abertura — a regra tem de se
    /// abster, nao chutar.
    /// </summary>
    public int? DiasDeAtividadeAte(DateTimeOffset dataDoDocumento)
    {
        if (DataInicioAtividade is not { } abertura)
        {
            return null;
        }

        return DateOnly.FromDateTime(dataDoDocumento.UtcDateTime).DayNumber - abertura.DayNumber;
    }

    internal void AdicionarSocio(Sociedade socio) => _quadroSocietario.Add(socio);

    /// <summary>
    /// Acrescenta um sócio ao quadro desta empresa, como o QSA o publica. O CPF chega da fonte
    /// já mascarado — ver <see cref="Comum.CpfParcial"/>.
    /// </summary>
    public Sociedade RegistrarSocio(string nome, string? cpfMascarado, string? qualificacao = null, DateOnly? entrada = null)
    {
        var socio = Sociedade.Criar(Id, nome, cpfMascarado, qualificacao, entrada);
        _quadroSocietario.Add(socio);
        return socio;
    }

    private static EnderecoFornecedor NormalizarEndereco(EnderecoFornecedor endereco) =>
        endereco with
        {
            Uf = endereco.Uf?.Trim().ToUpperInvariant(),
            Municipio = endereco.Municipio?.Trim().ToUpperInvariant(),
        };

    /// <summary>CNAE aparece com e sem pontuacao conforme a fonte: "4731800" e "47.31-8-00".</summary>
    private static string? NormalizarCnae(string? cnae)
    {
        if (string.IsNullOrWhiteSpace(cnae))
        {
            return null;
        }

        var destino = new char[cnae.Length];
        var tamanho = 0;

        foreach (var caractere in cnae)
        {
            if (caractere is >= '0' and <= '9')
            {
                destino[tamanho++] = caractere;
            }
        }

        return tamanho == 0 ? null : new string(destino, 0, tamanho);
    }
}
