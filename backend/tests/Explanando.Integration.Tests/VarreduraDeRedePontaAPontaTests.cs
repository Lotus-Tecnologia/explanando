using System.Globalization;
using Explanando.Application.Auditoria;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// A passada de rede contra Postgres real. O que só aqui se prova: que apagar as flags da
/// passada anterior recorta por <b>código de regra</b> e não por despesa — recortar por
/// despesa apagaria os achados individuais de deputados que nada têm com a rede.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class VarreduraDeRedePontaAPontaTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>Faixa 500.000 — ver a nota de faixas em ProdutoNoBancoTests.</summary>
    private static int ProximoIdExterno() => Interlocked.Increment(ref _sequencia) + 500_000;

    private static readonly DateTimeOffset PrimeiraNota =
        new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

    /// <summary>CNPJ distinto e válido a cada chamada, com dígitos calculados de verdade.</summary>
    private static string ProximoCnpjValido()
    {
        var raiz = (Interlocked.Increment(ref _sequencia) + 30_000_000)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(8, '0')[..8];
        var doze = raiz + "0001";

        var comPrimeiro = doze + Digito(doze, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return comPrimeiro + Digito(comPrimeiro, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);

        static char Digito(string numero, int[] pesos)
        {
            var soma = 0;

            for (var i = 0; i < pesos.Length; i++)
            {
                soma += (numero[i] - '0') * pesos[i];
            }

            var resto = soma % 11;
            return (char)('0' + (resto < 2 ? 0 : 11 - resto));
        }
    }

    private static Parlamentar Deputado(string partido) =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = ProximoIdExterno(),
            NomeCivil = "DEPUTADO DE TESTE",
            NomeParlamentar = "Deputado de Teste",
            SiglaPartido = partido,
            Uf = "AP",
            Tipo = TipoParlamentar.DeputadoFederal,
        });

    private static Fornecedor Fornecedora(string cnpj) =>
        Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(cnpj),
            RazaoSocial = "CONSULTORIA NOVA LTDA",
            CnaePrincipal = "7020400",
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataSituacaoCadastral = new DateOnly(2026, 1, 15),
            DataInicioAtividade = new DateOnly(2026, 1, 15),
            Endereco = new EnderecoFornecedor { Uf = "DF", Municipio = "BRASILIA" },
        });

    private static Despesa Nota(Guid parlamentarId, Guid fornecedorId, string cnpj, decimal valor)
    {
        var despesa = Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar(cnpj),
            NomeFornecedor = "CONSULTORIA NOVA LTDA",
            Competencia = Competencia.Criar(2026, 3),
            TipoDespesa = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.",
            CodigoDocumento = $"rede-{ProximoIdExterno()}",
            NumeroDocumento = "9001",
            DataDocumento = PrimeiraNota,
            ValorDocumento = valor,
            ValorLiquido = valor,
        });

        despesa.AssociarFornecedor(fornecedorId);
        return despesa;
    }

    /// <summary>Três deputados do mesmo partido e um fornecedor recém-aberto comum.</summary>
    private async Task<(List<Guid> Deputados, List<Guid> Despesas)> SemearConcentracaoAsync()
    {
        var cnpj = ProximoCnpjValido();
        var fornecedor = Fornecedora(cnpj);
        var deputados = new List<Parlamentar> { Deputado("MDB"), Deputado("MDB"), Deputado("MDB") };

        var despesas = deputados
            .Select(deputado => Nota(deputado.Id, fornecedor.Id, cnpj, 5_000m))
            .ToList();

        await using var contexto = banco.CriarContexto();
        contexto.Parlamentares.AddRange(deputados);
        contexto.Fornecedores.Add(fornecedor);
        contexto.Despesas.AddRange(despesas);
        await contexto.SaveChangesAsync();

        return ([.. deputados.Select(d => d.Id)], [.. despesas.Select(d => d.Id)]);
    }

    private ServicoDeVarreduraDeRede MontarServico(out Explanando.Infrastructure.Persistencia.ExplanandoDbContext contexto)
    {
        contexto = banco.CriarContexto();
        return new ServicoDeVarreduraDeRede(new RepositorioDeRede(contexto), MotorDeRede.Padrao());
    }

    [Fact]
    public async Task Passada_de_rede_grava_as_flags_de_concentracao()
    {
        var (deputados, _) = await SemearConcentracaoAsync();
        var servico = MontarServico(out var contexto);

        ResultadoDeRede resultado;

        await using (contexto)
        {
            resultado = await servico.VarrerAsync();
        }

        resultado.Flags.ShouldNotBeEmpty();

        await using var leitura = banco.CriarContexto();
        var gravadas = await leitura.FlagsAnomalias
            .Where(flag => flag.CodigoRegra == "REG_NET_004")
            .Join(
                leitura.Despesas.Where(d => deputados.Contains(d.ParlamentarId)),
                flag => flag.DespesaId,
                despesa => despesa.Id,
                (flag, _) => flag)
            .ToListAsync();

        gravadas.Count.ShouldBe(3, "uma flag por deputado do trio");
        gravadas.ShouldAllBe(flag => flag.VersaoDoMotor == MotorDeRegras.VersaoAtual);
        gravadas.ShouldAllBe(flag => flag.GrauDeConfianca < 1.0m);
    }

    [Fact]
    public async Task Rodar_de_novo_recalcula_em_vez_de_somar()
    {
        var (deputados, _) = await SemearConcentracaoAsync();

        for (var vez = 0; vez < 2; vez++)
        {
            var servico = MontarServico(out var contexto);

            await using (contexto)
            {
                await servico.VarrerAsync();
            }
        }

        await using var leitura = banco.CriarContexto();
        var quantas = await leitura.FlagsAnomalias
            .Where(flag => flag.CodigoRegra == "REG_NET_004")
            .Join(
                leitura.Despesas.Where(d => deputados.Contains(d.ParlamentarId)),
                flag => flag.DespesaId,
                despesa => despesa.Id,
                (flag, _) => flag)
            .CountAsync();

        quantas.ShouldBe(3, "a segunda passada apagou a primeira antes de gravar");
    }

    [Fact]
    public async Task Limpeza_da_passada_de_rede_nao_apaga_flag_individual_da_mesma_despesa()
    {
        // O ponto do teste. Se a limpeza recortasse por despesa em vez de por código de
        // regra, ela levaria junto os achados da varredura individual — que falam de outra
        // coisa e foram produzidos por outra passada.
        var (deputados, despesas) = await SemearConcentracaoAsync();

        var individual = FlagAnomalia.Criar(
            despesas[0],
            "REG_KYB_002",
            Gravidade.Alta,
            "Inconsistência com a regra da Câmara: situação cadastral do fornecedor.",
            1.0m);
        individual.RegistrarVersaoDoMotor(MotorDeRegras.VersaoAtual);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.FlagsAnomalias.Add(individual);
            await escrita.SaveChangesAsync();
        }

        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            await servico.VarrerAsync();
        }

        await using var leitura = banco.CriarContexto();

        (await leitura.FlagsAnomalias.AnyAsync(flag => flag.Id == individual.Id))
            .ShouldBeTrue("a flag individual sobreviveu à limpeza da passada de rede");

        (await leitura.FlagsAnomalias.CountAsync(
            flag => flag.DespesaId == despesas[0] && flag.CodigoRegra == "REG_NET_004"))
            .ShouldBe(1, "e a de rede foi gravada ao lado dela");

        _ = deputados;
    }

    [Fact]
    public async Task Resultado_avisa_que_a_base_esta_abaixo_do_piso()
    {
        // A base de teste tem punhado de deputados. "Exclusividade partidária" sobre isso
        // é aritmética, e o resultado precisa dizer para ninguém publicar.
        await SemearConcentracaoAsync();
        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            var resultado = await servico.VarrerAsync();

            resultado.PisoDeParlamentares.ShouldBe(MotorDeRede.PisoDeParlamentaresParaRede);
            resultado.BaseSuficiente.ShouldBeFalse();
            resultado.ParlamentaresNaBase.ShouldBeLessThan(resultado.PisoDeParlamentares);
        }
    }
}
