using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;

namespace Explanando.Domain.Tests.Comum;

/// <summary>
/// A Receita grava a razão social do MEI como "NOME DO DONO 12345678901" — o CPF inteiro do
/// empresário no fim do nome —, e a Câmara repete isso no nome do fornecedor. Medido na base
/// em 2026-09-25: 641 fornecedores e 2.885 despesas, e o número ia parar na tela, na descrição
/// dos achados e no RSS. O produto nunca exibe nem grava CPF completo (§3.4 do handoff).
/// </summary>
public sealed class CpfEmTextoTests
{
    [Theory]
    [InlineData("RODRIGO SANTOS GONSALVES 52998224725", "RODRIGO SANTOS GONSALVES ***.982.247-**")]
    [InlineData("CARLOS MOREIRA 529.982.247-25", "CARLOS MOREIRA ***.982.247-**")]
    [InlineData("52998224725 FULANO", "***.982.247-** FULANO")]
    [InlineData("PEDRO DA SILVA 52998224725-ME", "PEDRO DA SILVA ***.982.247-**-ME")]
    public void Mascara_o_CPF_no_nome_do_mesmo_jeito_que_o_documento(string nome, string esperado)
    {
        CpfEmTexto.Mascarar(nome).ShouldBe(esperado);
    }

    [Theory]
    [InlineData("AGENCIA DE VIAGENS LTDA")]
    [InlineData("POSTO 1234 LTDA")]
    // CNPJ de 14 dígitos não é CPF: um pedaço de 11 dele não pode ser mascarado.
    [InlineData("EMPRESA 11222333000181")]
    [InlineData("EMPRESA 11.222.333/0001-81")]
    // Doze dígitos seguidos não são um CPF — e mascarar parte deles inventaria um.
    [InlineData("TELEFONE 551199998888")]
    public void Nao_toca_no_que_nao_e_CPF(string nome)
    {
        CpfEmTexto.Mascarar(nome).ShouldBe(nome);
    }

    [Fact]
    public void A_mascara_e_a_mesma_do_documento_formatado()
    {
        // Um leitor que compara o nome com o documento da nota precisa ver a mesma máscara
        // nos dois lugares, ou vai achar que são pessoas diferentes.
        CpfEmTexto.Mascarar("X 52998224725").ShouldEndWith(CpfCnpj.Criar("52998224725").Formatado);
    }

    [Fact]
    public void A_despesa_guarda_o_nome_do_fornecedor_ja_mascarado()
    {
        var despesa = Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = Guid.CreateVersion7(),
            DocumentoFornecedor = CpfCnpj.Criar("11222333000181"),
            NomeFornecedor = "RODRIGO SANTOS GONSALVES 52998224725",
            Competencia = Competencia.Criar(2026, 3),
            TipoDespesa = "MANUTENÇÃO DE ESCRITÓRIO DE APOIO À ATIVIDADE PARLAMENTAR",
            CodigoDocumento = "cpf-no-nome",
            NumeroDocumento = "1",
            DataDocumento = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = 100m,
            ValorLiquido = 100m,
        });

        despesa.NomeFornecedor.ShouldBe("RODRIGO SANTOS GONSALVES ***.982.247-**");
    }

    [Fact]
    public void O_fornecedor_guarda_a_razao_social_e_o_nome_fantasia_ja_mascarados()
    {
        var fornecedor = Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar("11222333000181"),
            RazaoSocial = "RODRIGO SANTOS GONSALVES 52998224725",
            NomeFantasia = "RODRIGO 52998224725",
        });

        fornecedor.RazaoSocial.ShouldBe("RODRIGO SANTOS GONSALVES ***.982.247-**");
        fornecedor.NomeFantasia.ShouldBe("RODRIGO ***.982.247-**");
    }
}
