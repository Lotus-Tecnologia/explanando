using System.Text;

namespace Explanando.Domain.Cotas;

/// <summary>O que a tabela conseguiu concluir sobre um par (tipo de despesa, fornecedor).</summary>
public enum ResultadoDeCompatibilidade
{
    /// <summary>Falta dado para julgar. REG_KYB_001 nao emite flag.</summary>
    SemBaseParaAvaliar = 0,

    /// <summary>Ao menos um CNAE do fornecedor pertence a uma familia esperada.</summary>
    Compativel = 1,

    /// <summary>O fornecedor tem CNAE, e nenhum deles pertence a familia esperada.</summary>
    Incompativel = 2,
}

/// <summary>
/// Tabela curada de compatibilidade entre tipo de despesa da CEAP e ramo de atividade
/// (CNAE) do fornecedor. Insumo unico de REG_KYB_001.
///
/// <para><b>Postura permissiva</b> (ADR-0007). Tres escolhas de projeto sustentam isso:</para>
///
/// <list type="number">
/// <item>Compara por <b>prefixo</b> de CNAE, nao por codigo exato. O CNAE tem 7 digitos
/// (divisao-grupo-classe-subclasse); casar por 2 a 5 digitos aceita a familia inteira e
/// dispensa acertar a subclasse exata, que muda com o tempo.</item>
/// <item>Basta <b>um</b> CNAE do fornecedor bater — principal ou secundario. Exigir que o
/// principal batesse marcaria empresas legitimas de atividade mista, como o posto da amostra
/// real, que tem "bares" entre os secundarios.</item>
/// <item>Na duvida, <b>silencio</b>. Fornecedor sem CNAE, CNAE ilegivel ou tipo de despesa
/// fora da tabela produzem <see cref="ResultadoDeCompatibilidade.SemBaseParaAvaliar"/>.</item>
/// </list>
///
/// <para>O motivo e' o mesmo do ADR-0004: cada falso positivo aqui vira uma imputacao
/// indevida numa pagina publica indexada, com nome e foto de uma pessoa real. A regra so'
/// fala quando o desencontro e' evidente.</para>
///
/// <para>Os 24 tipos vieram de <c>/referencias/deputados/tipoDespesa</c>, capturado em
/// <c>docs/contratos/camara-tipos-despesa.json</c>.</para>
/// </summary>
public static class CompatibilidadeCnae
{
    /// <summary>
    /// Ramos que nunca caracterizam desencontro, seja qual for o tipo de despesa.
    ///
    /// <b>84 — administracao publica.</b> Pagamento a prefeitura ou orgao publico e' imposto,
    /// taxa ou tarifa, nao contratacao de fornecedor. Na calibragem contra dado real, o
    /// "Municipio de Belo Horizonte" sozinho gerou 386 flags de manutencao de escritorio —
    /// todas eram IPTU do imovel.
    /// </summary>
    private static readonly string[] SempreAceitos = ["84"];

