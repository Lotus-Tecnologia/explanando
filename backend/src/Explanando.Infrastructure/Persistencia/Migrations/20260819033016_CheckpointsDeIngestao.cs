using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CheckpointsDeIngestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checkpoints_ingestao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_externo_parlamentar = table.Column<int>(type: "integer", nullable: false),
                    legislatura = table.Column<int>(type: "integer", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    despesas_importadas = table.Column<int>(type: "integer", nullable: false),
                    despesas_rejeitadas = table.Column<int>(type: "integer", nullable: false),
                    com_alerta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_checkpoints_ingestao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_checkpoints_ingestao_com_alerta",
                table: "checkpoints_ingestao",
                column: "com_alerta");

            migrationBuilder.CreateIndex(
                name: "ix_checkpoints_ingestao_parlamentar_legislatura",
                table: "checkpoints_ingestao",
                columns: new[] { "id_externo_parlamentar", "legislatura" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkpoints_ingestao");
        }
    }
}
