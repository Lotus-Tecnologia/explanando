using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// O CPF do fornecedor pessoa física deixa de ser gravado inteiro (decisão de 26/09/2026).
    ///
    /// <para>A Câmara publica o CPF do fornecedor PF inteiro, e em 25/09 havia 325 deles em
    /// 5.748 despesas — no banco e no backup do R2. O domínio passou a guardar a máscara que a
    /// Receita usa no quadro societário (<c>***NNNNNN**</c>); esta migração leva o que já
    /// estava gravado para a mesma forma.</para>
    ///
    /// <para><b>O hash de deduplicação muda junto</b>, porque o documento entra nele
    /// (Despesa.CalcularHash). Sem recalcular, a próxima coleta não reconheceria essas despesas
    /// e as gravaria de novo. A fórmula em SQL abaixo foi conferida contra o hash gravado pelo
    /// C# em todas as 790.303 despesas da base local antes de virar migração — 790.303 iguais —,
    /// e com a máscara os hashes continuam todos distintos, e as 325 máscaras também.</para>
    ///
    /// <para>Só CPF válido (a mesma regra de CpfCnpj.CpfEhValido): onze dígitos que não passam
    /// no dígito verificador são documento inválido, não CPF, e o domínio os guarda como estão.
    /// Sem volta: o número mascarado não se recupera, e é esse o ponto.</para>
    /// </summary>
    public partial class MascararCpfDoFornecedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION pg_temp.cpf_valido(c text) RETURNS boolean
                LANGUAGE sql IMMUTABLE AS $$
                  SELECT c ~ '^[0-9]{11}$'
                     AND c !~ '^(.)\1{10}$'
                     AND (SELECT CASE WHEN (sum(substr(c, i, 1)::int * (11 - i)) * 10) % 11 = 10 THEN 0
                                      ELSE (sum(substr(c, i, 1)::int * (11 - i)) * 10) % 11 END
                            FROM generate_series(1, 9) i) = substr(c, 10, 1)::int
                     AND (SELECT CASE WHEN (sum(substr(c, i, 1)::int * (12 - i)) * 10) % 11 = 10 THEN 0
                                      ELSE (sum(substr(c, i, 1)::int * (12 - i)) * 10) % 11 END
                            FROM generate_series(1, 10) i) = substr(c, 11, 1)::int
                $$;

                UPDATE despesas
                   SET documento_fornecedor = '***' || substr(documento_fornecedor, 4, 6) || '**'
                 WHERE pg_temp.cpf_valido(documento_fornecedor);

                UPDATE despesas
                   SET hash_unico = substring(sha256(convert_to(concat_ws('|',
                           replace(parlamentar_id::text, '-', ''),
                           documento_fornecedor,
                           codigo_documento,
                           numero_documento,
                           lpad((competencia / 100)::text, 4, '0') || '-' || lpad((competencia % 100)::text, 2, '0'),
                           to_char(data_documento AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS') || '.'
                             || lpad(((extract(microseconds FROM data_documento)::bigint % 1000000) * 10)::text, 7, '0')
                             || 'Z',
                           valor_liquido::text,
                           codigo_lote::text,
                           parcela::text), 'UTF8')) FROM 1 FOR 16)
                 WHERE documento_fornecedor ~ '^\*{3}[0-9]{6}\*{2}$';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
