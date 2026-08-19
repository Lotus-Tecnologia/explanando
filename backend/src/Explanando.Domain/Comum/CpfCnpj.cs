namespace Explanando.Domain.Comum;

/// <summary>Natureza do documento de um fornecedor de despesa parlamentar.</summary>
public enum TipoDocumento
{
    /// <summary>Campo ausente na origem.</summary>
    Vazio = 0,

    /// <summary>Pessoa fisica (11 digitos).</summary>
    Cpf = 1,

    /// <summary>Pessoa juridica (14 digitos).</summary>
    Cnpj = 2,

    /// <summary>Valor sentinela da base da Camara: "00000000000001".</summary>
    Sentinela = 3,

    /// <summary>Preenchido, mas nao e' CPF nem CNPJ valido.</summary>
    Invalido = 4,
}

/// <summary>
/// Documento do fornecedor, vindo do campo <c>cnpjCpfFornecedor</c> da API da Camara.
///
/// Nao e' seguro tratar esse campo como "o CNPJ": a Etapa 01 mostrou que ele carrega
/// CNPJ, CPF de pessoa fisica e o valor sentinela "00000000000001" (2 em 100 despesas
/// na amostra real). Este tipo torna as tres possibilidades explicitas em vez de deixar
/// a decisao para cada ponto de uso.
/// </summary>
public sealed record CpfCnpj
{
    /// <summary>
    /// A Camara usa este valor quando a despesa nao tem fornecedor identificavel.
    /// A BrasilAPI responde 400 para ele.
    /// </summary>
    private const string NumeroSentinela = "00000000000001";

    private CpfCnpj(string numero, TipoDocumento tipo)
    {
        Numero = numero;
        Tipo = tipo;
    }

    /// <summary>Somente digitos, sem pontuacao. Vazio quando a origem nao informou nada.</summary>
    public string Numero { get; }

    public TipoDocumento Tipo { get; }

    public bool EhVazio => Tipo is TipoDocumento.Vazio;

    public bool EhSentinela => Tipo is TipoDocumento.Sentinela;

    public bool EhValido => Tipo is TipoDocumento.Cpf or TipoDocumento.Cnpj;

    /// <summary>
    /// Se vale a pena consultar a BrasilAPI para este documento. Falso para vazio,
    /// sentinela e invalido — consultar seria queimar requisicao e poluir o log de erro.
    /// </summary>
    public bool PodeSerEnriquecido => EhValido;

    public static CpfCnpj Criar(string? bruto)
    {
        var numero = SomenteDigitos(bruto);

        if (numero.Length == 0)
        {
            return new CpfCnpj(string.Empty, TipoDocumento.Vazio);
        }

        if (numero == NumeroSentinela)
        {
            return new CpfCnpj(numero, TipoDocumento.Sentinela);
        }

        var tipo = numero.Length switch
        {
            11 when CpfEhValido(numero) => TipoDocumento.Cpf,
            14 when CnpjEhValido(numero) => TipoDocumento.Cnpj,
            _ => TipoDocumento.Invalido,
        };

        return new CpfCnpj(numero, tipo);
    }

    /// <summary>
    /// Texto para exibicao. CNPJ sai inteiro — e' dado de pessoa juridica e publico.
    /// CPF sai sempre mascarado: e' dado pessoal, e o produto nunca expoe CPF completo.
    /// </summary>
    public string Formatado => Tipo switch
    {
        TipoDocumento.Cnpj =>
            $"{Numero[..2]}.{Numero[2..5]}.{Numero[5..8]}/{Numero[8..12]}-{Numero[12..]}",
        TipoDocumento.Cpf =>
            $"***.{Numero[3..6]}.{Numero[6..9]}-**",
        _ => Numero,
    };

    public override string ToString() => Formatado;

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

    private static bool TodosOsDigitosIguais(string numero)
    {
        for (var i = 1; i < numero.Length; i++)
        {
            if (numero[i] != numero[0])
            {
                return false;
            }
        }

        return true;
    }

    private static bool CpfEhValido(string numero)
    {
        if (TodosOsDigitosIguais(numero))
        {
            return false;
        }

        return DigitoDeCpf(numero, ate: 9) == numero[9] - '0'
            && DigitoDeCpf(numero, ate: 10) == numero[10] - '0';
    }

    private static int DigitoDeCpf(string numero, int ate)
    {
        var soma = 0;
        var peso = ate + 1;

        for (var i = 0; i < ate; i++)
        {
            soma += (numero[i] - '0') * peso--;
        }

        var resto = soma * 10 % 11;
        return resto == 10 ? 0 : resto;
    }

    private static bool CnpjEhValido(string numero)
    {
        if (TodosOsDigitosIguais(numero))
        {
            return false;
        }

        ReadOnlySpan<int> pesosPrimeiro = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        ReadOnlySpan<int> pesosSegundo = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        return DigitoDeCnpj(numero, pesosPrimeiro) == numero[12] - '0'
            && DigitoDeCnpj(numero, pesosSegundo) == numero[13] - '0';
    }

    private static int DigitoDeCnpj(string numero, ReadOnlySpan<int> pesos)
    {
        var soma = 0;

        for (var i = 0; i < pesos.Length; i++)
        {
            soma += (numero[i] - '0') * pesos[i];
        }

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
