using Explanando.Domain.Estado;

namespace Explanando.Domain.Cadeiras;

/// <summary>
/// Como o mapa de poder chama cada camada, em cada poder.
///
/// <para><b>A mesma camada tem nomes diferentes em poderes diferentes, e isso não é
/// enfeite.</b> A <c>Cúpula</c> do Executivo é "Ministros de Estado"; a do Judiciário é
/// "Ministros do STF"; a do Legislativo é "Mesa Diretora". Chamar as três de "Cúpula" na
/// tela faria o leitor traduzir sozinho, e traduzir errado — no Brasil "ministro" significa
/// duas coisas muito diferentes conforme o poder, e é justamente a confusão que um mapa do
/// Estado deveria desfazer.</para>
///
/// <para>Mora no domínio pelo mesmo motivo que <c>VocabularioDoEstado</c>: é vocabulário
/// de leitor, tem dono e tem teste. O navegador recebe o rótulo pronto.</para>
/// </summary>
public static class VocabularioDasCadeiras
{
    /// <summary>O nome da camada, no vocabulário do poder a que ela pertence.</summary>
    public static string De(Poder poder, CamadaDeComando camada) => (poder, camada) switch
    {
        (Poder.Executivo, CamadaDeComando.Chefia) => "Chefia de Estado e de Governo",
        (Poder.Executivo, CamadaDeComando.Cupula) => "Ministros de Estado",
        (Poder.Executivo, CamadaDeComando.Comando) => "Órgãos da Presidência",
        (Poder.Executivo, CamadaDeComando.Representacao) => "Autarquias e agências",

        (Poder.Judiciario, CamadaDeComando.Chefia) => "Presidência do Supremo",
        (Poder.Judiciario, CamadaDeComando.Cupula) => "Ministros do Supremo",
        (Poder.Judiciario, CamadaDeComando.Comando) => "Tribunais superiores",
        (Poder.Judiciario, CamadaDeComando.Representacao) => "Conselhos",

        (Poder.Legislativo, CamadaDeComando.Chefia) => "Presidência das Casas",
        (Poder.Legislativo, CamadaDeComando.Cupula) => "Mesa Diretora",
        (Poder.Legislativo, CamadaDeComando.Comando) => "Lideranças",
        (Poder.Legislativo, CamadaDeComando.Representacao) => "Presidências de comissão",

        (Poder.FuncoesEssenciaisAJustica, CamadaDeComando.Chefia) => "Chefia",
        (Poder.FuncoesEssenciaisAJustica, CamadaDeComando.Cupula) => "Procuradorias e defensorias",

        _ => camada switch
        {
            CamadaDeComando.Chefia => "Chefia",
            CamadaDeComando.Cupula => "Cúpula",
            CamadaDeComando.Comando => "Comando",
            _ => "Representação",
        },
    };

    /// <summary>
    /// A chave estável da camada, para o desenho decidir a faixa.
    ///
    /// <para>Mesma razão de <c>VocabularioDoEstado.ChaveDe</c>: se a posição da faixa no
    /// desenho casasse com o texto de tela, melhorar a redação mudaria o layout sem
    /// ninguém pedir e sem nenhum teste ficar vermelho.</para>
    /// </summary>
    public static string ChaveDe(CamadaDeComando camada) => camada switch
    {
        CamadaDeComando.Chefia => "chefia",
        CamadaDeComando.Cupula => "cupula",
        CamadaDeComando.Comando => "comando",
        _ => "representacao",
    };

    /// <summary>
    /// O que a tela diz quando um poder não tem nenhuma cadeira coletada.
    ///
    /// <para><b>Nomeia o motivo, e não só a ausência.</b> "Sem dados" faria parecer falha
    /// nossa ou desleixo da fonte; o motivo real é que o cadastro oficial da estrutura não
    /// publica titular, e o Diário Oficial — que publica — exige credencial. O leitor que
    /// souber disso entende o buraco; o que não souber, desconfia do produto inteiro.</para>
    /// </summary>
    public const string PorQueFaltaOcupante =
        "O SIORG, que é o cadastro oficial da estrutura, não publica quem ocupa cada cargo "
        + "— nenhum campo, em nenhum endpoint. Nomeação e exoneração saem no Diário "
        + "Oficial, cujo acesso estruturado exige credencial. O que está aqui veio dos "
        + "portais que publicam ocupante por conta própria.";

    /// <summary>
    /// A ressalva da cadeira transcrita à mão.
    ///
    /// <para>Uma tabela curada envelhece em silêncio: o ministro troca e a nossa linha
    /// continua igual. Dizer isso na tela é mais honesto do que confiar na próxima revisão.</para>
    /// </summary>
    public const string RessalvaDaCuradoria =
        "Transcrito à mão da fonte oficial, porque o portal do órgão não entrega a "
        + "composição em formato legível por máquina. Confira a data de verificação.";
}
