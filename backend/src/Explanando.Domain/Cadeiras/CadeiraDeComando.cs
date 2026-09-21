using Explanando.Domain.Estado;

namespace Explanando.Domain.Cadeiras;

/// <summary>
/// Em que camada do comando a cadeira fica.
///
/// <para>São quatro em todos os poderes, e o que cada uma significa muda com o poder —
/// ver <see cref="VocabularioDasCadeiras"/>. É o eixo vertical do mapa de poder, o mesmo
/// que a referência usa: quem manda em cima, quem executa embaixo.</para>
/// </summary>
public enum CamadaDeComando
{
    /// <summary>Quem chefia o poder. Presidente da República, Presidente do STF.</summary>
    Chefia = 0,

    /// <summary>O colegiado ou o primeiro escalão. Ministros de Estado, ministros do STF.</summary>
    Cupula = 1,

    /// <summary>Quem comanda uma casa ou um tribunal dentro do poder.</summary>
    Comando = 2,

    /// <summary>Quem representa um grupo: líder de partido, presidente de comissão.</summary>
    Representacao = 3,
}

/// <summary>
/// De onde veio a informação de quem ocupa a cadeira.
///
/// <para><b>Vai para a tela, e não só para o log.</b> Uma cadeira lida de uma API que roda
/// toda semana e uma cadeira transcrita à mão de um portal têm confiabilidades diferentes,
/// e o leitor tem direito de saber qual está olhando.</para>
/// </summary>
public enum OrigemDaCadeira
{
    /// <summary>Coletada de uma fonte que a máquina lê sozinha, e que se atualiza.</summary>
    Automatica = 1,

    /// <summary>
    /// Transcrita à mão de uma fonte que não é legível por máquina.
    ///
    /// <para>É o caso do STF: o portal é uma aplicação de página única e não serve o HTML
    /// da composição. Curada é aceitável para onze cadeiras que mudam uma vez por ano;
    /// não seria para trinta e oito ministérios.</para>
    /// </summary>
    Curada = 2,
}

/// <summary>Os dados de criação de uma cadeira, vindos de um coletor.</summary>
public sealed record NovaCadeira
{
    public required Poder Poder { get; init; }
    public required CamadaDeComando Camada { get; init; }

    /// <summary>O cargo, como a fonte o nomeia: "Ministro de Estado da Fazenda".</summary>
    public required string Cargo { get; init; }

    /// <summary>
    /// Quem ocupa. <b>Nulo é cadeira vaga</b>, e não "não sabemos".
    ///
    /// <para>Quando não sabemos, a cadeira não é coletada. É a distinção do ADR-0009
    /// aplicada a pessoas: ausência de informação e ausência de ocupante são fatos
    /// diferentes, e o desenho os mostra diferentes.</para>
    /// </summary>
    public string? Ocupante { get; init; }

    /// <summary>O órgão a que a cadeira pertence, quando a fonte informa.</summary>
    public string? Orgao { get; init; }

    /// <summary>
    /// O nó do grafo do Estado, quando conhecido.
    ///
    /// <para>É o que liga as duas vistas: clicar num ministério no mapa de instituições e
    /// achar o ministro no mapa de poder. Nulo quando a ponte por nome não resolve, e
    /// nulo <b>não</b> é motivo para descartar a cadeira.</para>
    /// </summary>
    public int? CodigoSiorg { get; init; }

    public string? Partido { get; init; }
    public string? Uf { get; init; }
    public string? FotoUrl { get; init; }

    /// <summary>A URL de onde isto foi lido. Obrigatória: sem fonte, não entra.</summary>
    public required string Fonte { get; init; }

    public required OrigemDaCadeira Origem { get; init; }
}

