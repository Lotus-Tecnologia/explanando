using System.Text.RegularExpressions;
using Explanando.Domain.Comum;

namespace Explanando.Domain.Auditoria;

/// <summary>Peso do achado. "Vermelha" na UI corresponde a <see cref="Alta"/>.</summary>
public enum Gravidade
{
    Baixa = 1,
    Media = 2,
    Alta = 3,
}

/// <summary>
/// Achado do motor de regras sobre uma despesa.
///
/// Sempre atribuido a uma regra nomeada (<see cref="CodigoRegra"/>) e sempre acompanhado
/// de um <see cref="GrauDeConfianca"/>. O grau existe porque nem toda regra e' certa:
/// REG_NET_001 cruza apenas 6 digitos de CPF e produz indicio, nao prova.
/// </summary>
public sealed partial class FlagAnomalia : Entidade
{
    private FlagAnomalia()
    {
        // EF Core.
        CodigoRegra = string.Empty;
        Descricao = string.Empty;
    }

    public Guid DespesaId { get; private set; }

    /// <summary>Codigo da regra que disparou, no formato <c>REG_XXX_000</c>.</summary>
    public string CodigoRegra { get; private set; }

    public Gravidade Gravidade { get; private set; }

    /// <summary>
    /// Texto exibido ao publico. Passa pelo <see cref="VocabularioDeCompliance"/>:
    /// o dominio recusa emitir descricao que impute conduta (ADR-0004).
    /// </summary>
    public string Descricao { get; private set; }

    /// <summary>Confianca no achado, em (0, 1]. Regra deterministica vale 1.</summary>
    public decimal GrauDeConfianca { get; private set; }

    public DateTimeOffset DataIdentificacao { get; private set; }

    /// <summary>
    /// Versao do motor que emitiu esta flag.
    ///
    /// E' o que permite a notificacao distinguir achado novo por <b>despesa nova</b> de
    /// achado novo por <b>regra alterada</b>. So' o primeiro e' noticia sobre o parlamentar.
    /// </summary>
    public int VersaoDoMotor { get; private set; }

    public bool EhVermelha => Gravidade is Gravidade.Alta;

    public bool EhDeterministica => GrauDeConfianca >= 1.0m;

    /// <summary>
    /// Contribuicao da flag para o score do parlamentar. Cresce com a gravidade e
    /// e' reduzida pela incerteza — um indicio a 50% nao pode pesar como um fato.
    /// </summary>
    public decimal Peso => (int)Gravidade * GrauDeConfianca;

    public static FlagAnomalia Criar(
        Guid despesaId,
        string codigoRegra,
        Gravidade gravidade,
        string descricao,
        decimal grauDeConfianca)
    {
        if (!FormatoDeCodigoDeRegra().IsMatch(codigoRegra ?? string.Empty))
        {
            throw new ArgumentException(
                $"Codigo de regra '{codigoRegra}' fora do formato REG_XXX_000.",
                nameof(codigoRegra));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);
        VocabularioDeCompliance.GarantirVocabularioTecnico(descricao, nameof(descricao));

        if (grauDeConfianca is <= 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(grauDeConfianca),
                grauDeConfianca,
                "Grau de confianca tem de estar em (0, 1]. Zero significaria emitir uma flag "
                + "em que o proprio motor nao acredita.");
        }

        return new FlagAnomalia
        {
            DespesaId = despesaId,
            CodigoRegra = codigoRegra!,
            Gravidade = gravidade,
            Descricao = descricao.Trim(),
            GrauDeConfianca = grauDeConfianca,
            DataIdentificacao = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Carimba a versao do motor. Chamado pelo motor ao coletar o resultado.</summary>
    public void RegistrarVersaoDoMotor(int versao)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(versao);
        VersaoDoMotor = versao;
    }

    [GeneratedRegex(@"^REG_[A-Z]{2,3}_\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDeCodigoDeRegra();
}
