using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TipoDeDocumentoNaDespesa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "codigo_tipo_documento",
                table: "despesas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_tipo_documento",
                table: "despesas");
        }
    }
}
