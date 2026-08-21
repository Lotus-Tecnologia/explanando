using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ArmazenamentoHashEUrlDerivada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "url_documento",
                table: "despesas");

            migrationBuilder.AddColumn<int>(
                name: "numero_carteira",
                table: "parlamentares",
                type: "integer",
                nullable: true);

            // Descarta e recria em vez de ALTER TYPE. Postgres nao tem cast de char para
            // bytea, entao o ALTER que o EF gerou falharia mesmo com a tabela vazia. O
            // conteudo antigo nao se perde de fato: o hash e' derivado da propria despesa,
            // e a coleta seguinte o recalcula.
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_despesas_hash_unico;");
            migrationBuilder.Sql("ALTER TABLE despesas DROP COLUMN hash_unico;");
            migrationBuilder.Sql(
                "ALTER TABLE despesas ADD COLUMN hash_unico bytea NOT NULL DEFAULT ''::bytea;");
            migrationBuilder.Sql("ALTER TABLE despesas ALTER COLUMN hash_unico DROP DEFAULT;");

            // Recria o indice que o DROP COLUMN levou junto. Ele e' a garantia de
            // idempotencia da ingestao: sem ele, reprocessar um mes duplica o mes inteiro.
            migrationBuilder.CreateIndex(
                name: "ix_despesas_hash_unico",
                table: "despesas",
                column: "hash_unico",
                unique: true);

            migrationBuilder.AddColumn<short>(
                name: "ano_do_arquivo",
                table: "despesas",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "tem_documento_publicado",
                table: "despesas",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "numero_carteira",
                table: "parlamentares");

            migrationBuilder.DropColumn(
                name: "ano_do_arquivo",
                table: "despesas");

            migrationBuilder.DropColumn(
                name: "tem_documento_publicado",
                table: "despesas");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_despesas_hash_unico;");
            migrationBuilder.Sql("ALTER TABLE despesas DROP COLUMN hash_unico;");
            migrationBuilder.Sql(
                "ALTER TABLE despesas ADD COLUMN hash_unico char(64) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE despesas ALTER COLUMN hash_unico DROP DEFAULT;");

            // Recria o indice que o DROP COLUMN levou junto. Ele e' a garantia de
            // idempotencia da ingestao: sem ele, reprocessar um mes duplica o mes inteiro.
            migrationBuilder.CreateIndex(
                name: "ix_despesas_hash_unico",
                table: "despesas",
                column: "hash_unico",
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "url_documento",
                table: "despesas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
