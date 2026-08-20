using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DoisNumerosVolumeEIndiceDeRisco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_scores_risco_score_calculado",
                table: "scores_risco");

            migrationBuilder.RenameColumn(
                name: "score_calculado",
                table: "scores_risco",
                newName: "peso_das_flags");

            migrationBuilder.AddColumn<bool>(
                name: "amostra_suficiente",
                table: "scores_risco",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "despesas_avaliadas",
                table: "scores_risco",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "indice_de_risco",
                table: "scores_risco",
                type: "numeric(10,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "volume_total_gasto",
                table: "scores_risco",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_scores_risco_indice_de_risco",
                table: "scores_risco",
                column: "indice_de_risco",
                descending: new bool[0],
                filter: "amostra_suficiente");

            migrationBuilder.CreateIndex(
                name: "ix_scores_risco_volume_total_gasto",
                table: "scores_risco",
                column: "volume_total_gasto",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_scores_risco_indice_de_risco",
                table: "scores_risco");

            migrationBuilder.DropIndex(
                name: "ix_scores_risco_volume_total_gasto",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "amostra_suficiente",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "despesas_avaliadas",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "indice_de_risco",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "volume_total_gasto",
                table: "scores_risco");

            migrationBuilder.RenameColumn(
                name: "peso_das_flags",
                table: "scores_risco",
                newName: "score_calculado");

            migrationBuilder.CreateIndex(
                name: "ix_scores_risco_score_calculado",
                table: "scores_risco",
                column: "score_calculado",
                descending: new bool[0]);
        }
    }
}
