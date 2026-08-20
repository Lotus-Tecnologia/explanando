using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ComposicaoDoIndicePorGravidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "peso_flags_altas",
                table: "scores_risco",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_flags_baixas",
                table: "scores_risco",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "peso_flags_medias",
                table: "scores_risco",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "total_flags_baixas",
                table: "scores_risco",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "total_flags_medias",
                table: "scores_risco",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "peso_flags_altas",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "peso_flags_baixas",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "peso_flags_medias",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "total_flags_baixas",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "total_flags_medias",
                table: "scores_risco");
        }
    }
}
