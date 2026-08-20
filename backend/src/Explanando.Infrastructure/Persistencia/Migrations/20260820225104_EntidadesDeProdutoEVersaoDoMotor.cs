using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EntidadesDeProdutoEVersaoDoMotor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "versao_do_motor",
                table: "flags_anomalias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "contestacoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    despesa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flag_anomalia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_do_gabinete = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    texto = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    url_do_anexo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    enviada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retirada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contestacoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_contestacoes_despesas_despesa_id",
                        column: x => x.despesa_id,
                        principalTable: "despesas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_contestacoes_flags_anomalias_flag_anomalia_id",
                        column: x => x.flag_anomalia_id,
                        principalTable: "flags_anomalias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_contestacoes_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "historico_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<DateOnly>(type: "date", nullable: false),
                    volume_total_gasto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    indice_de_risco = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    despesas_avaliadas = table.Column<int>(type: "integer", nullable: false),
                    total_flags_vermelhas = table.Column<int>(type: "integer", nullable: false),
                    versao_do_motor = table.Column<int>(type: "integer", nullable: false),
                    capturado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historico_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_historico_scores_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    hash_senha = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    perfil = table.Column<int>(type: "integer", nullable: false),
                    parlamentar_vinculado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_parlamentares_parlamentar_vinculado_id",
                        column: x => x.parlamentar_vinculado_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "favoritos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_criacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_notificacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favoritos", x => x.id);
                    table.ForeignKey(
                        name: "fk_favoritos_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_favoritos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "solicitacoes_varredura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parlamentar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_solicitacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    situacao = table.Column<int>(type: "integer", nullable: false),
                    payment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valor_pago = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    paga_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_da_falha = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_varredura", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitacoes_varredura_parlamentares_parlamentar_id",
                        column: x => x.parlamentar_id,
                        principalTable: "parlamentares",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_solicitacoes_varredura_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contestacoes_despesa_id",
                table: "contestacoes",
                column: "despesa_id");

            migrationBuilder.CreateIndex(
                name: "ix_contestacoes_flag_anomalia_id",
                table: "contestacoes",
                column: "flag_anomalia_id");

            migrationBuilder.CreateIndex(
                name: "ix_contestacoes_parlamentar_id",
                table: "contestacoes",
                column: "parlamentar_id");

            migrationBuilder.CreateIndex(
                name: "ix_favoritos_parlamentar_id",
                table: "favoritos",
                column: "parlamentar_id");

            migrationBuilder.CreateIndex(
                name: "ix_favoritos_usuario_parlamentar",
                table: "favoritos",
                columns: new[] { "usuario_id", "parlamentar_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_historico_scores_parlamentar_referencia",
                table: "historico_scores",
                columns: new[] { "parlamentar_id", "referencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_varredura_parlamentar_concluida",
                table: "solicitacoes_varredura",
                columns: new[] { "parlamentar_id", "concluida_em" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_varredura_payment_id",
                table: "solicitacoes_varredura",
                column: "payment_id",
                unique: true,
                filter: "payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_varredura_situacao",
                table: "solicitacoes_varredura",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_varredura_usuario_id",
                table: "solicitacoes_varredura",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_parlamentar_vinculado_id",
                table: "usuarios",
                column: "parlamentar_vinculado_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contestacoes");

            migrationBuilder.DropTable(
                name: "favoritos");

            migrationBuilder.DropTable(
                name: "historico_scores");

            migrationBuilder.DropTable(
                name: "solicitacoes_varredura");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropColumn(
                name: "versao_do_motor",
                table: "flags_anomalias");
        }
    }
}
