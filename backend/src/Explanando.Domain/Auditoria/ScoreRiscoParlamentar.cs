using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Auditoria;

/// <summary>
/// Os números públicos de um parlamentar. Relação 1:1 com <c>Parlamentar</c>.
///
/// <para><b>São dois números independentes, não um</b> (ADR-0009), porque o produto tem dois
/// compradores com necessidades opostas: o cidadão quer saber <i>quanto</i> se gastou, e o
/// departamento de compliance quer saber se a <i>proporção</i> de indicadores e' anomala.</para>
///
/// <para>A versao anterior guardava so' a soma bruta dos pesos das flags. Somar sem dividir
/// mede atividade administrativa, nao risco: na base real, um parlamentar com 1 flag vermelha
/// em 3.575 notas aparecia acima de outro com 14 em 752.</para>
///
/// <para>Tudo aqui e' <b>recalculado do zero</b> a cada varredura, nunca incrementado: a
/// varredura roda repetidas vezes sobre o mesmo parlamentar, e acumular inflaria o ranking
/// sem que nenhum gasto novo tivesse existido.</para>
/// </summary>
public sealed class ScoreRiscoParlamentar : Entidade
{
    private ScoreRiscoParlamentar()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    /// <summary>
    /// <b>O número do B2C.</b> Soma do que o erário efetivamente pagou, com estornos abatidos.
    ///
    /// E' o "Ranking de Volume de Gastos" — a peca publica e viral. Tambem e' o numero mais
    /// seguro juridicamente de todo o produto: gastar a cota inteira e' legal, e publicar
    /// quanto se gastou nao imputa conduta nenhuma.
    /// </summary>
    public decimal VolumeTotalGasto { get; private set; }

    /// <summary>Denominador do indice, e o que decide se ha amostra suficiente.</summary>
    public int DespesasAvaliadas { get; private set; }

    /// <summary>
    /// Soma bruta dos pesos das flags. <b>Contexto, nunca ordenacao.</b>
    ///
    /// Ordenar por isto e' o que produzia a distorcao descrita no cabecalho da classe.
    /// </summary>
    public decimal PesoDasFlags { get; private set; }

    /// <summary>Quantas flags de gravidade alta. Contexto na pagina, nunca ordenacao.</summary>
    public int TotalFlagsVermelhas { get; private set; }

    public int TotalFlagsMedias { get; private set; }

    public int TotalFlagsBaixas { get; private set; }

    // ---------------------------------------------------------------
    //  Composicao do indice, por gravidade
    // ---------------------------------------------------------------
    //  O indice sozinho engana. Na calibragem real, o primeiro colocado em risco tinha
    //  ZERO flags de gravidade alta — o numero dele vinha de muitas flags medias. Isso e'
    //  honesto, mas um comprador B2B que veja so' o total pode ler gravidade que nao existe.
    //  A API expoe a composicao junto do total, sempre.

    public decimal PesoFlagsAltas { get; private set; }

    public decimal PesoFlagsMedias { get; private set; }

    public decimal PesoFlagsBaixas { get; private set; }

    /// <summary>Parcela do indice que vem de flags de gravidade alta, em pontos por 100.</summary>
    public decimal IndiceDeAltas => PorCemDespesas(PesoFlagsAltas);

    public decimal IndiceDeMedias => PorCemDespesas(PesoFlagsMedias);

    public decimal IndiceDeBaixas => PorCemDespesas(PesoFlagsBaixas);

    /// <summary>
    /// Verdadeiro quando o indice nao tem nenhuma contribuicao de gravidade alta.
    ///
    /// E' o alerta de leitura: sem isto, um indice alto sugere gravidade que pode nao
    /// existir. Aconteceu na base real e nao e' caso de borda.
    /// </summary>
    public bool IndiceSemGravidadeAlta => PesoFlagsAltas == 0m && PesoDasFlags > 0m;

    /// <summary>
    /// <b>O número do B2B.</b> Densidade de indicadores, em pontos por 100 despesas
    /// avaliadas.
    ///
    /// E' o ativo da API de due diligence de PEPs. O ranking de volume qualquer um extrai
    /// dos dados abertos numa tarde; a densidade exige o motor de regras — e' ela que
    /// justifica a cobranca.
    /// </summary>
    public decimal IndiceDeRisco { get; private set; }

    /// <summary>
    /// Se o parlamentar tem despesas bastantes para o indice significar alguma coisa.
    ///
    /// Densidade pura cria a distorcao inversa da soma bruta: 10 despesas com 2 flags dao
    /// 20% e liderariam o ranking com prova fina. Abaixo do piso, o parlamentar fica
    /// <b>fora do ranking de risco</b> — mas continua no de volume, que nao depende de
    /// amostra. Mesma logica do ADR-0007: na duvida, o motor se cala.
    /// </summary>
    public bool AmostraSuficiente { get; private set; }

