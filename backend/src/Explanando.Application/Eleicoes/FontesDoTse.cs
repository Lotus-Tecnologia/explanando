using Explanando.Domain.Eleicoes;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Eleicoes;

/// <summary>Os votos de um município, dentro de uma candidatura.</summary>
public sealed record VotosDaFonte(int CodigoDoMunicipio, string NomeDoMunicipio, long Votos);

/// <summary>
/// Uma candidatura a deputado federal ou senador, com os votos já somados por município (as
/// zonas eleitorais do mesmo município viram uma linha só).
/// </summary>
public sealed record CandidatoNaVotacao
{
    public required long Sequencial { get; init; }
    public required string NomeCivil { get; init; }
    public required string NomeDeUrna { get; init; }
    public required string Uf { get; init; }

    /// <summary>6 é deputado federal, 5 é senador — a codificação do TSE.</summary>
    public required int CodigoDoCargo { get; init; }

    public required string Cargo { get; init; }
    public required string SiglaPartido { get; init; }
    public required int NumeroNaUrna { get; init; }
    public required string Situacao { get; init; }
    public IReadOnlyList<VotosDaFonte> PorMunicipio { get; init; } = [];
}

/// <summary>
/// Uma receita de campanha ou de partido, <b>já filtrada para pessoa física</b>. O documento vem
/// inteiro, como o TSE publica, e só existe até virar <see cref="DoacaoEleitoral"/>, que o
/// mascara.
/// </summary>
public sealed record ReceitaDaFonte
{
    public required long Sequencial { get; init; }
    public required DestinoDaDoacao Destino { get; init; }
    public long? SequencialDoCandidato { get; init; }
    public required string NomeDoDestinatario { get; init; }
    public required string SiglaPartidoDoDestinatario { get; init; }
    public required string UfDoDestinatario { get; init; }
    public string? CargoDoDestinatario { get; init; }
    public required string DocumentoDoDoador { get; init; }
    public required string NomeDoDoador { get; init; }
    public required DateOnly Data { get; init; }
    public required decimal Valor { get; init; }
}

/// <summary>Os arquivos anuais do TSE — baixados à mão, porque o CDN recusa cliente automatizado.</summary>
public interface IFonteDoTse
{
    /// <summary>Deputado federal e senador, primeiro turno, uma entrada por candidatura.</summary>
    IAsyncEnumerable<CandidatoNaVotacao> LerVotacaoAsync(int ano, CancellationToken cancelamento = default);

    /// <summary>Receitas de candidatos e de órgãos partidários vindas de pessoa física.</summary>
    IAsyncEnumerable<ReceitaDaFonte> LerReceitasAsync(int ano, CancellationToken cancelamento = default);
}

/// <summary>A chave de um sócio para o cruzamento: nome normalizado e os seis dígitos do CPF.</summary>
public sealed record ChaveDeSocio(string NomeNormalizado, string DigitosDoCpf);

public interface IRepositorioDoTse
{
    /// <summary>Rastreados: a importação completa o nome civil de quem não o tem.</summary>
    Task<IReadOnlyList<Parlamentar>> ObterParlamentaresAsync(CancellationToken cancelamento = default);

    Task<IReadOnlySet<ChaveDeSocio>> ObterChavesDeSociosAsync(CancellationToken cancelamento = default);

    /// <summary>Apaga o que uma importação anterior do mesmo ano gravou. A carga é do zero.</summary>
    Task LimparAnoAsync(int ano, CancellationToken cancelamento = default);

    /// <summary>
    /// Roda o apagar-e-gravar do ano numa transação só: ou a carga inteira entra, ou a
    /// anterior continua lá. Pode ser repetido pela estratégia de retentativa do banco, então
    /// o trabalho precisa ser refazível do começo.
    /// </summary>
    Task EmUmaTransacaoAsync(Func<CancellationToken, Task> trabalho, CancellationToken cancelamento = default);

    void Adicionar(CandidaturaDoParlamentar candidatura);

    void Adicionar(VotosNoMunicipio votos);

    void Adicionar(DoacaoEleitoral doacao);

    /// <summary>Grava e esvazia o rastreamento — a carga tem dezenas de milhares de linhas.</summary>
    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>O que a importação de um ano produziu.</summary>
public sealed record ResultadoDaImportacaoDoTse
{
    public int Parlamentares { get; init; }
    public int NomesCivisCompletados { get; init; }
    public int CandidaturasLigadas { get; init; }
    public int Ambiguos { get; init; }
    public IReadOnlyList<string> SemCandidatura { get; init; } = [];
    public int MunicipiosComVoto { get; init; }
    public int ReceitasDePessoaFisicaLidas { get; init; }
    public int DoacoesParaParlamentares { get; init; }
    public int DoacoesDeSocios { get; init; }
}
