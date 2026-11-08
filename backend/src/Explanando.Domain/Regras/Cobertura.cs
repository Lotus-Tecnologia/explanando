using Explanando.Domain.Comum;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;

using Explanando.Domain.Parlamentares;

namespace Explanando.Domain.Regras;

/// <summary>
/// O dado externo de que uma regra depende para conseguir opinar sobre uma despesa.
///
/// <para>Existe porque "a regra não emitiu flag" tem dois significados incompatíveis:
/// <i>examinei e está tudo certo</i> e <i>não tive como examinar</i>. Apresentar o segundo
/// como se fosse o primeiro é a forma mais barata de o produto afirmar mais do que sabe —
/// exatamente o que o <c>ADR-0004</c> existe para impedir.</para>
///
/// <para>Não é hipótese. Na calibragem contra 28.540 despesas reais, duas regras ficaram
/// mudas o tempo todo: 65% dos fornecedores não têm a opção pelo MEI informada, e só 17%
/// das despesas caem num período com teto de cota configurado. Nas duas, silêncio foi
/// ausência de dado, não ausência de achado.</para>
/// </summary>
[Flags]
public enum DadoExigido
{
    /// <summary>A regra se vira só com a despesa. Cobertura sempre total.</summary>
    Nenhum = 0,

    /// <summary>O CNPJ resolveu para um fornecedor conhecido.</summary>
    CadastroDoFornecedor = 1,

    /// <summary>O fornecedor tem ao menos um CNAE legível.</summary>
    CnaeDoFornecedor = 2,

    /// <summary>A situação cadastral do fornecedor não é desconhecida.</summary>
    SituacaoDoFornecedor = 4,

    /// <summary>A data de início de atividade do fornecedor é conhecida.</summary>
    AberturaDoFornecedor = 8,

    /// <summary>A opção pelo MEI é conhecida — nula em 65% da base real.</summary>
    OpcaoPeloMei = 16,

    /// <summary>O fornecedor tem UF no endereço.</summary>
    EnderecoDoFornecedor = 32,

    /// <summary>Há teto de cota vigente para a UF do parlamentar naquela data.</summary>
    TetoDaCota = 64,

    /// <summary>O tipo de despesa está na tabela curada de CNAE.</summary>
    TipoNaTabelaCurada = 128,

    /// <summary>O fornecedor tem quadro societário com CPF (mascarado) para cruzar.</summary>
    QuadroSocietarioDoFornecedor = 256,

    /// <summary>O parlamentar tem campanha ligada na base do TSE.</summary>
    CampanhaDoParlamentar = 512,

    /// <summary>
    /// A despesa traz o documento do fornecedor. Não traz em 184.280 passagens aéreas da
    /// Câmara (medido em 2026-09-25), e sem ele não há como agrupar por fornecedor.
    /// </summary>
    DocumentoDoFornecedor = 1024,

    /// <summary>
    /// O tipo de despesa diz o que foi comprado. Não diz na rubrica do Senado que junta
    /// locomoção, hospedagem, alimentação e combustível: ali não se sabe se foi hotel ou posto.
    /// </summary>
    NaturezaDaDespesa = 2048,

    /// <summary>
    /// O valor traz os centavos. A Câmara publica com seis dígitos significativos: de
    /// R$ 10 mil para cima o centavo não existe na origem (levantamento de 08/11/2026).
    /// </summary>
    CentavosDoValor = 4096,
}

/// <summary>O que aconteceu com uma regra numa passada.</summary>
public enum SituacaoDaRegra
{
    /// <summary>Examinou e encontrou. </summary>
    Emitiu = 1,

    /// <summary>Examinou tudo que precisava e não encontrou nada. Silêncio informativo.</summary>
    SemAchados = 2,

    /// <summary>
    /// Examinou parte das despesas; nas outras faltou dado. O resultado vale para a parte
    /// examinada e não diz nada sobre o resto.
    /// </summary>
    CoberturaParcial = 3,