/// <summary>
/// Uma cadeira de comando do Estado, e quem está sentado nela.
///
/// <para><b>Esta tabela é a exceção deliberada à regra de não guardar nome de pessoa.</b>
/// A regra existe por causa do quadro de pessoal: 778 mil vínculos nominais do Portal da
/// Transparência, que o <c>ServicoDeColetaDePessoal</c> agrega durante a leitura e nunca
/// persiste — há um teste de reflexão que quebra o build se alguém tentar. Aqui é outra
/// coisa: são cerca de cem cargos de comando cujo ocupante o próprio governo publica com
/// nome e sobrenome, no site da Presidência e nas APIs do Congresso. Um ministro de Estado
/// não tem expectativa de privacidade sobre o fato de ser ministro.</para>
///
/// <para><b>Toda cadeira carrega a fonte e a data em que foi vista.</b> Não é metadado de
/// auditoria: é o que permite à tela dizer "verificado em tal dia, nesta página", que é a
/// diferença entre informar e afirmar. Uma tabela de ocupantes envelhece em silêncio — o
/// ministro troca e o site continua igual —, e a data é o único antídoto.</para>
/// </summary>
public sealed class CadeiraDeComando
{
    private CadeiraDeComando()
    {
        Cargo = string.Empty;
        Fonte = string.Empty;
        Coletor = string.Empty;
    }

    /// <param name="coletor">
    /// Quem leu esta linha — <c>planalto</c>, <c>camara</c>, <c>liderancas</c>. É a chave
    /// de substituição: cada coleta apaga só o que aquele coletor gravou antes.
    /// </param>
    public static CadeiraDeComando Criar(NovaCadeira nova, string coletor, DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(nova);

        if (string.IsNullOrWhiteSpace(coletor))
        {
            throw new ArgumentException("A cadeira precisa saber quem a coletou.", nameof(coletor));
        }

        if (string.IsNullOrWhiteSpace(nova.Cargo))
        {
            throw new ArgumentException("A cadeira precisa de um cargo.", nameof(nova));
        }

        if (string.IsNullOrWhiteSpace(nova.Fonte))
        {
            throw new ArgumentException(
                "A cadeira precisa da fonte de onde foi lida.",
                nameof(nova));
        }

        return new CadeiraDeComando
        {
            Id = Guid.CreateVersion7(),
            Coletor = coletor.Trim(),
            Poder = nova.Poder,
            Camada = nova.Camada,
            Cargo = nova.Cargo.Trim(),
            Ocupante = Limpar(nova.Ocupante),
            Orgao = Limpar(nova.Orgao),
            CodigoSiorg = nova.CodigoSiorg,
            Partido = Limpar(nova.Partido),
            Uf = Limpar(nova.Uf),
            FotoUrl = Limpar(nova.FotoUrl),
            Fonte = nova.Fonte.Trim(),
            Origem = nova.Origem,
            VerificadoEm = quando,
        };
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Quem leu esta linha.
    ///
    /// <para>Existe para a substituição ser <b>exata</b>. A primeira versão deduzia o
    /// coletor do prefixo da URL — <c>gov.br</c> é o Planalto, <c>legis.senado</c> são as
    /// lideranças —, o que é uma tabela de adivinhação escondida no repositório: bastaria
    /// o Planalto mudar de domínio para a coleta parar de apagar a anterior e passar a
    /// dobrar o Executivo a cada passada, silenciosamente.</para>
    /// </summary>
    public string Coletor { get; private set; }

    public Poder Poder { get; private set; }

    public CamadaDeComando Camada { get; private set; }

    public string Cargo { get; private set; }

    /// <summary>Nulo é cadeira vaga. Ver <see cref="NovaCadeira.Ocupante"/>.</summary>
    public string? Ocupante { get; private set; }

    public string? Orgao { get; private set; }

    public int? CodigoSiorg { get; private set; }

    public string? Partido { get; private set; }

    public string? Uf { get; private set; }

    public string? FotoUrl { get; private set; }

    public string Fonte { get; private set; }

    public OrigemDaCadeira Origem { get; private set; }

    /// <summary>Quando esta linha foi vista na fonte. Ver a nota da classe.</summary>
    public DateTimeOffset VerificadoEm { get; private set; }

    /// <summary>A cadeira está vaga?</summary>
    public bool Vaga => Ocupante is null;

    /// <summary>
    /// Há quantos dias esta cadeira não é verificada.
    /// </summary>
    public int DiasSemVerificar(DateTimeOffset agora) =>
        Math.Max(0, (int)(agora - VerificadoEm).TotalDays);

    private static string? Limpar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