    /// <summary>Exibida em destaque na pagina publica — mostra se o dado esta fresco.</summary>
    public DateTimeOffset DataUltimaVarredura { get; private set; }

    /// <summary>
    /// Quantas das despesas avaliadas tem o fornecedor identificado no cadastro da Receita.
    ///
    /// <para>Existe porque o indice divide o peso dos achados por TODAS as despesas — e a
    /// maior parte das regras so' examina despesa com fornecedor identificado. Medido na
    /// auditoria de 25/09: 92 dos 498 deputados ranqueados tinham menos da metade das despesas
    /// com fornecedor, quase sempre por passagem aerea, que a Camara publica sem ele. Para
    /// esses, indice baixo podia ser pouca verificacao, nao pouco achado.</para>
    /// </summary>
    public int DespesasComFornecedor { get; private set; }

    /// <summary>
    /// Metade das despesas com fornecedor identificado: abaixo disso, o indice descreve menos
    /// da metade do gasto (decisao de 26/09/2026). Configuravel em ParametrosDoMotor.
    /// </summary>
    public const decimal PisoDeCoberturaPadrao = 0.5m;

    /// <summary>De 0 a 1. Zero quando nao ha despesa — nao ha o que cobrir.</summary>
    public decimal CoberturaDeFornecedor =>
        DespesasAvaliadas == 0 ? 0m : (decimal)DespesasComFornecedor / DespesasAvaliadas;

    /// <summary>
    /// Se o indice descreve gasto bastante para entrar no ranking por indice (decisao de
    /// 26/09/2026). Abaixo do piso o parlamentar continua no ranking de volume e o perfil
    /// continua mostrando o indice — com a cobertura ao lado.
    /// </summary>
    public bool CoberturaSuficiente { get; private set; }

    public static ScoreRiscoParlamentar Criar(Guid parlamentarId) => new()
    {
        ParlamentarId = parlamentarId,
        VolumeTotalGasto = 0m,
        DespesasAvaliadas = 0,
        PesoDasFlags = 0m,
        TotalFlagsVermelhas = 0,
        IndiceDeRisco = 0m,
        AmostraSuficiente = false,
        DataUltimaVarredura = DateTimeOffset.MinValue,
    };

    public void Recalcular(
        IReadOnlyCollection<Despesa> despesas,
        IReadOnlyCollection<FlagAnomalia> flags,
        DateTimeOffset momento,
        int pisoAmostral,
        int despesasComFornecedor,
        decimal pisoDeCobertura = PisoDeCoberturaPadrao)
    {
        ArgumentNullException.ThrowIfNull(despesas);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentOutOfRangeException.ThrowIfNegative(pisoAmostral);
        ArgumentOutOfRangeException.ThrowIfNegative(despesasComFornecedor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(despesasComFornecedor, despesas.Count);

        var volume = 0m;

        foreach (var despesa in despesas)
        {
            volume += despesa.ValorParaScore;
        }

        var pesoAltas = 0m;
        var pesoMedias = 0m;
        var pesoBaixas = 0m;
        var altas = 0;
        var medias = 0;
        var baixas = 0;

        foreach (var flag in flags)
        {
            switch (flag.Gravidade)
            {
                case Gravidade.Alta:
                    pesoAltas += flag.Peso;
                    altas++;
                    break;
                case Gravidade.Media:
                    pesoMedias += flag.Peso;
                    medias++;
                    break;
                default:
                    pesoBaixas += flag.Peso;
                    baixas++;
                    break;
            }
        }

        var peso = pesoAltas + pesoMedias + pesoBaixas;

        VolumeTotalGasto = volume;
        DespesasAvaliadas = despesas.Count;
        PesoDasFlags = peso;

        TotalFlagsVermelhas = altas;
        TotalFlagsMedias = medias;
        TotalFlagsBaixas = baixas;

        PesoFlagsAltas = pesoAltas;
        PesoFlagsMedias = pesoMedias;
        PesoFlagsBaixas = pesoBaixas;

        IndiceDeRisco = PorCemDespesas(peso);

        AmostraSuficiente = despesas.Count >= pisoAmostral && despesas.Count > 0;

        DespesasComFornecedor = despesasComFornecedor;
        CoberturaSuficiente = despesas.Count > 0 && CoberturaDeFornecedor >= pisoDeCobertura;

        DataUltimaVarredura = momento;
    }

    /// <summary>
    /// Densidade em pontos por 100 despesas. Sem despesa nao ha denominador — e dividir
    /// por zero seria o jeito mais bobo de derrubar a varredura.
    /// </summary>
    private decimal PorCemDespesas(decimal peso) =>
        DespesasAvaliadas == 0 ? 0m : peso / DespesasAvaliadas * 100m;
}
