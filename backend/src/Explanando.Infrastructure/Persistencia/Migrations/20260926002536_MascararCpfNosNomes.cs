using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Explanando.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class MascararCpfNosNomes : Migration
    {
        // O mesmo padrão de CpfEmTexto (Domain/Comum): 11 dígitos, ou 000.000.000-00, sem
        // dígito colado dos lados — para não morder CNPJ.
        private const string Padrao =
            @"'(?<![0-9])([0-9]{3})\.?([0-9]{3})\.?([0-9]{3})-?([0-9]{2})(?![0-9])'";

        private const string Mascara = @"'***.\2.\3-**'";

        /// <summary>
        /// Mascara o CPF que a Receita e a Câmara escrevem dentro do nome do MEI — 641
        /// fornecedores e 2.885 despesas em 2026-09-25. O código novo já grava mascarado; isto
        /// corrige o que entrou antes. As descrições dos achados citam o nome do fornecedor e
        /// seriam refeitas na próxima varredura, mas não se espera por ela: até lá, o número
        /// continuaria público.
        /// </summary>
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE fornecedores
                   SET razao_social = regexp_replace(razao_social, {Padrao}, {Mascara}, 'g')
                 WHERE razao_social ~ {Padrao};

                UPDATE fornecedores
                   SET nome_fantasia = regexp_replace(nome_fantasia, {Padrao}, {Mascara}, 'g')
                 WHERE nome_fantasia ~ {Padrao};

                UPDATE despesas
                   SET nome_fornecedor = regexp_replace(nome_fornecedor, {Padrao}, {Mascara}, 'g')
                 WHERE nome_fornecedor ~ {Padrao};

                UPDATE flags_anomalias
                   SET descricao = regexp_replace(descricao, {Padrao}, {Mascara}, 'g')
                 WHERE descricao ~ {Padrao};
                """);
        }

        /// <summary>Sem volta: o número mascarado não se recupera, e é esse o ponto.</summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
