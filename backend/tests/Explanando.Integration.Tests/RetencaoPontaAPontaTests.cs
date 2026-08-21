using System.Globalization;
using Explanando.Application.Retencao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Retencao;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// A poda é o único caminho destrutivo do sistema: ela apaga gasto público do banco. Estes
/// testes cobrem o que não dá para desfazer — que o total sobrevive, que o mês preservado
/// não é tocado, e que a janela desligada não apaga nada.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class RetencaoPontaAPontaTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>Faixa 400.000 — ver a nota de faixas em ProdutoNoBancoTests.</summary>
    private static int ProximoIdExterno() => Interlocked.Increment(ref _sequencia) + 400_000;

    /// <summary>Congelado em agosto de 2026: a janela é medida contra "hoje".</summary>
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private sealed class RelogioFixo : IRelogioDaRetencao
    {
        public DateTimeOffset Agora { get; } = RetencaoPontaAPontaTests.Agora;
    }

    private ServicoDeRetencao MontarServico(out ExplanandoDbContext contexto)
    {
        contexto = banco.CriarContexto();
        return new ServicoDeRetencao(new RepositorioDeRetencao(contexto), new RelogioFixo());
    }

    private static Despesa Nota(Guid parlamentarId, int ano, int mes, decimal valor)
    {
        var dia = new DateTimeOffset(ano, mes, 10, 0, 0, 0, TimeSpan.Zero);

        return Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
            NomeFornecedor = "FORNECEDOR",
            Competencia = Competencia.Criar(ano, mes),
            TipoDespesa = "TELEFONIA",
            CodigoDocumento = $"ret-{Interlocked.Increment(ref _sequencia)}",
            NumeroDocumento = _sequencia.ToString(CultureInfo.InvariantCulture),
            DataDocumento = dia,
            ValorDocumento = valor,
            ValorLiquido = valor,
        });
    }

    /// <summary>Um deputado com gasto em três meses bem separados no tempo.</summary>
    private async Task<Guid> SemearTresMesesAsync()
    {
        var deputado = Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = ProximoIdExterno(),
            NomeCivil = "DEPUTADO DE TESTE",
            NomeParlamentar = "Deputado de Teste",
            SiglaPartido = "MDB",
            Uf = "AP",
            Tipo = TipoParlamentar.DeputadoFederal,
        });

        await using var contexto = banco.CriarContexto();
        contexto.Parlamentares.Add(deputado);
        contexto.Despesas.AddRange(
            Nota(deputado.Id, 2023, 3, 1_000m),   // bem antigo
            Nota(deputado.Id, 2023, 3, 500m),
            Nota(deputado.Id, 2024, 6, 2_000m),   // antigo
            Nota(deputado.Id, 2026, 8, 300m));    // mês corrente
        await contexto.SaveChangesAsync();

        return deputado.Id;
    }

    // ------------------------------------------------------------------
    //  Consolidar sem podar
    // ------------------------------------------------------------------

    [Fact]
    public async Task Janela_ilimitada_consolida_e_nao_apaga_nada()
    {
        // É a configuração do V1. Se esta passada apagasse qualquer coisa, a consolidação
        // de rotina viraria perda de dado como efeito colateral de somar.
        var parlamentarId = await SemearTresMesesAsync();
        var servico = MontarServico(out var contexto);

        ResultadoDaRetencao resultado;

        await using (contexto)
        {
            resultado = await servico.AplicarAsync(JanelaDeRetencao.Ilimitada);
        }

        resultado.Podou.ShouldBeFalse();
        resultado.DespesasDescartadas.ShouldBe(0);

        await using var leitura = banco.CriarContexto();

        (await leitura.Despesas.CountAsync(d => d.ParlamentarId == parlamentarId)).ShouldBe(4);

        var consolidados = await leitura.ConsolidadosMensais
            .Where(c => c.ParlamentarId == parlamentarId)
            .ToListAsync();

        consolidados.Count.ShouldBe(3, "três meses distintos");
        consolidados.ShouldAllBe(c => c.DetalheDisponivel);
        consolidados.Sum(c => c.TotalLiquido).ShouldBe(3_800m);
    }

    [Fact]
    public async Task Consolidar_duas_vezes_nao_duplica_nem_soma_em_cima()
    {
        // A consolidação roda a cada varredura sobre os mesmos meses.
        var parlamentarId = await SemearTresMesesAsync();

        for (var vez = 0; vez < 2; vez++)
        {
            var servico = MontarServico(out var contexto);

            await using (contexto)
            {
                await servico.AplicarAsync(JanelaDeRetencao.Ilimitada);
            }
        }

        await using var leitura = banco.CriarContexto();
        var consolidados = await leitura.ConsolidadosMensais
            .Where(c => c.ParlamentarId == parlamentarId)
            .ToListAsync();

        consolidados.Count.ShouldBe(3);
        consolidados.Sum(c => c.TotalLiquido).ShouldBe(3_800m, "e não o dobro");
    }

    // ------------------------------------------------------------------
    //  Podar
    // ------------------------------------------------------------------

    [Fact]
    public async Task Poda_apaga_o_detalhe_antigo_e_preserva_o_total()
    {
        // O ponto inteiro do mecanismo: o gasto público não some, só deixa de ser
        // consultável nota a nota.
        var parlamentarId = await SemearTresMesesAsync();
        var servico = MontarServico(out var contexto);

        ResultadoDaRetencao resultado;

        await using (contexto)
        {
            // 24 meses a partir de agosto/2026 preserva de setembro/2024 em diante.
            resultado = await servico.AplicarAsync(JanelaDeRetencao.De(24));
        }

        resultado.Podou.ShouldBeTrue();
        resultado.DespesasDescartadas.ShouldBe(3, "os dois de 2023 e o de junho/2024");

        await using var leitura = banco.CriarContexto();

        var restantes = await leitura.Despesas
            .Where(d => d.ParlamentarId == parlamentarId)
            .ToListAsync();

        restantes.Count.ShouldBe(1);
        restantes[0].Competencia.ShouldBe(Competencia.Criar(2026, 8));

        var consolidados = await leitura.ConsolidadosMensais
            .Where(c => c.ParlamentarId == parlamentarId)
            .OrderBy(c => c.Competencia)
            .ToListAsync();

        consolidados.Count.ShouldBe(3, "os meses podados continuam existindo como total");
        consolidados.Sum(c => c.TotalLiquido)
            .ShouldBe(3_800m, "o volume do mandato inteiro sobrevive à poda");
    }

    [Fact]
    public async Task Mes_podado_declara_que_o_detalhe_nao_esta_mais_disponivel()
    {
        // Sem esta marca a API mostraria um mês consolidado como se as notas ainda
        // pudessem ser abertas, e o leitor concluiria que o documento foi retirado.
        var parlamentarId = await SemearTresMesesAsync();
        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            await servico.AplicarAsync(JanelaDeRetencao.De(24));
        }

        await using var leitura = banco.CriarContexto();
        var consolidados = await leitura.ConsolidadosMensais
            .Where(c => c.ParlamentarId == parlamentarId)
            .ToListAsync();

        consolidados
            .Where(c => c.Competencia < Competencia.Criar(2024, 9))
            .ShouldAllBe(c => !c.DetalheDisponivel);

        consolidados
            .Single(c => c.Competencia == Competencia.Criar(2026, 8))
            .DetalheDisponivel.ShouldBeTrue();
    }

    [Fact]
    public async Task Poda_repetida_e_idempotente()
    {
        var parlamentarId = await SemearTresMesesAsync();

        ResultadoDaRetencao segunda;

        var primeiroServico = MontarServico(out var contextoA);

        await using (contextoA)
        {
            await primeiroServico.AplicarAsync(JanelaDeRetencao.De(24));
        }

        var segundoServico = MontarServico(out var contextoB);

        await using (contextoB)
        {
            segunda = await segundoServico.AplicarAsync(JanelaDeRetencao.De(24));
        }

        segunda.DespesasDescartadas.ShouldBe(0, "não sobrou nada antigo para apagar");
        segunda.MesesSemDetalhe.ShouldBe(0, "já estavam marcados");

        await using var leitura = banco.CriarContexto();

        (await leitura.ConsolidadosMensais.CountAsync(c => c.ParlamentarId == parlamentarId))
            .ShouldBe(3);
    }

    [Fact]
    public async Task Poda_nao_reduz_o_total_de_um_mes_que_ela_mesma_esvaziou()
    {
        // A ordem importa: consolida primeiro, apaga depois. Invertida, o total do mês
        // podado seria recalculado sobre zero despesas e o gasto sumiria de vez.
        var parlamentarId = await SemearTresMesesAsync();

        var primeiro = MontarServico(out var contextoA);

        await using (contextoA)
        {
            await primeiro.AplicarAsync(JanelaDeRetencao.De(24));
        }

        // Uma segunda passada encontra os meses antigos já sem despesa alguma.
        var segundo = MontarServico(out var contextoB);

        await using (contextoB)
        {
            await segundo.AplicarAsync(JanelaDeRetencao.De(24));
        }

        await using var leitura = banco.CriarContexto();

        (await leitura.ConsolidadosMensais
                .Where(c => c.ParlamentarId == parlamentarId)
                .SumAsync(c => c.TotalLiquido))
            .ShouldBe(3_800m, "o total dos meses podados não pode ser zerado depois");
    }
}
