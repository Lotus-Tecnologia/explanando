using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Ingestao;

/// <summary>
/// Um deputado como a fonte externa o descreve. Traz apenas o que o V1 consome —
/// o endpoint de detalhe da Camara devolve CPF completo, nascimento e redes sociais,
/// e nada disso atravessa esta fronteira (LGPD art. 6, III).
/// </summary>
public sealed record DeputadoDaFonte
{
    public required int IdExterno { get; init; }
    public required string NomeCivil { get; init; }
    public required string NomeParlamentar { get; init; }
    public required string SiglaPartido { get; init; }
    public required string Uf { get; init; }
    public string? FotoUrl { get; init; }
    public int? Legislatura { get; init; }

    /// <summary>`ultimoStatus.situacao` == "Exercício" no contrato da Camara.</summary>
    public bool EmExercicio { get; init; } = true;

    /// <summary>
    /// De que casa vem esta pessoa.
    ///
    /// <para>O padrao e' deputado para que o caminho da Camara — que nao conhece este campo —
    /// continue igual. O Senado preenche explicitamente.</para>
    ///
    /// <para><b>Nao e' decoracao.</b> A unicidade de parlamentar e' do par
    /// <c>(IdExterno, Tipo)</c>, e nao do numero: o comentario em
    /// <c>ParlamentarConfiguracao</c> ja dizia que "o id 204379 da Camara e o id 204379 do
    /// Senado seriam pessoas diferentes". Sem carregar o tipo ate aqui, a ingestao gravaria
    /// senador como deputado e a busca por id sozinho poderia devolver a pessoa errada.</para>
    /// </summary>
    public TipoParlamentar Tipo { get; init; } = TipoParlamentar.DeputadoFederal;
}

/// <summary>
/// Uma despesa da cota como a fonte a descreve. Espelha o contrato real capturado na
/// Etapa 01 (docs/contratos/camara-despesas-pagina-100.json), incluindo os tres valores
/// distintos e o numero da nota — ambos ausentes da especificacao original.
/// </summary>
public sealed record DespesaDaFonte
{
    public required int Ano { get; init; }
    public required int Mes { get; init; }
    public required string TipoDespesa { get; init; }
    public required string CodigoDocumento { get; init; }
    public string? NumeroDocumento { get; init; }

    /// <summary>`codTipoDocumento`: 0 = Nota Fiscal, 4 = NF-e, demais = recibo/bilhete.</summary>
    public int CodigoTipoDocumento { get; init; }

    public required DateTimeOffset DataDocumento { get; init; }
    public required decimal ValorDocumento { get; init; }
    public required decimal ValorLiquido { get; init; }
    /// <summary>Nulo quando a fonte nao publica a glosa — caso da CEAPS do Senado.</summary>
    public decimal? ValorGlosa { get; init; }
    public string? UrlDocumento { get; init; }
    public string? NomeFornecedor { get; init; }
    public string? CnpjCpfFornecedor { get; init; }
    public long CodigoLote { get; init; }
    public int Parcela { get; init; }
}

/// <summary>Um socio no quadro societario. O CPF vem com 6 dos 11 digitos.</summary>
public sealed record SocioDaFonte
{
    public required string NomeSocio { get; init; }
    public string? CpfMascarado { get; init; }
    public string? Qualificacao { get; init; }
    public DateOnly? DataEntradaSociedade { get; init; }
}

/// <summary>Cadastro de pessoa juridica como a fonte o descreve.</summary>
public sealed record EmpresaDaFonte
{
    public required string Cnpj { get; init; }
    public required string RazaoSocial { get; init; }
    public string? NomeFantasia { get; init; }
    public string? CnaePrincipal { get; init; }
    public IReadOnlyList<string> CnaesSecundarios { get; init; } = [];
    public string? DescricaoSituacaoCadastral { get; init; }
    public DateOnly? DataSituacaoCadastral { get; init; }
    public DateOnly? DataInicioAtividade { get; init; }
    public bool? OptantePeloMei { get; init; }
    public DateOnly? DataOpcaoPeloMei { get; init; }
    public DateOnly? DataExclusaoDoMei { get; init; }
    public string? Porte { get; init; }
    public string? Logradouro { get; init; }
    public string? Numero { get; init; }
    public string? Bairro { get; init; }
    public string? Municipio { get; init; }
    public int? CodigoMunicipioIbge { get; init; }
    public string? Uf { get; init; }
    public string? Cep { get; init; }
    public IReadOnlyList<SocioDaFonte> QuadroSocietario { get; init; } = [];
}

/// <summary>Fonte de parlamentares e de suas despesas de cota.</summary>
public interface IFonteDeParlamentares
{
    IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(CancellationToken cancelamento = default);

    Task<DeputadoDaFonte?> ObterDetalheAsync(int idExterno, CancellationToken cancelamento = default);

    /// <summary>
    /// Despesas de um deputado numa legislatura.
    ///
    /// A legislatura e' parametro obrigatorio de proposito: sem ela a API da Camara responde
    /// 200 com lista vazia em vez de erro, e a assinatura deste metodo torna impossivel
    /// esquece-la. Ver docs/registro/etapa-01-contratos.md.
    /// </summary>
    IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
        int idExterno,
        int legislatura,
        CancellationToken cancelamento = default);
}

/// <summary>Fonte de cadastro de pessoa juridica (CNPJ, CNAE, situacao, QSA).</summary>
public interface IFonteDeCadastroEmpresarial
{
    /// <summary>
    /// Nulo quando a fonte nao conhece o documento. Nao lanca: CNPJ ausente do cadastro
    /// e' rotina na base da Camara, nao excecao.
    /// </summary>
    Task<EmpresaDaFonte?> ConsultarAsync(string cnpj, CancellationToken cancelamento = default);
}
