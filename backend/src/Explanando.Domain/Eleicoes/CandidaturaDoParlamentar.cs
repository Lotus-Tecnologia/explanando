using Explanando.Domain.Comum;

namespace Explanando.Domain.Eleicoes;

/// <summary>
/// A candidatura de um parlamentar numa eleição, como o TSE a registra.
///
/// <para>É a ponte entre as duas bases: o TSE identifica o candidato pelo sequencial dele
/// (<c>SQ_CANDIDATO</c>), e é por esse número que a prestação de contas e a votação se ligam ao
/// parlamentar. A ligação é feita pelo nome civil com a UF e o cargo, normalizados e comparados
/// por igualdade — nunca por semelhança.</para>
/// </summary>
public sealed class CandidaturaDoParlamentar : Entidade
{
    private CandidaturaDoParlamentar()
    {
        // EF Core.
        NomeDeUrna = string.Empty;
        Cargo = string.Empty;
        Uf = string.Empty;
        SiglaPartido = string.Empty;
        Situacao = string.Empty;
    }

    public Guid ParlamentarId { get; private set; }

    public int AnoDaEleicao { get; private set; }

    /// <summary>O <c>SQ_CANDIDATO</c> do TSE, único por candidatura.</summary>
    public long SequencialNoTse { get; private set; }

    public int NumeroNaUrna { get; private set; }

    public string NomeDeUrna { get; private set; }

    public string Cargo { get; private set; }

    public string Uf { get; private set; }

    /// <summary>O partido na eleição — não necessariamente o de hoje.</summary>
    public string SiglaPartido { get; private set; }

    /// <summary>"ELEITO POR QP", "SUPLENTE" — como o TSE totalizou.</summary>
    public string Situacao { get; private set; }

    public long TotalDeVotos { get; private set; }

    public static CandidaturaDoParlamentar Criar(
        Guid parlamentarId,
        int anoDaEleicao,
        long sequencialNoTse,
        int numeroNaUrna,
        string nomeDeUrna,
        string cargo,
        string uf,
        string siglaPartido,
        string situacao,
        long totalDeVotos)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequencialNoTse);
        ArgumentOutOfRangeException.ThrowIfNegative(totalDeVotos);

        return new CandidaturaDoParlamentar
        {
            ParlamentarId = parlamentarId,
            AnoDaEleicao = anoDaEleicao,
            SequencialNoTse = sequencialNoTse,
            NumeroNaUrna = numeroNaUrna,
            NomeDeUrna = nomeDeUrna.Trim(),
            Cargo = cargo.Trim(),
            Uf = uf.Trim(),
            SiglaPartido = siglaPartido.Trim(),
            Situacao = situacao.Trim(),
            TotalDeVotos = totalDeVotos,
        };
    }
}

/// <summary>Os votos de uma candidatura num município, no primeiro turno.</summary>
public sealed class VotosNoMunicipio : Entidade
{
    private VotosNoMunicipio()
    {
        // EF Core.
        NomeDoMunicipio = string.Empty;
        Uf = string.Empty;
    }

    public Guid CandidaturaId { get; private set; }

    public Guid ParlamentarId { get; private set; }

    /// <summary>
    /// O código do TSE, que <b>não</b> é o do IBGE. Por isso o nome vem junto: é ele que casa
    /// com o endereço do fornecedor.
    /// </summary>
    public int CodigoDoMunicipioNoTse { get; private set; }

    public string NomeDoMunicipio { get; private set; }

    public string Uf { get; private set; }

    public long Votos { get; private set; }

    public static VotosNoMunicipio Criar(
        CandidaturaDoParlamentar candidatura,
        int codigoDoMunicipioNoTse,
        string nomeDoMunicipio,
        string uf,
        long votos)
    {
        ArgumentNullException.ThrowIfNull(candidatura);
        ArgumentOutOfRangeException.ThrowIfNegative(votos);

        return new VotosNoMunicipio
        {
            CandidaturaId = candidatura.Id,
            ParlamentarId = candidatura.ParlamentarId,
            CodigoDoMunicipioNoTse = codigoDoMunicipioNoTse,
            NomeDoMunicipio = nomeDoMunicipio.Trim(),
            Uf = uf.Trim(),
            Votos = votos,
        };
    }
}
