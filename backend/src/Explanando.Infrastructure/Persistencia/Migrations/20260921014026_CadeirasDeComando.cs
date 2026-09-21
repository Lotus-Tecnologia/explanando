using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CadeirasDeComando : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cadeiras_de_comando",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coletor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    poder = table.Column<int>(type: "integer", nullable: false),
                    camada = table.Column<int>(type: "integer", nullable: false),
                    cargo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ocupante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    orgao = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    codigo_siorg = table.Column<int>(type: "integer", nullable: true),
                    partido = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    foto_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fonte = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origem = table.Column<int>(type: "integer", nullable: false),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cadeiras_de_comando", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cadeiras_coletor",
                table: "cadeiras_de_comando",
                column: "coletor");

            migrationBuilder.CreateIndex(
                name: "ix_cadeiras_poder_camada",
                table: "cadeiras_de_comando",
                columns: new[] { "poder", "camada" });

            migrationBuilder.CreateIndex(
                name: "ix_cadeiras_siorg",
                table: "cadeiras_de_comando",
                column: "codigo_siorg");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cadeiras_de_comando");
        }
    }
}
