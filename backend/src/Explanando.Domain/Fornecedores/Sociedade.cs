using Explanando.Domain.Comum;

namespace Explanando.Domain.Fornecedores;

/// <summary>
/// Socio ou administrador do fornecedor (QSA da Receita Federal, via BrasilAPI).
///
/// O CPF chega com apenas 6 dos 11 digitos visiveis — por isso <see cref="Cpf"/> e' um
/// <see cref="CpfParcial"/> e nao uma string. Ver ADR-0004 sobre o que isso impoe a UI
/// de REG_NET_001.
///
/// A fonte tambem entrega `faixa_etaria`, que nao alimenta regra nenhuma e por isso
/// nao e' modelada aqui.
/// </summary>
public sealed class Sociedade : Entidade
{
    private Sociedade()
    {
        // EF Core.
        NomeSocio = string.Empty;
        Cpf = CpfParcial.Criar(null);
    }

    public Guid FornecedorId { get; private set; }

    public string NomeSocio { get; private set; }

    /// <summary>CPF parcial: 6 digitos conhecidos, 5 ocultos.</summary>
    public CpfParcial Cpf { get; private set; }

    public string? Qualificacao { get; private set; }

    public DateOnly? DataEntradaSociedade { get; private set; }

    public static Sociedade Criar(
        Guid fornecedorId,
        string nomeSocio,
        string? cpfMascarado,
        string? qualificacao,
        DateOnly? dataEntrada)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeSocio);

        return new Sociedade
        {
            FornecedorId = fornecedorId,
            NomeSocio = nomeSocio.Trim().ToUpperInvariant(),
            Cpf = CpfParcial.Criar(cpfMascarado),
            Qualificacao = qualificacao?.Trim(),
            DataEntradaSociedade = dataEntrada,
        };
    }
}