    /// <summary>Não conseguiu examinar despesa nenhuma. Silêncio sem informação.</summary>
    SemDado = 4,

    /// <summary>Estourou. O resultado da passada está incompleto neste código.</summary>
    Falhou = 5,
}

/// <summary>O que uma regra conseguiu fazer, e sobre quanto.</summary>
public sealed record CoberturaDaRegra
{
    public required string Codigo { get; init; }

    public required SituacaoDaRegra Situacao { get; init; }

    /// <summary>Despesas em que a regra tinha todo o dado de que precisa.</summary>
    public int DespesasComDado { get; init; }

    /// <summary>Despesas que a regra teve de pular por falta de dado.</summary>
    public int DespesasSemDado { get; init; }

    public int FlagsEmitidas { get; init; }

    /// <summary>Qual dado faltou, quando faltou. Nulo quando a cobertura foi total.</summary>
    public DadoExigido DadoAusente { get; init; }

    /// <summary>Motivo da falha, quando <see cref="Situacao"/> é <see cref="SituacaoDaRegra.Falhou"/>.</summary>
    public string? MotivoDaFalha { get; init; }

    public int DespesasVistas => DespesasComDado + DespesasSemDado;

    /// <summary>Fração das despesas sobre a qual esta regra tem algo a dizer, em [0, 1].</summary>
    public decimal Cobertura => DespesasVistas == 0
        ? 0m
        : (decimal)DespesasComDado / DespesasVistas;

    /// <summary>
    /// Se o silêncio desta regra pode ser lido como "nada encontrado". Falso quando a regra
    /// não examinou tudo — a UI precisa dizer "não avaliado", não "sem indicadores".
    /// </summary>
    public bool SilencioEhConclusivo =>
        Situacao is SituacaoDaRegra.SemAchados or SituacaoDaRegra.Emitiu;
}

/// <summary>
/// O que a passada do motor cobriu, para ser publicado junto do resultado.
///
/// <para>É a peça que permite a página dizer "11 regras aplicadas, 2 sem dado suficiente"
/// em vez de deixar o leitor supor que o silêncio das 2 significa aprovação. Também é o que
/// um gabinete precisa para contestar com precisão, e o que um comprador B2B precisa para
/// saber o que o índice não mediu.</para>
/// </summary>
public sealed record MetadadosDeCobertura
{
    public IReadOnlyList<CoberturaDaRegra> PorRegra { get; init; } = [];

    public int DespesasAvaliadas { get; init; }

    /// <summary>Primeira e última competência presentes. Nulas quando não há despesa.</summary>
    public Competencia? PrimeiraCompetencia { get; init; }

    public Competencia? UltimaCompetencia { get; init; }

    /// <summary>Mínimo de despesas para o índice de risco ser publicável (ADR-0009).</summary>
    public int PisoAmostral { get; init; }

    public int VersaoDoMotor { get; init; }

    public bool AmostraSuficiente => DespesasAvaliadas >= PisoAmostral;

    /// <summary>Regras que não puderam avaliar nada. O silêncio delas não é conclusivo.</summary>
    public IReadOnlyList<CoberturaDaRegra> SemDado =>
        [.. PorRegra.Where(regra => regra.Situacao == SituacaoDaRegra.SemDado)];

    /// <summary>Regras que avaliaram só parte das despesas.</summary>
    public IReadOnlyList<CoberturaDaRegra> Parciais =>
        [.. PorRegra.Where(regra => regra.Situacao == SituacaoDaRegra.CoberturaParcial)];

    public IReadOnlyList<CoberturaDaRegra> Falhas =>
        [.. PorRegra.Where(regra => regra.Situacao == SituacaoDaRegra.Falhou)];

    /// <summary>Quantas regras produziram resultado que se pode interpretar.</summary>
    public int RegrasConclusivas => PorRegra.Count(regra => regra.SilencioEhConclusivo);

