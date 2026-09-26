using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CoberturaDeFornecedorNoScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cobertura_suficiente",
                table: "scores_risco",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "despesas_com_fornecedor",
                table: "scores_risco",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preenche as linhas que ja existem, com a mesma regra do dominio
            // (ScoreRiscoParlamentar.Recalcular, piso de 0,5). Sem isto, todo mundo ficaria
            // com cobertura zero ate a proxima varredura — e fora do ranking por indice. A
            // varredura seguinte recalcula pelo contexto de avaliacao, que e' o numero oficial.
            migrationBuilder.Sql("""
                UPDATE scores_risco AS s
                   SET despesas_com_fornecedor = x.com_fornecedor,
                       cobertura_suficiente = s.despesas_avaliadas > 0
                           AND x.com_fornecedor::numeric / s.despesas_avaliadas >= 0.5
                  FROM (SELECT parlamentar_id,
                               count(*) FILTER (WHERE fornecedor_id IS NOT NULL) AS com_fornecedor
                          FROM despesas
                         GROUP BY parlamentar_id) AS x
                 WHERE x.parlamentar_id = s.parlamentar_id
                   AND x.com_fornecedor <= s.despesas_avaliadas;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cobertura_suficiente",
                table: "scores_risco");

            migrationBuilder.DropColumn(
                name: "despesas_com_fornecedor",
                table: "scores_risco");
        }
    }
}
