using Explanando.Domain.Comum;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Explanando.Infrastructure.Persistencia;

/// <summary>
/// Conversores dos objetos de valor do dominio para colunas do Postgres.
///
/// Todos guardam a forma canonica e reconstroem pela fabrica do proprio tipo. Assim as
/// invariantes do dominio (validacao de digito verificador, deteccao do sentinela, extracao
/// dos 6 digitos do CPF parcial) valem tambem para dado que vem do banco, e nao apenas para
/// dado que vem da API.
/// </summary>
internal static class Conversores
{
    /// <summary>Guarda so' os digitos; o tipo e a validade sao rederivados na leitura.</summary>
    public static readonly ValueConverter<CpfCnpj, string> CpfCnpj =
        new(documento => documento.Numero, texto => Domain.Comum.CpfCnpj.Criar(texto));

    /// <summary>
    /// Guarda a mascara como a fonte a entregou ("***456789**"). Nao ha o que ocultar
    /// aqui: os 5 digitos que faltam nunca chegaram ao sistema.
    /// </summary>
    public static readonly ValueConverter<CpfParcial, string> CpfParcial =
        new(cpf => cpf.Mascarado, texto => Domain.Comum.CpfParcial.Criar(texto));

    /// <summary>
    /// Competencia vira um inteiro AAAAMM.
    ///
    /// A codificacao preserva a ordem cronologica, entao comparacao e ordenacao continuam
    /// funcionando no SQL e o indice serve tanto a filtro por mes quanto a intervalo — que
    /// e' o que REG_TX_001 (fracionamento no mes) e REG_TX_004 (spike contra a serie
    /// historica) precisam.
    /// </summary>
    public static readonly ValueConverter<Competencia, int> Competencia =
        new(
            competencia => (competencia.Ano * 100) + competencia.Mes,
            codificado => Domain.Comum.Competencia.Criar(codificado / 100, codificado % 100));
}
