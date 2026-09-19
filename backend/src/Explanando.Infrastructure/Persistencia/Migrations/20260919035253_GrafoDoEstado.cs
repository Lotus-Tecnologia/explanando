using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class GrafoDoEstado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mudancas_estruturais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_unidade = table.Column<int>(type: "integer", nullable: false),
                    nome_na_epoca = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    campo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    de = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    para = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    detectada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mudancas_estruturais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidades_do_estado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<int>(type: "integer", nullable: false),
                    codigo_pai = table.Column<int>(type: "integer", nullable: true),
                    codigo_orgao = table.Column<int>(type: "integer", nullable: true),
                    nome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sigla = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    poder = table.Column<int>(type: "integer", nullable: true),
                    esfera = table.Column<int>(type: "integer", nullable: true),
                    natureza_juridica = table.Column<int>(type: "integer", nullable: true),
                    normatizacao = table.Column<int>(type: "integer", nullable: true),
                    primeira_coleta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_coleta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unidades_do_estado", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mudancas_estruturais_detectada_em",
                table: "mudancas_estruturais",
                column: "detectada_em",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_mudancas_estruturais_unidade",
                table: "mudancas_estruturais",
                column: "codigo_unidade");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_do_estado_codigo",
                table: "unidades_do_estado",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unidades_do_estado_pai",
                table: "unidades_do_estado",
                column: "codigo_pai");

            migrationBuilder.CreateIndex(
                name: "ix_unidades_do_estado_poder_ativa",
                table: "unidades_do_estado",
                columns: new[] { "poder", "ativa" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mudancas_estruturais");

            migrationBuilder.DropTable(
                name: "unidades_do_estado");
        }
    }
}
