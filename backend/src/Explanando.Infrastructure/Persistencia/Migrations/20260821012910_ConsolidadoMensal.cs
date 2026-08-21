using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidadoMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consolidados_mensais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<int>(type: "integer", nullable: false),
                    total_liquido = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_documento = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_glosa = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    quantidade_de_despesas = table.Column<int>(type: "integer", nullable: false),
                    quantidade_de_estornos = table.Column<int>(type: "integer", nullable: false),
                    detalhe_disponivel = table.Column<bool>(type: "boolean", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consolidados_mensais", x => x.id);
                    table.ForeignKey(
                        name: "fk_consolidados_mensais_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consolidados_mensais_parlamentar_competencia",
                table: "consolidados_mensais",
                columns: new[] { "parlamentar_id", "competencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consolidados_mensais");
        }
    }
}
