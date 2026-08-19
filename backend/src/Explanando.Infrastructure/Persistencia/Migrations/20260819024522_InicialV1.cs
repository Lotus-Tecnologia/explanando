using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InicialV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    razao_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cnae_principal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cnaes_secundarios = table.Column<string[]>(type: "text[]", nullable: false),
                    situacao_cadastral = table.Column<int>(type: "integer", nullable: false),
                    data_situacao_cadastral = table.Column<DateOnly>(type: "date", nullable: true),
                    data_inicio_atividade = table.Column<DateOnly>(type: "date", nullable: true),
                    optante_pelo_mei = table.Column<bool>(type: "boolean", nullable: true),
                    porte = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_codigo_municipio_ibge = table.Column<int>(type: "integer", nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fornecedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "limites_cota",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uf = table.Column<string>(type: "char(2)", nullable: false),
                    valor_mensal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_limites_cota", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parlamentares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_externo = table.Column<int>(type: "integer", nullable: false),
                    nome_civil = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nome_parlamentar = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sigla_partido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uf = table.Column<string>(type: "char(2)", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    foto_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parlamentares", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "despesas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_fornecedor = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    nome_fornecedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    competencia = table.Column<int>(type: "integer", nullable: false),
                    tipo_despesa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    codigo_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    numero_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_documento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valor_documento = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_liquido = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_glosa = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    url_documento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    codigo_lote = table.Column<long>(type: "bigint", nullable: false),
                    parcela = table.Column<int>(type: "integer", nullable: false),
                    hash_unico = table.Column<string>(type: "char(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_despesas", x => x.id);
                    table.ForeignKey(
                        name: "fk_despesas_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sociedades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_socio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cpf_mascarado = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    qualificacao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_entrada_sociedade = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sociedades", x => x.id);
                    table.ForeignKey(
                        name: "fk_sociedades_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mandatos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legislatura = table.Column<int>(type: "integer", nullable: false),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    data_fim = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mandatos", x => x.id);
                    table.ForeignKey(
                        name: "fk_mandatos_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scores_risco",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score_calculado = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    total_flags_vermelhas = table.Column<int>(type: "integer", nullable: false),
                    data_ultima_varredura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scores_risco", x => x.id);
                    table.ForeignKey(
                        name: "fk_scores_risco_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flags_anomalias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    despesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_regra = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    gravidade = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    grau_de_confianca = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    data_identificacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flags_anomalias", x => x.id);
                    table.ForeignKey(
                        name: "fk_flags_anomalias_despesas_despesa_id",
                        column: x => x.despesa_id,
                        principalTable: "despesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_despesas_data_documento",
                table: "despesas",
                column: "data_documento");

            migrationBuilder.CreateIndex(
                name: "ix_despesas_fornecedor_id",
                table: "despesas",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_despesas_hash_unico",
                table: "despesas",
                column: "hash_unico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_despesas_parlamentar_competencia_fornecedor",
                table: "despesas",
                columns: new[] { "parlamentar_id", "competencia", "documento_fornecedor" });

            migrationBuilder.CreateIndex(
                name: "ix_despesas_parlamentar_id",
                table: "despesas",
                column: "parlamentar_id");

            migrationBuilder.CreateIndex(
                name: "ix_flags_anomalias_codigo_regra",
                table: "flags_anomalias",
                column: "codigo_regra");

            migrationBuilder.CreateIndex(
                name: "ix_flags_anomalias_despesa_id",
                table: "flags_anomalias",
                column: "despesa_id");

            migrationBuilder.CreateIndex(
                name: "ix_flags_anomalias_despesa_regra",
                table: "flags_anomalias",
                columns: new[] { "despesa_id", "codigo_regra" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fornecedores_documento",
                table: "fornecedores",
                column: "documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fornecedores_endereco_uf",
                table: "fornecedores",
                column: "endereco_uf");

            migrationBuilder.CreateIndex(
                name: "ix_limites_cota_uf_vigencia",
                table: "limites_cota",
                columns: new[] { "uf", "vigencia_inicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mandatos_parlamentar_legislatura",
                table: "mandatos",
                columns: new[] { "parlamentar_id", "legislatura" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parlamentares_id_externo_tipo",
                table: "parlamentares",
                columns: new[] { "id_externo", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parlamentares_nome_parlamentar",
                table: "parlamentares",
                column: "nome_parlamentar");

            migrationBuilder.CreateIndex(
                name: "ix_scores_risco_parlamentar_id",
                table: "scores_risco",
                column: "parlamentar_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scores_risco_score_calculado",
                table: "scores_risco",
                column: "score_calculado",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_sociedades_fornecedor_id",
                table: "sociedades",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_sociedades_nome_socio",
                table: "sociedades",
                column: "nome_socio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flags_anomalias");

            migrationBuilder.DropTable(
                name: "limites_cota");

            migrationBuilder.DropTable(
                name: "mandatos");

            migrationBuilder.DropTable(
                name: "scores_risco");

            migrationBuilder.DropTable(
                name: "sociedades");

            migrationBuilder.DropTable(
                name: "despesas");

            migrationBuilder.DropTable(
                name: "parlamentares");

            migrationBuilder.DropTable(
                name: "fornecedores");
        }
    }
}
