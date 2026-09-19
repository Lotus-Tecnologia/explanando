namespace Explanando.Domain.Rede;

/// <summary>
/// Quando dois parlamentares aparecem ligados no mapa de fornecedores — e, sobretudo,
/// quando <b>não</b> aparecem.
///
/// <para><b>O critério inteiro existe por causa de uma medição.</b> Dos 41.105 fornecedores
/// da base, 15.272 atendem mais de um parlamentar. Os mais compartilhados são a CASCOL
/// Combustíveis (417 parlamentares), o Auto Posto Cinco Estrelas (369), a WMS Papelaria
/// (313), a TAM (274) e a Telefônica (258). Ligar dois deputados porque ambos abastecem no
/// mesmo posto de Brasília não descreve arranjo nenhum: descreve a geografia de uma cidade
/// em que todo mundo trabalha no mesmo quarteirão.</para>
///
/// <para>Um grafo assim seria um emaranhado com quase todo o Congresso num nó só, e cada
/// linha dele afirmaria ao leitor uma ligação que não existe. É o mesmo erro que a ponte
/// SIAPE↔SIORG se recusa a cometer por distância de edição: parecer um achado não é ser um
/// achado.</para>
///
/// <para>O sinal está no extremo oposto. Fornecedor que atende <i>poucos</i> gabinetes e
/// movimenta <i>muito</i> não é infraestrutura da cidade — é uma relação escolhida. Este
/// arquivo guarda os dois números que separam um caso do outro, e a razão de cada um.</para>
///
/// <para>Nada disso conclui coisa alguma sobre conduta (<c>ADR-0004</c>). Duas pessoas
/// contratarem a mesma agência de comunicação é lícito e corriqueiro. O mapa mostra a
/// coincidência para quem quiser puxar o fio — e <see cref="Ressalva"/> diz isso na tela.</para>
/// </summary>
public static class CriteriosDaRede
{
    /// <summary>
    /// Abaixo de dois parlamentares não há ligação nenhuma a desenhar: é um fornecedor de
    /// um gabinete só, que já aparece no perfil dele.
    /// </summary>
    public const int ParlamentaresMinimos = 2;

    /// <summary>
    /// O teto é o que transforma o emaranhado em mapa.
    ///
    /// <para>Cinco, e não três ou dez, por como a distribuição se comporta: 12.753 dos
    /// 15.272 fornecedores compartilhados atendem de dois a cinco parlamentares, e 96
    /// atendem mais de cinquenta. O vale entre esses dois grupos é largo, e é nele que o
    /// corte cai — não em cima de uma fronteira disputada.</para>
    ///
    /// <para>Acima do teto o fornecedor não fica "menos suspeito": ele deixa de ser
    /// <i>informativo</i>. Uma companhia aérea que atende 274 parlamentares não liga
    /// nenhum deles a nenhum outro.</para>
    /// </summary>
    public const int ParlamentaresMaximos = 5;

    /// <summary>
    /// Valor mínimo, somando todos os parlamentares, para a ligação ser desenhada.
    ///
    /// <para>Sem piso, dois gabinetes que pagaram R$ 80 ao mesmo estacionamento viram uma
    /// aresta com o mesmo peso visual de dois que dividem uma agência de R$ 1,5 milhão. O
    /// piso não julga o fornecedor pequeno: ele reconhece que uma nota isolada não é uma
    /// relação.</para>
    ///
    /// <para>Bem mais alto que o <c>ValorMinimoPorParlamentarNaConcentracao</c> de
    /// R$ 1.000 da REG_NET_004 porque as duas coisas medem o oposto uma da outra: lá o
    /// piso serve para <i>não descartar</i> uma relação real, e a regra tem outras três
    /// condições segurando o portão. Aqui o piso é a única condição além da contagem, e um
    /// mapa é lido de relance — a aresta que aparece já é a afirmação.</para>
    /// </summary>
    public const decimal ValorMinimoDaLigacao = 50_000m;

