using System.Text;

namespace Explanando.Domain.Comum;

/// <summary>
/// Guarda a fronteira linguistica do ADR-0004.
///
/// O produto reporta achados de um motor de regras e deixa a conclusao para o leitor ou
/// para a autoridade competente. Ele nunca imputa conduta. Deixar essa regra apenas num
/// documento nao funciona: basta alguem escrever "indicio de fraude" numa descricao de
/// flag para que a blindagem juridica do produto inteiro seja perdida naquele texto.
///
/// Aqui a regra e' executavel, e o dominio recusa emitir texto que a viole.
/// </summary>
public static class VocabularioDeCompliance
{
    /// <summary>
    /// Termos proibidos pelo documento de Requisitos de Negocio, §1. Comparados sem
    /// acento e em minusculas, e sempre como palavra inteira.
    /// </summary>
    private static readonly string[] TermosProibidos =
    [
        "fraude", "fraudulenta", "fraudulento",
        "crime", "crimes", "criminoso", "criminosa",
        "corrupcao", "corrupto", "corrupta",
        "lavagem",
        "desvio", "desviou", "desviado",
        "roubo", "roubou",
        "propina",
        "caixa dois",
    ];

    /// <summary>
    /// Termos que o mesmo documento define como obrigatorios. Ficam aqui como referencia
    /// viva para quem for escrever descricao de regra nova.
    /// </summary>
    public static IReadOnlyList<string> TermosPreferidos { get; } =
    [
        "Inconsistência Financeira",
        "Inconsistência com a Regra da Câmara",
        "Atipicidade de Volume Financeiro",
        "Fator de Risco Elevado",
        "Alerta de Compliance",
        "Desvio Padrão",
    ];

    /// <summary>
    /// Expressoes que contem um termo proibido mas sao <b>obrigatorias</b> pelo mesmo
    /// documento de Negocios.
    ///
    /// "Desvio Padrao" e' o caso: "desvio" esta na lista de proibidos por imputar conduta,
    /// mas "desvio padrao" e' termo estatistico e consta entre os termos preferidos. Sem
    /// esta excecao, o proprio vocabulario recomendado seria recusado pelo dominio.
    /// </summary>
    private static readonly string[] ExpressoesPermitidas = ["desvio padrao"];

