using Explanando.Domain.Comum;

namespace Explanando.Domain.Parlamentares;

/// <summary>Casa legislativa do parlamentar. O Senado esta fora do V1 (ADR-0002).</summary>
public enum TipoParlamentar
{
    DeputadoFederal = 1,
    Senador = 2,
}

/// <summary>Dados de criacao de um parlamentar, vindos da API da Camara.</summary>
public sealed record NovoParlamentar
{
    public required int IdExterno { get; init; }
    public required string NomeCivil { get; init; }
    public required string NomeParlamentar { get; init; }
    public required string SiglaPartido { get; init; }
    public required string Uf { get; init; }
    public required TipoParlamentar Tipo { get; init; }
    public string? FotoUrl { get; init; }
}

/// <summary>
/// Deputado federal ou senador.
///
/// O que a API entrega e este modelo <b>deliberadamente ignora</b>: CPF completo, data de
/// nascimento, municipio de nascimento e redes sociais. Nenhum deles alimenta regra do V1,
/// e guardar dado pessoal sem finalidade contraria o principio da necessidade da LGPD
/// (art. 6, III). Decisao registrada em docs/registro/etapa-01-contratos.md.
/// </summary>
public sealed class Parlamentar : Entidade
{
    private readonly List<Mandato> _mandatos = [];

    private Parlamentar()
    {
        // EF Core.
        NomeCivil = string.Empty;
        NomeParlamentar = string.Empty;
        SiglaPartido = string.Empty;
        Uf = string.Empty;
    }

    /// <summary>Id do parlamentar na API de origem. Unico apenas dentro da mesma casa.</summary>
    public int IdExterno { get; private set; }

    public string NomeCivil { get; private set; }

    /// <summary>Nome pelo qual e' conhecido publicamente — o que vai para a UI.</summary>
    public string NomeParlamentar { get; private set; }

    public string SiglaPartido { get; private set; }

    /// <summary>UF pela qual foi eleito. Base de comparacao de REG_GEO_002.</summary>
    public string Uf { get; private set; }

    public TipoParlamentar Tipo { get; private set; }

    public string? FotoUrl { get; private set; }

    /// <summary>
    /// Número da carteira parlamentar, usado no caminho dos documentos publicados.
    ///
    /// <para><b>Não vem de nenhum endpoint da API.</b> Ele só existe dentro da URL do
    /// documento — verificado contra quatro rotas. Por isso é extraído na ingestão, da
    /// primeira nota que traga link, e guardado aqui: sem esse passo, descartar a coluna de
    /// URL perderia a capacidade de linkar documento para sempre.</para>
    ///
    /// <para>É constante por deputado — zero divergências em 10.676 URLs de 20 deputados.
    /// Nulo enquanto nenhuma despesa com link tiver sido ingerida.</para>
    /// </summary>
    public int? NumeroCarteira { get; private set; }

    public IReadOnlyCollection<Mandato> Mandatos => _mandatos;

    public static Parlamentar Criar(NovoParlamentar dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(dados.IdExterno, 0);
        ArgumentException.ThrowIfNullOrWhiteSpace(dados.NomeParlamentar);

        return new Parlamentar
        {
            IdExterno = dados.IdExterno,
            NomeCivil = dados.NomeCivil.Trim(),
            NomeParlamentar = dados.NomeParlamentar.Trim(),
            SiglaPartido = dados.SiglaPartido.Trim().ToUpperInvariant(),
            Uf = NormalizarUf(dados.Uf),
            Tipo = dados.Tipo,
            FotoUrl = dados.FotoUrl,
        };
    }

    /// <summary>
    /// Registra a carteira observada numa URL de documento. Idempotente: a primeira
    /// observação vale, porque o número é constante e reescrever a cada nota seria escrita
    /// inútil em toda ingestão.
    /// </summary>
    public void RegistrarNumeroDeCarteira(int numeroCarteira)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(numeroCarteira);

        NumeroCarteira ??= numeroCarteira;
    }

    /// <summary>
    /// Troca o nome civil pelo que o detalhe da casa publica.
    ///
    /// <para>A lista de deputados da Camara traz so' o nome parlamentar, e a ingestao o
    /// gravava nos dois campos. O civil de verdade esta' no detalhe de cada um — e e' a unica
    /// chave que liga o parlamentar a candidatura no TSE, onde o nome de urna costuma ser
    /// outro ("GLEISI", "FRAGA", "CAPITAO DERRITE").</para>
    /// </summary>
    public void CompletarNomeCivil(string nomeCivil)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeCivil);

        NomeCivil = nomeCivil.Trim();
    }

    /// <summary>
    /// Idempotente: a ingestao roda repetidas vezes sobre os mesmos dados e nao pode
    /// acumular mandatos duplicados.
    /// </summary>
    public Mandato AdicionarMandato(int legislatura, DateOnly inicio, DateOnly? fim)
    {
        var existente = _mandatos.Find(mandato => mandato.Legislatura == legislatura);

        if (existente is not null)
        {
            existente.Atualizar(inicio, fim);
            return existente;
        }

        var novo = Mandato.Criar(Id, legislatura, inicio, fim);
        _mandatos.Add(novo);
        return novo;
    }

    private static string NormalizarUf(string uf)
    {
        var normalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizada.Length != 2)
        {
            throw new ArgumentException(
                $"UF deve ter exatamente 2 letras; recebido '{uf}'.", nameof(uf));
        }

        return normalizada;
    }
}
