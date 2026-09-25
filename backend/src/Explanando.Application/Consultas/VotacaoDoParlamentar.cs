using Explanando.Domain.Comum;

namespace Explanando.Application.Consultas;

/// <summary>Um município na votação, com a parte dos votos do parlamentar que veio dele.</summary>
public sealed record MunicipioDaVotacao
{
    public required string Nome { get; init; }
    public required string Uf { get; init; }
    public long Votos { get; init; }

    /// <summary>De 0 a 1, sobre o total de votos do parlamentar.</summary>
    public decimal Fatia { get; init; }
}

/// <summary>
/// De onde vieram os votos, e quanto da cota foi para fornecedores sediados ali.
///
/// <para><b>A "base eleitoral" aqui é aritmética, não política:</b> os municípios que, somados
/// do maior para o menor, dão metade dos votos. É uma definição que qualquer um refaz com o
/// arquivo do TSE, e não uma leitura nossa de onde fica o reduto de alguém.</para>
///
/// <para>O endereço do fornecedor é o da sede na Receita, e não onde o serviço foi prestado. O
/// cruzamento diz onde as empresas pagas estão registradas — só isso.</para>
/// </summary>
public sealed record VotacaoDoParlamentar
{
    /// <summary>Falso quando o parlamentar não tem candidatura ligada na base do TSE.</summary>
    public bool TemCandidatura { get; init; }

    public int? Ano { get; init; }
    public string? NomeDeUrna { get; init; }
    public string? Cargo { get; init; }
    public string? SiglaPartido { get; init; }
    public string? Situacao { get; init; }
    public long TotalDeVotos { get; init; }
    public int MunicipiosComVoto { get; init; }

    /// <summary>Os que mais deram voto, até dez.</summary>
    public IReadOnlyList<MunicipioDaVotacao> Principais { get; init; } = [];

    /// <summary>Quantos municípios somam metade dos votos — a base eleitoral, na conta acima.</summary>
    public int MunicipiosDaMetadeDosVotos { get; init; }

    /// <summary>Pago pela cota a fornecedores sediados nos municípios da metade dos votos.</summary>
    public decimal PagoAFornecedoresDaBase { get; init; }

    /// <summary>Pago a fornecedores com endereço conhecido — o denominador da comparação.</summary>
    public decimal PagoAFornecedoresComEndereco { get; init; }
}

/// <summary>Monta a votação a partir dos votos por município e dos pagamentos por sede.</summary>
public static class MontagemDaVotacao
{
    public const int Principais = 10;

    public static VotacaoDoParlamentar Montar(
        (int Ano, string NomeDeUrna, string Cargo, string SiglaPartido, string Situacao, long Total) candidatura,
        IReadOnlyList<(string Municipio, string Uf, long Votos)> votos,
        IReadOnlyList<(string? Municipio, string? Uf, decimal Valor)> pagamentosPorSede)
    {
        ArgumentNullException.ThrowIfNull(votos);
        ArgumentNullException.ThrowIfNull(pagamentosPorSede);

        var total = votos.Sum(v => v.Votos);
        var ordenados = votos.Where(v => v.Votos > 0).OrderByDescending(v => v.Votos).ToList();

        var metade = new List<(string Municipio, string Uf, long Votos)>();
        long acumulado = 0;

        foreach (var municipio in ordenados)
        {
            if (total > 0 && acumulado * 2 >= total)
            {
                break;
            }

            metade.Add(municipio);
            acumulado += municipio.Votos;
        }

        var chavesDaBase = metade
            .Select(m => (NomeNormalizado.De(m.Municipio), m.Uf))
            .ToHashSet();

        var comEndereco = pagamentosPorSede
            .Where(p => !string.IsNullOrWhiteSpace(p.Municipio) && !string.IsNullOrWhiteSpace(p.Uf))
            .ToList();

        return new VotacaoDoParlamentar
        {
            TemCandidatura = true,
            Ano = candidatura.Ano,
            NomeDeUrna = candidatura.NomeDeUrna,
            Cargo = candidatura.Cargo,
            SiglaPartido = candidatura.SiglaPartido,
            Situacao = candidatura.Situacao,
            TotalDeVotos = total,
            MunicipiosComVoto = ordenados.Count,
            Principais = [.. ordenados.Take(Principais).Select(m => new MunicipioDaVotacao
            {
                Nome = m.Municipio,
                Uf = m.Uf,
                Votos = m.Votos,
                Fatia = total == 0 ? 0m : (decimal)m.Votos / total,
            })],
            MunicipiosDaMetadeDosVotos = metade.Count,
            PagoAFornecedoresDaBase = comEndereco
                .Where(p => chavesDaBase.Contains((NomeNormalizado.De(p.Municipio), p.Uf!.Trim().ToUpperInvariant())))
                .Sum(p => p.Valor),
            PagoAFornecedoresComEndereco = comEndereco.Sum(p => p.Valor),
        };
    }
}
