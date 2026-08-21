using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TipoDeDespesaCatalogado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "codigo_tipo_despesa",
                table: "despesas",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "tipo_despesa_nao_catalogado",
                table: "despesas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Converte o que ja esta no banco em vez de descartar. Os dezesseis casos
            // abaixo sao um retrato do CatalogoDeTiposDeDespesa neste commit — migration e'
            // fotografia, nao referencia viva, e por isso a duplicacao aqui e' correta.
            // Tipo que nao casar com nenhum preserva o nome por extenso, exatamente como o
            // codigo faz em tempo de execucao.
            migrationBuilder.Sql("""
                UPDATE despesas SET codigo_tipo_despesa = CASE tipo_despesa
                    WHEN 'COMBUSTÍVEIS E LUBRIFICANTES.' THEN 1
                    WHEN 'PASSAGEM AÉREA - SIGEPA' THEN 2
                    WHEN 'SERVIÇO DE TÁXI, PEDÁGIO E ESTACIONAMENTO' THEN 3
                    WHEN 'MANUTENÇÃO DE ESCRITÓRIO DE APOIO À ATIVIDADE PARLAMENTAR' THEN 4
                    WHEN 'DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.' THEN 5
                    WHEN 'FORNECIMENTO DE ALIMENTAÇÃO DO PARLAMENTAR' THEN 6
                    WHEN 'LOCAÇÃO OU FRETAMENTO DE VEÍCULOS AUTOMOTORES' THEN 7
                    WHEN 'TELEFONIA' THEN 8
                    WHEN 'HOSPEDAGEM ,EXCETO DO PARLAMENTAR NO DISTRITO FEDERAL.' THEN 9
                    WHEN 'ASSINATURA DE PUBLICAÇÕES' THEN 10
                    WHEN 'PASSAGEM AÉREA - RPA' THEN 11
                    WHEN 'LOCAÇÃO OU FRETAMENTO DE AERONAVES' THEN 12
                    WHEN 'PASSAGEM AÉREA - REEMBOLSO' THEN 13
                    WHEN 'PASSAGENS TERRESTRES, MARÍTIMAS OU FLUVIAIS' THEN 14
                    WHEN 'AQUISIÇÃO DE TOKENS E CERTIFICADOS DIGITAIS' THEN 15
                    WHEN 'SERVIÇOS POSTAIS' THEN 16
                    ELSE 0 END;
                """);

            migrationBuilder.Sql("""
                UPDATE despesas SET tipo_despesa_nao_catalogado = tipo_despesa
                WHERE codigo_tipo_despesa = 0;
                """);

            migrationBuilder.DropColumn(
                name: "tipo_despesa",
                table: "despesas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codigo_tipo_despesa",
                table: "despesas");

            migrationBuilder.DropColumn(
                name: "tipo_despesa_nao_catalogado",
                table: "despesas");

            migrationBuilder.AddColumn<string>(
                name: "tipo_despesa",
                table: "despesas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
