using Explanando.Domain.Comum;

namespace Explanando.Domain.Eleicoes;

/// <summary>Para quem foi a doação.</summary>
public enum DestinoDaDoacao
{
    /// <summary>Uma campanha de candidato.</summary>
    Candidato = 1,

    /// <summary>Um órgão partidário — diretório nacional, estadual ou municipal.</summary>
    Partido = 2,
}

/// <summary>
/// Uma doação de pessoa física numa eleição, como a prestação de contas a declara.
///
/// <para><b>O CPF chega inteiro do TSE e sai daqui mascarado.</b> A máscara é a da Receita — os
/// seis dígitos do meio —, aplicada em <see cref="Criar"/>, antes de a entidade existir. O número
/// completo nunca é gravado: não serve a regra nenhuma, e a política de privacidade promete que
/// não entra.</para>
///
/// <para>Só pessoa física: doação de empresa é proibida desde 2015, e o que chega com CNPJ na
/// prestação de contas é partido, campanha ou fundo — dinheiro partidário, não "doador" no
/// sentido que este produto cruza.</para>
/// </summary>
public sealed class DoacaoEleitoral : Entidade
{
    private DoacaoEleitoral()
    {
        // EF Core.
        NomeDoDoador = string.Empty;
        NomeDoDoadorNormalizado = string.Empty;
        CpfDoDoador = CpfParcial.Criar(null);
        NomeDoDestinatario = string.Empty;
        SiglaPartidoDoDestinatario = string.Empty;
        UfDoDestinatario = string.Empty;
    }

    public int AnoDaEleicao { get; private set; }

    /// <summary>O <c>SQ_RECEITA</c> do TSE. Com o destino e o ano, identifica a doação.</summary>
    public long SequencialDaReceita { get; private set; }

    public DestinoDaDoacao Destino { get; private set; }

    /// <summary>O <c>SQ_CANDIDATO</c> do destinatário. Nulo quando a doação foi a partido.</summary>
    public long? SequencialDoCandidato { get; private set; }

    /// <summary>Preenchido quando o destinatário é um dos parlamentares da base.</summary>
    public Guid? ParlamentarId { get; private set; }

    /// <summary>O nome como a Receita o registra, quando o TSE traz; senão, o declarado.</summary>
    public string NomeDoDoador { get; private set; }

    /// <summary>A chave de casamento com o sócio. Ver <see cref="NomeNormalizado"/>.</summary>
    public string NomeDoDoadorNormalizado { get; private set; }

    public CpfParcial CpfDoDoador { get; private set; }

    /// <summary>O candidato ou o órgão partidário que recebeu.</summary>
    public string NomeDoDestinatario { get; private set; }

    public string SiglaPartidoDoDestinatario { get; private set; }

    public string UfDoDestinatario { get; private set; }

    public string? CargoDoDestinatario { get; private set; }

    public DateOnly Data { get; private set; }

    public decimal Valor { get; private set; }

    public static DoacaoEleitoral Criar(
        int anoDaEleicao,
        long sequencialDaReceita,
        DestinoDaDoacao destino,
        long? sequencialDoCandidato,
        Guid? parlamentarId,
        string nomeDoDoador,
        string cpfCompletoDoDoador,
        string nomeDoDestinatario,
        string siglaPartidoDoDestinatario,
        string ufDoDestinatario,
        string? cargoDoDestinatario,
        DateOnly data,
        decimal valor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeDoDoador);

        var cpf = CpfParcial.DeCpfCompleto(cpfCompletoDoDoador);

        if (!cpf.TemDigitos)
        {
            throw new ArgumentException(
                "Doação sem CPF de pessoa física: só pessoa física entra como doador.",
                nameof(cpfCompletoDoDoador));
        }

        return new DoacaoEleitoral
        {
            AnoDaEleicao = anoDaEleicao,
            SequencialDaReceita = sequencialDaReceita,
            Destino = destino,
            SequencialDoCandidato = sequencialDoCandidato,
            ParlamentarId = parlamentarId,
            NomeDoDoador = nomeDoDoador.Trim(),
            NomeDoDoadorNormalizado = NomeNormalizado.De(nomeDoDoador),
            CpfDoDoador = cpf,
            NomeDoDestinatario = nomeDoDestinatario.Trim(),
            SiglaPartidoDoDestinatario = siglaPartidoDoDestinatario.Trim(),
            UfDoDestinatario = ufDoDestinatario.Trim(),
            CargoDoDestinatario = string.IsNullOrWhiteSpace(cargoDoDestinatario) ? null : cargoDoDestinatario.Trim(),
            Data = data,
            Valor = valor,
        };
    }

    /// <summary>
    /// Se esta doação veio de quem tem este nome e este CPF parcial. Verdadeiro quer dizer
    /// <b>compatível</b>: nome idêntico e os seis dígitos visíveis iguais — cinco continuam
    /// ocultos dos dois lados.
    /// </summary>
    public bool VeioDe(string nomeDoSocio, CpfParcial cpfDoSocio)
    {
        ArgumentNullException.ThrowIfNull(cpfDoSocio);

        return cpfDoSocio.TemDigitos
            && cpfDoSocio.Equals(CpfDoDoador)
            && string.Equals(NomeNormalizado.De(nomeDoSocio), NomeDoDoadorNormalizado, StringComparison.Ordinal);
    }
}
