using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class QuadroDePessoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lotacoes_do_orgao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<int>(type: "integer", nullable: false),
                    codigo_siafi = table.Column<int>(type: "integer", nullable: false),
                    nome_na_origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo_siorg = table.Column<int>(type: "integer", nullable: true),
                    vinculos = table.Column<int>(type: "integer", nullable: false),
                    com_funcao_de_confianca = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotacoes_do_orgao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contagens_por_funcao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sigla = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    lotacao_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contagens_por_funcao", x => x.id);
                    table.ForeignKey(
                        name: "fk_contagens_por_funcao_lotacoes_do_orgao_lotacao_id",
                        column: x => x.lotacao_id,
                        principalTable: "lotacoes_do_orgao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contagens_por_funcao_lotacao_id",
                table: "contagens_por_funcao",
                column: "lotacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_lotacoes_competencia_siafi",
                table: "lotacoes_do_orgao",
                columns: new[] { "competencia", "codigo_siafi" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lotacoes_siorg_competencia",
                table: "lotacoes_do_orgao",
                columns: new[] { "codigo_siorg", "competencia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contagens_por_funcao");

            migrationBuilder.DropTable(
                name: "lotacoes_do_orgao");
        }
    }
}
