using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DatasDoMei : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "data_exclusao_do_mei",
                table: "fornecedores",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "data_opcao_pelo_mei",
                table: "fornecedores",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_exclusao_do_mei",
                table: "fornecedores");

            migrationBuilder.DropColumn(
                name: "data_opcao_pelo_mei",
                table: "fornecedores");
        }
    }
}
