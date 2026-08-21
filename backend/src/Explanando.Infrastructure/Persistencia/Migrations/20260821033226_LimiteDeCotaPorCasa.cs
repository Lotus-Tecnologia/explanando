using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LimiteDeCotaPorCasa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_limites_cota_uf_vigencia",
                table: "limites_cota");

            migrationBuilder.AddColumn<int>(
                name: "casa",
                table: "limites_cota",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_limites_cota_casa_uf_vigencia",
                table: "limites_cota",
                columns: new[] { "casa", "uf", "vigencia_inicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_limites_cota_casa_uf_vigencia",
                table: "limites_cota");

            migrationBuilder.DropColumn(
                name: "casa",
                table: "limites_cota");

            migrationBuilder.CreateIndex(
                name: "ix_limites_cota_uf_vigencia",
                table: "limites_cota",
                columns: new[] { "uf", "vigencia_inicio" },
                unique: true);
        }
    }
}
