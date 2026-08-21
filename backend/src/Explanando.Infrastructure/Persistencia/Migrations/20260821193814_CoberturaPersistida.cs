using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CoberturaPersistida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "coberturas_regra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_regra = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    despesas_com_dado = table.Column<int>(type: "integer", nullable: false),
                    despesas_sem_dado = table.Column<int>(type: "integer", nullable: false),
                    flags_emitidas = table.Column<int>(type: "integer", nullable: false),
                    dado_ausente = table.Column<int>(type: "integer", nullable: false),
                    motivo_da_falha = table.Column<string>(type: "text", nullable: true),
                    versao_do_motor = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coberturas_regra", x => x.id);
                    table.ForeignKey(
                        name: "fk_coberturas_regra_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_coberturas_regra_parlamentar_regra",
                table: "coberturas_regra",
                columns: new[] { "parlamentar_id", "codigo_regra" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coberturas_regra");
        }
    }
}