    /// <summary>
    /// Frase pronta para a página, no vocabulário do <c>ADR-0004</c>. Sem ela, cada
    /// consumidor da API redigiria a própria — e o risco de alguém escrever "nada
    /// irregular encontrado" é justamente o que o produto não pode correr.
    /// </summary>
    public string Resumo()
    {
        var total = PorRegra.Count;
        var incompletas = SemDado.Count + Parciais.Count;

        if (total == 0)
        {
            return "Nenhuma regra foi aplicada a este período.";
        }

        var periodo = PrimeiraCompetencia is { } inicio && UltimaCompetencia is { } fim
            ? $" no período de {inicio} a {fim}"
            : string.Empty;

        var baseDaFrase =
            $"{total} regras aplicadas a {DespesasAvaliadas} despesas{periodo}.";

        if (incompletas == 0)
        {
            return baseDaFrase;
        }

        return baseDaFrase
            + $" {incompletas} não puderam ser avaliadas integralmente por ausência de dado "
            + "público — a ausência de indicador nelas não significa ausência de achado.";
    }
}

/// <summary>Mede que dado uma despesa tem disponível, no contexto de um parlamentar.</summary>
internal static class DisponibilidadeDeDados
{
    public static DadoExigido Em(ContextoDeAvaliacao contexto, Despesa despesa)
    {
        var disponivel = DadoExigido.Nenhum;

        if (CompatibilidadeCnae.ConheceOTipo(despesa.TipoDespesa))
        {
            disponivel |= DadoExigido.TipoNaTabelaCurada;
        }

        if (contexto.LimiteVigenteEm(DateOnly.FromDateTime(despesa.DataDocumento.UtcDateTime))
            is not null)
        {
            disponivel |= DadoExigido.TetoDaCota;
        }

        if (contexto.DoacoesDaCampanha is not null)
        {
            disponivel |= DadoExigido.CampanhaDoParlamentar;
        }

        if (!despesa.DocumentoFornecedor.EhVazio)
        {
            disponivel |= DadoExigido.DocumentoDoFornecedor;
        }

        if (contexto.Parlamentar.Tipo == TipoParlamentar.Senador
            || Despesa.PrecisaoDaOrigem(despesa.ValorLiquido) == 0.01m)
        {
            disponivel |= DadoExigido.CentavosDoValor;
        }

        if (!TiposPresenciais.EhRubricaAmbigua(despesa.TipoDespesa))
        {
            disponivel |= DadoExigido.NaturezaDaDespesa;
        }

        if (contexto.FornecedorDe(despesa) is not { } fornecedor)
        {
            return disponivel;
        }

        disponivel |= DadoExigido.CadastroDoFornecedor;

        if (!string.IsNullOrWhiteSpace(fornecedor.CnaePrincipal)
            || fornecedor.CnaesSecundarios.Count > 0)
        {
            disponivel |= DadoExigido.CnaeDoFornecedor;
        }

        if (fornecedor.SituacaoCadastral != SituacaoCadastral.Desconhecida)
        {
            disponivel |= DadoExigido.SituacaoDoFornecedor;
        }

        if (fornecedor.DataInicioAtividade is not null)
        {
            disponivel |= DadoExigido.AberturaDoFornecedor;
        }

        // Nulo em 65% da base real: a BrasilAPI só informa quando sabe. E optante sem a
        // data da opção não diz se esta despesa foi paga a um MEI.
        if (fornecedor.EraMeiEm(DateOnly.FromDateTime(despesa.DataDocumento.UtcDateTime)) is not null)
        {
            disponivel |= DadoExigido.OpcaoPeloMei;
        }

        if (fornecedor.Endereco.Uf is { Length: 2 })
        {
            disponivel |= DadoExigido.EnderecoDoFornecedor;
        }

        if (fornecedor.QuadroSocietario.Any(socio => socio.Cpf.TemDigitos))
        {
            disponivel |= DadoExigido.QuadroSocietarioDoFornecedor;
        }

        return disponivel;
    }
}
