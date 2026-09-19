using Explanando.Domain.Comum;

namespace Explanando.Domain.Estado;

/// <summary>O que aconteceu com uma unidade entre duas coletas.</summary>
public enum TipoDeMudanca
{
    /// <summary>Apareceu pela primeira vez.</summary>
    Criada = 1,

    /// <summary>Continuou existindo, mas um campo mudou.</summary>
    Alterada = 2,

    /// <summary>
    /// Sumiu de uma coleta <b>completa</b>.
    ///
    /// <para>Não é "extinta": o SIORG não diz isso. Some da resposta quem foi extinto, quem
    /// foi incorporado e quem o cadastro deixou de publicar. O nome do tipo é literal de
    /// propósito — afirmar extinção seria inventar um fato jurídico a partir de um silêncio
    /// de API.</para>
    /// </summary>
    Desaparecida = 3,

    /// <summary>Voltou a aparecer depois de ter sumido.</summary>
    Reaparecida = 4,
}

/// <summary>
/// Uma linha da história da estrutura do Estado.
///
/// <para><b>Esta tabela é o produto, não um log.</b> O SIORG publica um retrato do agora e
/// nada mais — medido em <c>docs/contratos/siorg.md</c>: os campos de histórico chegam
/// vazios e os parâmetros de versão são ignorados. Sem guardar o que mudou a cada coleta, o
/// grafo responderia "como o Estado está" e nunca "o que andou mudando", que é metade do
/// valor da referência que originou este contexto.</para>
///
/// <para><b>O nome fica congelado.</b> Guardar só o código faria uma linha de 2027 ficar
/// ilegível assim que o órgão fosse renomeado. O nome da época é parte do fato.</para>
/// </summary>
public sealed class MudancaEstrutural : Entidade
{
    private MudancaEstrutural()
    {
        // EF Core.
        NomeNaEpoca = string.Empty;
    }

    public int CodigoUnidade { get; private set; }

    /// <summary>O nome que a unidade tinha quando a mudança foi detectada.</summary>
    public string NomeNaEpoca { get; private set; }

    public TipoDeMudanca Tipo { get; private set; }

    /// <summary>Nulos quando o tipo não é <see cref="TipoDeMudanca.Alterada"/>.</summary>
    public string? Campo { get; private set; }
    public string? De { get; private set; }
    public string? Para { get; private set; }

    public DateTimeOffset DetectadaEm { get; private set; }

    public static MudancaEstrutural Criacao(int codigo, string nome, DateTimeOffset quando) =>
        Nova(codigo, nome, TipoDeMudanca.Criada, quando);

    public static MudancaEstrutural Desaparecimento(int codigo, string nome, DateTimeOffset quando) =>
        Nova(codigo, nome, TipoDeMudanca.Desaparecida, quando);

    public static MudancaEstrutural Reaparecimento(int codigo, string nome, DateTimeOffset quando) =>
        Nova(codigo, nome, TipoDeMudanca.Reaparecida, quando);

    public static MudancaEstrutural Alteracao(
        int codigo,
        string nome,
        MudancaDeCampo campo,
        DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(campo);

        var mudanca = Nova(codigo, nome, TipoDeMudanca.Alterada, quando);

        mudanca.Campo = campo.Campo;
        mudanca.De = campo.De;
        mudanca.Para = campo.Para;

        return mudanca;
    }

    /// <summary>
    /// A frase que vai para a tela, no vocabulário de quem lê.
    ///
    /// <para>Mesma razão do <c>CatalogoDeRegras</c> e do ADR-0004: <c>CodigoPai</c> é
    /// telemetria. Quem acompanha a estrutura do Estado precisa de "passou a responder a
    /// outro órgão".</para>
    /// </summary>
    public string Descrever() => Tipo switch
    {
        TipoDeMudanca.Criada => "passou a constar na estrutura oficial",
        TipoDeMudanca.Desaparecida => "deixou de constar na estrutura oficial",
        TipoDeMudanca.Reaparecida => "voltou a constar na estrutura oficial",
        _ => DescreverCampo(),
    };

    /// <summary>
    /// A chave estável do tipo, para o desenho decidir o selo.
    ///
    /// <para>Mesma razão de <c>VocabularioDoEstado.ChaveDe</c>: a cor e o formato do selo
    /// não podem casar com o texto de tela, senão melhorar a redação muda a aparência sem
    /// ninguém pedir.</para>
    /// </summary>
    public string Chave() => Tipo switch
    {
        TipoDeMudanca.Criada => "criada",
        TipoDeMudanca.Alterada => "alterada",
        TipoDeMudanca.Desaparecida => "desapareceu",
        TipoDeMudanca.Reaparecida => "voltou",
        _ => "outra",
    };

    /// <summary>
    /// O selo curto que vai no cartão.
    ///
    /// <para>Curto de propósito: ele fica ao lado do nome do órgão, e um selo mais longo
    /// que o nome inverte a hierarquia da leitura.</para>
    /// </summary>
    public string Selo() => Tipo switch
    {
        TipoDeMudanca.Criada => "Passou a constar",
        TipoDeMudanca.Alterada => "Alterada",
        TipoDeMudanca.Desaparecida => "Deixou de constar",
        TipoDeMudanca.Reaparecida => "Voltou a constar",
        _ => "Mudou",
    };

    private string DescreverCampo() => Campo switch
    {
        "Nome" => "mudou de nome",
        "Sigla" => "mudou de sigla",
        "CodigoPai" => "passou a responder a outra unidade",
        "CodigoOrgao" => "passou a integrar outro órgão",
        "Poder" => "passou a ser classificada em outro poder",
        "Esfera" => "mudou de esfera de governo",
        "Tipo" => "passou a ser classificada de outra forma",
        "NaturezaJuridica" => "mudou de forma jurídica",
        "Normatizacao" => "mudou a norma que a cria",
        _ => "teve um dado cadastral alterado",
    };

    private static MudancaEstrutural Nova(
        int codigo,
        string nome,
        TipoDeMudanca tipo,
        DateTimeOffset quando)
    {
        if (codigo <= 0)
        {
            throw new ArgumentException(
                $"Mudança precisa apontar para uma unidade; recebido código {codigo}.",
                nameof(codigo));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException(
                "Mudança sem o nome da unidade na época seria ilegível no dia em que o "
                + "órgão fosse renomeado.",
                nameof(nome));
        }

        return new MudancaEstrutural
        {
            CodigoUnidade = codigo,
            NomeNaEpoca = nome.Trim(),
            Tipo = tipo,
            DetectadaEm = quando,
        };
    }
}
