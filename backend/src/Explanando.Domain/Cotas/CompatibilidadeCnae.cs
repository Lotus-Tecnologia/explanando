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
    /// Chave normalizada (sem acento, minuscula, sem pontuacao final) -> prefixos de CNAE
    /// aceitos. Ver <see cref="Normalizar"/>.
    /// </summary>
    private static readonly Dictionary<string, string[]> Tabela = new(StringComparer.Ordinal)
    {
        // 1 — aluguel, energia, agua, limpeza, telecom, reforma e material de construcao.
        // Deliberadamente amplo: "manutencao de escritorio" cobre quase tudo que um
        // imovel consome.
        ["manutencao de escritorio de apoio a atividade parlamentar"] =
            ["68", "35", "36", "37", "38", "81", "61", "43", "41", "474", "475", "477"],

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

        // 122 — taxi, aplicativos, estacionamento e concessionarias de rodovia.
        ["servico de taxi, pedagio e estacionamento"] = ["492", "522"],

        // 123 — rodoviario interestadual, aquaviario e agencias.
        ["passagens terrestres, maritimas ou fluviais"] = ["492", "50", "79"],

        // 137 — educacao, organizacao de eventos e associacoes profissionais.
        ["participacao em curso, palestra ou evento similar"] = ["85", "823", "94"],

        // 145 — certificacao digital, TI e varejo de informatica.
        ["aquisicao de tokens e certificados digitais"] = ["62", "63", "475", "8299"],
    };

    /// <summary>Os tipos de despesa que a tabela cobre, em forma normalizada.</summary>
    public static IReadOnlyCollection<string> TiposCobertos => Tabela.Keys;

    public static IReadOnlyList<string> PrefixosPara(string tipoDespesa) =>
        Tabela.TryGetValue(Normalizar(tipoDespesa), out var prefixos) ? prefixos : [];

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