    /// <summary>
    /// Chave normalizada (sem acento, minuscula, sem pontuacao final) -> prefixos de CNAE
    /// aceitos. Ver <see cref="Normalizar"/>.
    ///
    /// <para>As listas foram <b>calibradas contra 28.540 despesas reais</b>. A versao
    /// anterior, escrita sem dado, marcava 5,2% de todas as despesas — alto demais para uma
    /// regra que deveria falar so' no desencontro evidente.</para>
    /// </summary>
    private static readonly Dictionary<string, string[]> Tabela = new(StringComparer.Ordinal)
    {
        // 1 — o rotulo engana: na pratica este tipo cobre quase tudo que um escritorio
        // consome. A calibragem real mostrou nuvem, software, seguro, locacao de
        // equipamento, servicos administrativos e supermercado — todos legitimos, e todos
        // marcados pela versao anterior da tabela.
        ["manutencao de escritorio de apoio a atividade parlamentar"] =
            [
                "68", "35", "36", "37", "38", "81", "61", "43", "41",  // imovel e utilidades
                "62", "63",                                            // software, nuvem, provedor
                "64", "65", "66",                                      // servicos financeiros e seguro
                "77",                                                  // locacao de maquinas e equipamentos
                "82",                                                  // servicos administrativos
                "47",                                                  // varejo em geral
                "95",                                                  // reparacao e manutencao
            ],

        // 2 — hospedagem, alimentacao, transporte terrestre e agencias de viagem.
        ["locomocao, alimentacao e hospedagem"] = ["55", "56", "49", "79"],

        // 3 — varejo e atacado de combustivel, lubrificantes e refino.
        ["combustiveis e lubrificantes"] = ["473", "468", "192"],

        // 4 — servicos profissionais: juridico, contabil, gestao, engenharia, P&D,
        // pesquisa de mercado, TI e treinamento.
        ["consultorias, pesquisas e trabalhos tecnicos"] =
            ["69", "70", "71", "72", "73", "62", "63", "85"],

        // 5 — grafica, editoria, audiovisual, radio/TV, portais, publicidade, design e eventos.
        ["divulgacao da atividade parlamentar"] =
            ["18", "58", "59", "60", "63", "73", "74", "823"],

        // 6 — papelaria, atacado de artigos de escritorio, informatica e papel.
        ["aquisicao de material de escritorio"] = ["476", "464", "475", "17", "18"],

        // 7 — software, servicos de dados, correio e assinaturas.
        // A chave nao leva ponto final: Normalizar remove o ponto, e chave com ponto
        // nunca seria encontrada. O teste Toda_entrada_da_tabela_tem_ao_menos_um_prefixo
        // existe para pegar exatamente esse deslize.
        ["aquisicao ou loc. de software; serv. postais; ass"] = ["62", "63", "53", "58", "475"],

        // 8 — vigilancia, seguranca e investigacao.
        ["servico de seguranca prestado por empresa especializada"] = ["80"],

        // 9 / 998 / 999 — transporte aereo e agencias de viagem.
        ["passagem aerea - reembolso"] = ["51", "79"],
        ["passagem aerea - sigepa"] = ["51", "79"],
        ["passagem aerea - rpa"] = ["51", "79"],

        // 10 — telecomunicacoes e varejo de equipamentos.
        ["telefonia"] = ["61", "475"],

        // 11 — correio e entrega.
        ["servicos postais"] = ["53"],

        // 12 — editoras, livrarias e portais de conteudo.
        ["assinatura de publicacoes"] = ["58", "476", "63"],

        // 13 — restaurantes, bufes e varejo de alimentos.
        ["fornecimento de alimentacao do parlamentar"] = ["56", "472"],

        // 14 — hoteis e similares.
        ["hospedagem ,exceto do parlamentar no distrito federal"] = ["55"],

        // 15 / 120 — locacao pura (77) e fretamento com motorista, que e' transporte (49).
        // Aceitar so' uma das duas familias marcaria metade dos contratos legitimos.
        ["locacao de veiculos automotores ou fretamento de embarcacoes"] =
            ["771", "7719", "492", "50", "523"],
        ["locacao ou fretamento de veiculos automotores"] = ["771", "7719", "492"],

        // 119 — taxi aereo, fretamento e aluguel de aeronaves.
        ["locacao ou fretamento de aeronaves"] = ["51", "7739", "5240"],

        // 121 — navegacao e apoio aquaviario.
        ["locacao ou fretamento de embarcacoes"] = ["50", "7719", "523"],

        // 122 — a maior descoberta da calibragem: a Uber tem CNAE 7490-1/04
        // (intermediacao e agenciamento), nao transporte. Ela sozinha respondia por 439 das
        // 458 flags deste tipo. Sindicatos e associacoes de taxistas (94) tambem aparecem
        // como emitentes.
        ["servico de taxi, pedagio e estacionamento"] =
            ["492", "522", "5229", "7490", "94", "5320"],

        // 123 — rodoviario interestadual, aquaviario e agencias.
        ["passagens terrestres, maritimas ou fluviais"] = ["492", "50", "79"],

        // 137 — educacao, organizacao de eventos e associacoes profissionais.
        ["participacao em curso, palestra ou evento similar"] = ["85", "823", "94"],

        // 145 — certificacao digital, TI e varejo de informatica.
        ["aquisicao de tokens e certificados digitais"] = ["62", "63", "475", "8299"],

        // ------------------------------------------------------------------
        //  CEAPS — Senado Federal
        // ------------------------------------------------------------------
        //  As categorias do Senado sao MAIS GROSSAS que as da Camara, e isso enfraquece
        //  esta regra por construcao. A primeira delas junta locomocao, hospedagem,
        //  alimentacao e combustivel numa rubrica so' — 44% das despesas do Senado — e a
        //  lista de prefixos precisa cobrir os quatro dominios. O efeito e' que quase
        //  qualquer CNAE plausivel passa, e a regra so' pega incompatibilidade grosseira.
        //
        //  A alternativa seria deixar a categoria fora da tabela, o que a marcaria como
        //  SemDado na cobertura. Preferiu-se a lista ampla: "construcao civil" contra
        //  "locomocao e alimentacao" continua sendo achado legitimo, e perder isso seria
        //  jogar fora sinal que existe.

        // 45 — locomocao + hospedagem + alimentacao + combustivel, tudo numa rubrica.
        //
        //  Os seis ultimos prefixos entraram na calibragem contra a base real do Senado,
        //  cada um com a contagem do que estava sendo sinalizado indevidamente:
        //
        //    77  locadora de veiculos          1.052   alugar carro E' locomocao
        //    74  agenciamento de viagens       1.655   intermediacao, CNAE profissional
        //    94  associacao de taxistas          115   taxi cooperativado
        //    45  comercio e reparacao de veic.   101   oficina e revenda de combustivel
        //    47  varejo em geral                 100   a rubrica inclui ALIMENTACAO
        //    82  servicos de apoio a escritorio    55   vale-refeicao e vale-combustivel
        //
        //  `47` subsume o antigo `473`, que ficou de fora por redundancia: prefixo de dois
        //  digitos ja casa toda a divisao.
        ["locomocao, hospedagem, alimentacao, combustiveis e lubrificantes"] =
            ["49", "50", "51", "55", "56", "79", "47", "468", "192", "52", "77", "74", "94", "45", "82"],

        // 46 — aluguel de imovel para escritorio politico.
        //
        //  A rubrica diz, ela mesma, "compreendendo despesas CONCERNENTES A ELES" — e o que
        //  concerne a um imovel alugado e' enumeravel. O que a base real mostrou faltando:
        //
        //    81  servicos para edificios       1.638   TAXA DE CONDOMINIO, a maior lacuna
        //    80  vigilancia e seguranca          201   portaria do predio
        //    47  varejo                          168   material de manutencao
        //    82  servicos de apoio a escritorio  145
        //    43  instalacoes e acabamentos       141   eletrica, hidraulica, ar
        //    97  servicos domesticos             104   limpeza contratada de PF
        //    41  construcao de edificios          86   reforma da sala
        //    95  reparacao e manutencao           59
        //
        //  Ficaram DE FORA de proposito, e continuam sinalizando: `69` contabilidade,
        //  `64` fomento mercantil e `73` publicidade. Nenhum dos tres concerne a um imovel,
        //  e e' exatamente esse o sinal que a rubrica ainda pode dar.
        //
        //  A excecao dentro do `64`, decidida em 26/09/2026 (versao 6 do motor): `6462` e
        //  `6463`, as holdings. Holding patrimonial dona do imovel alugado e' comum, e era de
        //  onde vinham 89% do indice do primeiro do ranking do Senado. O resto do `64` segue
        //  sinalizando.
        ["aluguel de imoveis para escritorio politico, compreendendo despesas concernentes a eles"] =
            ["68", "6810", "6821", "35", "36", "37", "61", "81", "80", "47", "82", "43", "97", "41", "95",
             "6462", "6463"],

        // 47 — passagens, de qualquer modal.
        ["passagens aereas, aquaticas e terrestres nacionais"] = ["49", "50", "51", "79"],

        // 48 — a rubrica de consultorias do Senado NAO tem entrada, e a ausencia e' a
        //  decisao.
        //
        //  Ela estava aqui com a lista da rubrica equivalente da Camara, o que parecia
        //  razoavel: mesmo dominio, mesma familia de CNAEs. A medicao contra a base real
        //  desmentiu — 4.405 flags em 8.462 despesas, 52,06%. Uma regra que sinaliza mais da
        //  METADE do que examina nao esta achando desencontro grosseiro.
        //
        //  A causa esta no texto da propria rubrica, que a da Camara nao tem:
        //  "...E OUTROS SERVICOS DE APOIO AO EXERCICIO DO MANDATO PARLAMENTAR". Isso e' um
        //  conjunto ABERTO, e contra um conjunto aberto nenhum ramo de atividade pode ser
        //  declarado incompativel. Das 4.405, 3.800 eram uma agencia de viagens faturando
        //  nesta rubrica — escolha de classificacao do gabinete, nao desencontro do
        //  fornecedor.
        //
        //  Sem entrada, `Avaliar` devolve SemBaseParaAvaliar e a cobertura exibe "nao
        //  avaliado" com o motivo. Nao e' esconder: e' declarar, que e' o ADR-0007.

        // 49 — divulgacao NAO tem entrada propria: a rubrica do Senado normaliza para a
        //  mesma chave da Camara ("DIVULGACAO DA ATIVIDADE PARLAMENTAR." perde o ponto
        //  final em Normalizar). A entrada de cima ja atende as duas casas — e foi o
        //  analisador que pegou a duplicata, que teria sobrescrito em silencio.

        // 50 — material de consumo, software, postais, publicacoes e locacao de moveis.
        //  Outra rubrica-guarda-chuva: o proprio texto dela lista cinco coisas diferentes.
        //  A lista antiga so' tinha subclasses estreitas de comercio — `476` livraria,
        //  `475` eletro, `464` atacado de bens de consumo. Material de consumo de escritorio
        //  se compra em QUALQUER varejo, e eram 724 despesas dizendo isso; mais 86 no
        //  atacado. `47` e `46` subsumem as tres antigas.
        ["aquisicao de material de consumo para uso no escritorio politico, inclusive aquisicao ou locacao de software, despesas postais, aquisicao de publicacoes, locacao de moveis e de equipamentos"] =
            ["47", "46", "17", "18", "77", "62", "63", "53", "58"],

        // 51 — seguranca privada.
        ["servicos de seguranca privada"] = ["80"],

    };