    /// <summary>
    /// Quantos fornecedores em comum dois parlamentares precisam ter para o par ser
    /// <b>denso</b> — e ir para o desenho.
    ///
    /// <para><b>Este número existe porque o mapa inteiro não é um mapa.</b> Medido: dos
    /// 463 parlamentares que entram, <b>454 caem num único componente conexo</b>. Basta
    /// uma corrente de fornecedores para ligar quase todo mundo a quase todo mundo, e um
    /// desenho em que "estar conectado" vale para 98% das pessoas não informa
    /// nada.</para>
    ///
    /// <para>Exigir dois fornecedores em comum corta 1.386 pares para <b>247</b>, em
    /// <b>49 agrupamentos separados</b> de 2 a 14 pessoas — que é um mapa de verdade, com
    /// ilhas que se leem uma a uma. Um fornecedor em comum é coincidência; dois é um
    /// padrão pequeno, e é o mínimo que se pode desenhar sem prometer mais do que o dado
    /// tem.</para>
    /// </summary>
    public const int FornecedoresParaParDenso = 2;

    /// <summary>
    /// O que a densidade acaba mostrando, e que o leitor precisa saber antes de olhar.
    ///
    /// <para>Medido sobre os 247 pares densos: <b>91% são do mesmo estado</b> e apenas
    /// <b>19% do mesmo partido</b>. A explicação dominante é geográfica — gabinetes de um
    /// estado contratam no mercado daquele estado —, e não partidária.</para>
    ///
    /// <para>Isso é informação, não decepção: é a razão de a <c>REG_NET_004</c>, que exige
    /// exclusividade partidária, produzir apenas 19 marcações na base inteira. E é o que
    /// torna um par de <i>estados diferentes</i> o caso raro que vale olhar.</para>
    /// </summary>
    public const string OQueADensidadeMostra =
        "A proximidade aqui é sobretudo geográfica: 91% dos pares que dividem dois ou mais "
        + "fornecedores são do mesmo estado, e só 19% são do mesmo partido. Gabinetes de um "
        + "estado contratam no mercado daquele estado, e isso explica a maior parte do "
        + "desenho. Os pares de estados diferentes são os poucos que fogem à regra.";

    /// <summary>
    /// O fornecedor liga gabinetes no mapa?
    /// </summary>
    public static bool Liga(int parlamentaresAtendidos, decimal valorLiquidoTotal) =>
        parlamentaresAtendidos >= ParlamentaresMinimos
        && parlamentaresAtendidos <= ParlamentaresMaximos
        && valorLiquidoTotal >= ValorMinimoDaLigacao;

    /// <summary>
    /// A frase que acompanha o mapa, sempre.
    ///
    /// <para>Mora no domínio, e não no componente de tela, pela mesma razão que
    /// <c>MudancaEstrutural.Descrever</c>: é uma afirmação sobre o que o dado significa, e
    /// uma afirmação dessas tem que ser testável e ter um dono.</para>
    /// </summary>
    public const string Ressalva =
        "Fornecedor em comum não é indício de irregularidade. Dois gabinetes contratarem a "
        + "mesma empresa é lícito e comum. O mapa mostra onde isso acontece com fornecedores "
        + "pouco compartilhados e de valor alto, que é onde a coincidência diz mais.";

    /// <summary>
    /// Por que este fornecedor entrou e a companhia aérea não — em uma frase, para o painel.
    /// </summary>
    public static string PorQueEsteCorte { get; } =
        $"Entram no mapa fornecedores que atendem de {ParlamentaresMinimos} a "
        + $"{ParlamentaresMaximos} parlamentares e somam ao menos "
        + $"R$ {ValorMinimoDaLigacao / 1_000m:0} mil. Postos de combustível, companhias "
        + "aéreas e operadoras de telefonia atendem centenas de gabinetes e ficam de fora: "
        + "eles não ligam ninguém a ninguém.";
}
