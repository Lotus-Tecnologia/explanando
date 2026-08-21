using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;

namespace Explanando.Domain.Regras;

/// <summary>
/// Tudo que o motor precisa para avaliar um parlamentar, ja carregado em memoria.
///
/// O motor nao consulta banco nem rede: quem monta o contexto e' a camada de aplicacao.
/// Isso mantem as regras testaveis com dado literal e garante que uma regra nova nao
/// introduza consulta N+1 sem ninguem perceber.
///
/// O escopo e' <b>um parlamentar</b>, porque e' assim que as regras raciocinam: fracionamento,
/// pico e padrao mensal so' fazem sentido contra o historico da mesma pessoa. Regras que
/// cruzam parlamentares — como REG_NET_004 — precisam de outro tipo de passada.
/// </summary>
public sealed class ContextoDeAvaliacao
{
    private readonly Dictionary<string, Fornecedor> _fornecedoresPorDocumento;
    private readonly List<LimiteCota> _limites;

    public ContextoDeAvaliacao(
        Parlamentar parlamentar,
        IReadOnlyList<Despesa> despesas,
        IEnumerable<Fornecedor> fornecedores,
        IEnumerable<LimiteCota> limites,
        ParametrosDoMotor? parametros = null)
    {
        ArgumentNullException.ThrowIfNull(parlamentar);
        ArgumentNullException.ThrowIfNull(despesas);
        ArgumentNullException.ThrowIfNull(fornecedores);
        ArgumentNullException.ThrowIfNull(limites);

        Parlamentar = parlamentar;
        Despesas = despesas;
        Parametros = parametros ?? ParametrosDoMotor.Padrao;

        _fornecedoresPorDocumento = fornecedores.ToDictionary(
            fornecedor => fornecedor.Documento.Numero,
            StringComparer.Ordinal);

        _limites = [.. limites];
    }

    public Parlamentar Parlamentar { get; }

    public IReadOnlyList<Despesa> Despesas { get; }

    public ParametrosDoMotor Parametros { get; }

    /// <summary>
    /// O fornecedor da despesa, ou nulo quando nao ha um — caso do CNPJ sentinela, do
    /// campo vazio da passagem aerea, ou de CNPJ que a fonte de cadastro nao conhece.
    /// Regra que precisa de fornecedor deve se abster quando isto vier nulo (ADR-0007).
    /// </summary>
    public Fornecedor? FornecedorDe(Despesa despesa)
    {
        ArgumentNullException.ThrowIfNull(despesa);

        return _fornecedoresPorDocumento.TryGetValue(despesa.DocumentoFornecedor.Numero, out var f)
            ? f
            : null;
    }

    /// <summary>
    /// Teto da cota vigente na data, para a UF do parlamentar. Nulo quando o periodo nao
    /// esta coberto pela tabela — despesas de 2023 a 2025 ainda nao estao.
    /// </summary>
    public LimiteCota? LimiteVigenteEm(DateOnly data) =>
        _limites.Find(limite =>
            // A casa entra na busca porque CEAP e CEAPS sao cotas distintas com valores
            // proprios. Sem este filtro, o gasto de um senador seria comparado ao teto de
            // um deputado da mesma UF — e REG_TX_002 falaria sobre o numero errado.
            limite.Casa == Parlamentar.Tipo
            && string.Equals(limite.Uf, Parlamentar.Uf, StringComparison.Ordinal)
            && limite.VigenteEm(data));

    /// <summary>Despesas agrupadas por competencia, em ordem cronologica.</summary>
    public IEnumerable<IGrouping<Comum.Competencia, Despesa>> PorCompetencia() =>
        Despesas.GroupBy(despesa => despesa.Competencia).OrderBy(grupo => grupo.Key);
}

/// <summary>
/// Uma regra determinística do motor. Cada <c>REG_*</c> e' uma implementacao.
///
/// Devolve <see cref="IEnumerable{T}"/> em vez de uma flag unica porque as regras tem
/// naturezas diferentes: umas olham uma despesa por vez, outras precisam do mes inteiro
/// ou da serie historica. A assinatura acomoda as duas sem forcar nenhuma.
/// </summary>
public interface IRegraAnomalia
{
    /// <summary>Codigo no formato <c>REG_XXX_000</c>.</summary>
    string Codigo { get; }

    /// <summary>O que a regra procura, em uma frase, para documentacao e telemetria.</summary>
    string Descricao { get; }

    /// <summary>
    /// O dado externo sem o qual esta regra nao consegue opinar sobre uma despesa.
    ///
    /// <para>Declarado, e nao inferido do corpo de <see cref="Avaliar"/>, porque e' o que
    /// permite ao motor distinguir "examinei e nao achei" de "nao tive como examinar" sem
    /// que cada regra precise instrumentar o proprio laco. Regra que se vira so' com a
    /// despesa declara <see cref="DadoExigido.Nenhum"/>.</para>
    ///
    /// <para>E' obrigatorio de proposito: uma regra nova que nao pensasse nisso entraria
    /// no relatorio de cobertura afirmando 100%, e um silencio por falta de dado passaria
    /// por aprovacao.</para>
    /// </summary>
    DadoExigido Exige { get; }

    IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto);
}