    /// <summary>Os tipos de despesa que a tabela cobre, em forma normalizada.</summary>
    public static IReadOnlyCollection<string> TiposCobertos => Tabela.Keys;

    public static IReadOnlyList<string> PrefixosPara(string tipoDespesa) =>
        Tabela.TryGetValue(Normalizar(tipoDespesa), out var prefixos) ? prefixos : [];

    /// <summary>
    /// Se a tabela curada cobre este tipo de despesa. Quando nao cobre, REG_KYB_001 nao
    /// tem base para opinar — e o relatorio de cobertura precisa dizer isso, em vez de
    /// deixar o silencio passar por aprovacao.
    /// </summary>
    public static bool ConheceOTipo(string? tipoDespesa) =>
        PrefixosPara(tipoDespesa ?? string.Empty).Count > 0;

    /// <summary>
    /// Julga se o ramo do fornecedor combina com o tipo de despesa.
    ///
    /// So' devolve <see cref="ResultadoDeCompatibilidade.Incompativel"/> quando ha' base
    /// para isso: tipo conhecido <b>e</b> ao menos um CNAE legivel que nao bate em nada.
    /// </summary>
    public static ResultadoDeCompatibilidade Avaliar(
        string? tipoDespesa,
        IEnumerable<string?> cnaesDoFornecedor)
    {
        ArgumentNullException.ThrowIfNull(cnaesDoFornecedor);

        var prefixos = PrefixosPara(tipoDespesa ?? string.Empty);

        if (prefixos.Count == 0)
        {
            return ResultadoDeCompatibilidade.SemBaseParaAvaliar;
        }

        var houveCnaeLegivel = false;

        foreach (var bruto in cnaesDoFornecedor)
        {
            var cnae = SomenteDigitos(bruto);

            if (cnae.Length < 2)
            {
                continue;
            }

            houveCnaeLegivel = true;

            foreach (var prefixo in SempreAceitos)
            {
                if (cnae.StartsWith(prefixo, StringComparison.Ordinal))
                {
                    return ResultadoDeCompatibilidade.Compativel;
                }
            }

            foreach (var prefixo in prefixos)
            {
                if (cnae.StartsWith(prefixo, StringComparison.Ordinal))
                {
                    return ResultadoDeCompatibilidade.Compativel;
                }
            }
        }

        return houveCnaeLegivel
            ? ResultadoDeCompatibilidade.Incompativel
            : ResultadoDeCompatibilidade.SemBaseParaAvaliar;
    }

