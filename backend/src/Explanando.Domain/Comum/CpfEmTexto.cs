using System.Text.RegularExpressions;

namespace Explanando.Domain.Comum;

/// <summary>
/// Mascara CPF que venha escrito DENTRO de um nome.
///
/// <para>A Receita grava a razão social do microempreendedor individual como
/// <c>"NOME DO DONO 12345678901"</c> — o CPF inteiro do empresário no fim do nome —, e a
/// Câmara repete esse nome na despesa. Medido na base em 2026-09-25: <b>641 fornecedores e
/// 2.885 despesas</b>, com o número indo para a tela, para a descrição dos achados e para o
/// RSS. O produto nunca exibe nem grava CPF completo, então o nome entra no banco já
/// mascarado, com a mesma máscara de <see cref="CpfCnpj.Formatado"/>.</para>
///
/// <para>Só sequências de exatamente 11 dígitos (ou no formato 000.000.000-00), sem dígito
/// colado antes ou depois: um CNPJ de 14 dígitos contém 11 seguidos, e mascará-lo inventaria
/// um CPF que não existe. Letra, espaço ou hífen do lado não impedem — "NOME 12345678901-ME"
/// é um MEI real da base.</para>
/// </summary>
public static partial class CpfEmTexto
{
    public static string Mascarar(string texto) =>
        string.IsNullOrEmpty(texto) ? texto : Cpf().Replace(texto, "***.$2.$3-**");

    [GeneratedRegex(
        @"(?<![0-9])([0-9]{3})\.?([0-9]{3})\.?([0-9]{3})-?([0-9]{2})(?![0-9])",
        RegexOptions.CultureInvariant)]
    private static partial Regex Cpf();
}
