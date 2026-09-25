using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EleicoesDoTse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidaturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano_da_eleicao = table.Column<int>(type: "integer", nullable: false),
                    sequencial_no_tse = table.Column<long>(type: "bigint", nullable: false),
                    numero_na_urna = table.Column<int>(type: "integer", nullable: false),
                    nome_de_urna = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cargo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    sigla_partido = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    situacao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    total_de_votos = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidaturas", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidaturas_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "doacoes_eleitorais",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano_da_eleicao = table.Column<int>(type: "integer", nullable: false),
                    sequencial_da_receita = table.Column<long>(type: "bigint", nullable: false),
                    destino = table.Column<int>(type: "integer", nullable: false),
                    sequencial_do_candidato = table.Column<long>(type: "bigint", nullable: true),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome_do_doador = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_do_doador_normalizado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf_mascarado_do_doador = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    nome_do_destinatario = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sigla_partido_do_destinatario = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    uf_do_destinatario = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    cargo_do_destinatario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doacoes_eleitorais", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "votos_por_municipio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_do_municipio_no_tse = table.Column<int>(type: "integer", nullable: false),
                    nome_do_municipio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    votos = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_votos_por_municipio", x => x.id);
                    table.ForeignKey(
                        name: "fk_votos_por_municipio_candidaturas_candidatura_id",
                        column: x => x.candidatura_id,
                        principalTable: "candidaturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidaturas_ano_sequencial",
                table: "candidaturas",
                columns: new[] { "ano_da_eleicao", "sequencial_no_tse" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_candidaturas_parlamentar_ano",
                table: "candidaturas",
                columns: new[] { "parlamentar_id", "ano_da_eleicao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_doacoes_ano_destino_sequencial",
                table: "doacoes_eleitorais",
                columns: new[] { "ano_da_eleicao", "destino", "sequencial_da_receita" });

            migrationBuilder.CreateIndex(
                name: "ix_doacoes_nome_do_doador",
                table: "doacoes_eleitorais",
                column: "nome_do_doador_normalizado");

            migrationBuilder.CreateIndex(
                name: "ix_doacoes_parlamentar",
                table: "doacoes_eleitorais",
                column: "parlamentar_id");

            migrationBuilder.CreateIndex(
                name: "ix_votos_por_municipio_candidatura_id",
                table: "votos_por_municipio",
                column: "candidatura_id");

            migrationBuilder.CreateIndex(
                name: "ix_votos_por_municipio_parlamentar",
                table: "votos_por_municipio",
                column: "parlamentar_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doacoes_eleitorais");

            migrationBuilder.DropTable(
                name: "votos_por_municipio");

            migrationBuilder.DropTable(
                name: "candidaturas");
        }
    }
}