    /// <summary>
    /// Se o texto viola o ADR-0004.
    ///
    /// <para><b>Trecho entre aspas simples nao e' examinado</b>, porque nao e' vocabulario
    /// nosso: e' dado da origem interpolado na frase — razao social, tipo de despesa. A
    /// regra existe para conter o que o PRODUTO afirma, e o nome de uma empresa que existe
    /// no mundo nao e' escolha de palavra nossa.</para>
    ///
    /// <para>Descoberto rodando contra os 513 deputados: tres lava-jatos — "MOREIRA LAVAGEM
    /// E POLIMENTOS LTDA", "ESQUILOS LAVAGEM AUTOMOTIVA LTDA" e "RODO SERVICE LAVAGEM E
    /// CONSERVACAO LTDA" — derrubaram REG_KYB_001 seis vezes, porque "lavagem" esta na lista
    /// de proibidos por causa de <i>lavagem de dinheiro</i>. Seis achados legitimos foram
    /// perdidos por causa do ramo de atividade do fornecedor.</para>
    ///
    /// <para>O preco disso e' que alguem poderia evadir o filtro pondo o termo entre aspas.
    /// E' aceitavel: isto e' guarda contra <b>descuido</b> de redacao, nao barreira de
    /// seguranca, e quotar "fraude" de proposito seria violacao consciente do ADR — coisa
    /// que revisao de codigo pega e que filtro nenhum impediria.</para>
    /// </summary>
    public static bool ContemTermoProibido(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var normalizado = Normalizar(RemoverCitacoes(texto));

        // Remove as expressoes tecnicas permitidas antes de procurar os termos proibidos,
        // para que o "desvio" de "desvio padrao" nao seja contado.
        foreach (var permitida in ExpressoesPermitidas)
        {
            normalizado = normalizado.Replace(permitida, " ", StringComparison.Ordinal);
        }

        foreach (var proibido in TermosProibidos)
        {
            if (ContemPalavraInteira(normalizado, proibido))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lanca se o texto violar o ADR-0004. Usado nas fronteiras do dominio.</summary>
    public static void GarantirVocabularioTecnico(string? texto, string nomeDoParametro)
    {
        if (ContemTermoProibido(texto))
        {
            throw new ArgumentException(
                "Texto usa vocabulario acusatorio, proibido pelo ADR-0004. O produto reporta "
                + "inconsistencias de um motor de regras e nao imputa conduta. Use termos como "
                + $"'{string.Join("', '", TermosPreferidos)}'. Texto recebido: '{texto}'.",
                nomeDoParametro);
        }
    }

    /// <summary>
    /// Troca por espaco tudo que estiver entre aspas simples. Ver a nota de
    /// <see cref="ContemTermoProibido"/>.
    ///
    /// Aspa sem par fecha no fim do texto: um trecho citado pela metade continua sendo dado
    /// da origem, e deixar o resto exposto reintroduziria o problema pela borda.
    /// </summary>
    private static string RemoverCitacoes(string texto)
    {
        if (!texto.Contains('\'', StringComparison.Ordinal))
        {
            return texto;
        }

        var construtor = new StringBuilder(texto.Length);
        var dentro = false;

        foreach (var caractere in texto)
        {
            if (caractere == '\'')
            {
                dentro = !dentro;
                construtor.Append(' ');
                continue;
            }

            construtor.Append(dentro ? ' ' : caractere);
        }

        return construtor.ToString();
    }

    /// <summary>
    /// Casar por substring cega inviabilizaria a regra: "descricao" contem "cri",
    /// "discriminado" contem "crim". A comparacao e' por palavra inteira.
    /// </summary>
    private static bool ContemPalavraInteira(string textoNormalizado, string termo)
    {
        var indice = textoNormalizado.IndexOf(termo, StringComparison.Ordinal);

        while (indice >= 0)
        {
            var comecaPalavra = indice == 0 || !char.IsLetter(textoNormalizado[indice - 1]);
            var fim = indice + termo.Length;
            var terminaPalavra = fim >= textoNormalizado.Length
                || !char.IsLetter(textoNormalizado[fim]);

            if (comecaPalavra && terminaPalavra)
            {
                return true;
            }

            indice = textoNormalizado.IndexOf(termo, indice + 1, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// Minusculas e sem diacriticos: "Corrupção" e "corrupcao" tem de casar igual.
    ///
    /// A dobra e' feita por tabela explicita, e nao por <c>Normalize(FormD)</c>, porque a
    /// solution roda com <c>InvariantGlobalization=true</c> — e nesse modo a normalizacao
    /// Unicode e' silenciosamente um no-op ("corrupção" continua com 9 caracteres apos a
    /// decomposicao). Verificado na Etapa 02.
    ///
    /// A tabela tambem e' o comportamento que se quer num motor de regras: identica em
    /// qualquer maquina, sem depender da versao do ICU instalada.
    /// </summary>
    private static string Normalizar(string texto)
    {
        var construtor = new StringBuilder(texto.Length);

        foreach (var caractere in texto.ToLowerInvariant())
        {
            construtor.Append(SemAcento(caractere));
        }

        return construtor.ToString();
    }

    private static char SemAcento(char caractere) => caractere switch
    {
        'á' or 'à' or 'ã' or 'â' or 'ä' => 'a',
        'é' or 'è' or 'ê' or 'ë' => 'e',
        'í' or 'ì' or 'î' or 'ï' => 'i',
        'ó' or 'ò' or 'õ' or 'ô' or 'ö' => 'o',
        'ú' or 'ù' or 'û' or 'ü' => 'u',
        'ç' => 'c',
        'ñ' => 'n',
        _ => caractere,
    };
}