    /// <summary>
    /// O texto vem da API em caixa alta e com acento ("COMBUSTÍVEIS E LUBRIFICANTES.").
    /// A chave nao pode depender disso, nem do ponto final, que aparece em alguns tipos
    /// e nao em outros.
    /// </summary>
    private static string Normalizar(string? tipoDespesa)
    {
        if (string.IsNullOrWhiteSpace(tipoDespesa))
        {
            return string.Empty;
        }

        var construtor = new StringBuilder(tipoDespesa.Length);

        foreach (var caractere in tipoDespesa.Trim().ToLowerInvariant())
        {
            construtor.Append(SemAcento(caractere));
        }

        // Espacos repetidos e ponto final variam entre os registros da Camara.
        return construtor.ToString().TrimEnd('.', ' ');
    }

    private static char SemAcento(char caractere) => caractere switch
    {
        'á' or 'à' or 'ã' or 'â' or 'ä' => 'a',
        'é' or 'è' or 'ê' or 'ë' => 'e',
        'í' or 'ì' or 'î' or 'ï' => 'i',
        'ó' or 'ò' or 'õ' or 'ô' or 'ö' => 'o',
        'ú' or 'ù' or 'û' or 'ü' => 'u',
        'ç' => 'c',
        _ => caractere,
    };

    private static string SomenteDigitos(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return string.Empty;
        }

        var destino = new char[bruto.Length];
        var tamanho = 0;

        foreach (var caractere in bruto)
        {
            if (caractere is >= '0' and <= '9')
            {
                destino[tamanho++] = caractere;
            }
        }

        return new string(destino, 0, tamanho);
    }
}
