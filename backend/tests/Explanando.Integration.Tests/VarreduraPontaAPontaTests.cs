using System.Globalization;
using Explanando.Application.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Explanando.Infrastructure.Persistencia.Semente;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// Fluxo da Etapa 04 contra Postgres real: dados no banco -> motor de regras -> flags
/// persistidas -> score recalculado.
///
/// Os testes unitarios provam que cada regra decide certo. Este prova que o resultado
/// chega ao banco, respeita o indice unico de (despesa, regra) e recalcula em vez de somar.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class VarreduraPontaAPontaTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>
    /// CNPJ distinto e valido a cada chamada.
    ///
    /// Reaproveitar um CNPJ fixo aqui foi um defeito real: outros testes da mesma colecao
    /// gravam o CNPJ 36826246000164 com situacao ATIVA, e o cenario acabava usando aquele
    /// fornecedor em vez do fornecedor baixado que o teste precisa. O banco e' compartilhado
    /// pela colecao, entao o dado tem de ser unico por teste.
    /// </summary>
    private static string ProximoCnpjValido()
    {
        var raiz = (Interlocked.Increment(ref _sequencia) + 20_000_000)
            .ToString(CultureInfo.InvariantCulture).PadLeft(8, '0')[..8];
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

    private sealed class RelogioFixo(DateTimeOffset? agora = null) : IRelogioDaVarredura
    {
        public DateTimeOffset Agora { get; } =
            agora ?? new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private ServicoDeVarredura MontarServico(
        out ExplanandoDbContext contexto,
        DateTimeOffset? agora = null)
    {
        contexto = banco.CriarContexto();
        return new ServicoDeVarredura(
            new RepositorioDeVarredura(contexto), MotorDeRegras.Padrao(), new RelogioFixo(agora));
    }

    /// <summary>
    /// Semeia um deputado do AP com fornecedor baixado, de ramo alheio e sediado em GO —
    /// combinacao que dispara mais de uma regra sobre a mesma despesa.
    /// </summary>
    private async Task<Guid> SemearParlamentarComProblemaAsync()
    {
        await using var contexto = banco.CriarContexto();

        var deputado = Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = Interlocked.Increment(ref _sequencia) + 700_000,
            NomeCivil = "DEPUTADO DE TESTE",
            NomeParlamentar = "Dep Teste",
            SiglaPartido = "XYZ",
            Uf = "AP",
            Tipo = TipoParlamentar.DeputadoFederal,
        });

        var fornecedor = Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(ProximoCnpjValido()),
            RazaoSocial = "AUTO POSTO BOMTEMPO LTDA",
            CnaePrincipal = "4723700",                       // bebidas: alheio a consultoria
            SituacaoCadastral = SituacaoCadastral.Baixada,
            DataSituacaoCadastral = new DateOnly(2026, 1, 15),
            DataInicioAtividade = new DateOnly(1991, 7, 23),
            Endereco = new EnderecoFornecedor { Uf = "GO", Municipio = "GOIANIA" },
        });

        contexto.Parlamentares.Add(deputado);
        contexto.Fornecedores.Add(fornecedor);
        await contexto.SaveChangesAsync();

        var persistido = fornecedor;

        var despesa = Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = deputado.Id,
            DocumentoFornecedor = persistido.Documento,
            NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
            Competencia = Competencia.Criar(2026, 7),
            TipoDespesa = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.",
            CodigoDocumento = $"doc-{deputado.IdExterno}",
            NumeroDocumento = "8887",
            CodigoTipoDocumento = 4,
            DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = 9_000m,
            ValorLiquido = 9_000m,
        });

        despesa.AssociarFornecedor(persistido.Id);
        contexto.Despesas.Add(despesa);

        await SementeDeLimitesCota.AplicarAsync(contexto);
        await contexto.SaveChangesAsync();

        return deputado.Id;
    }

    [Fact]
    public async Task Varredura_grava_flags_e_score_no_banco()
    {
        var parlamentarId = await SemearParlamentarComProblemaAsync();
        var servico = MontarServico(out var contexto);

        ResultadoDaVarredura resultado;

        await using (contexto)
        {
            resultado = await servico.VarrerAsync(parlamentarId);
        }

        resultado.DespesasAvaliadas.ShouldBe(1);
        resultado.FlagsEmitidas.ShouldBeGreaterThan(1, "o cenario dispara mais de uma regra");
        resultado.Falhas.ShouldBeEmpty();

        await using var leitura = banco.CriarContexto();

        var despesaId = await leitura.Despesas
            .Where(d => d.ParlamentarId == parlamentarId)
            .Select(d => d.Id)
            .SingleAsync();

        var flags = await leitura.FlagsAnomalias.Where(f => f.DespesaId == despesaId).ToListAsync();

        flags.Select(f => f.CodigoRegra).ShouldContain("REG_KYB_001");
        flags.Select(f => f.CodigoRegra).ShouldContain("REG_KYB_002");

        var score = await leitura.ScoresRisco.SingleAsync(s => s.ParlamentarId == parlamentarId);
        score.PesoDasFlags.ShouldBeGreaterThan(0m);
        score.IndiceDeRisco.ShouldBeGreaterThan(0m);
        score.TotalFlagsVermelhas.ShouldBeGreaterThan(0);

        // Os dois numeros do ADR-0009 chegam ao banco separados.
        score.VolumeTotalGasto.ShouldBe(9_000m, "o volume nao depende de flag nenhuma");
        score.DespesasAvaliadas.ShouldBe(1);
        score.AmostraSuficiente.ShouldBeFalse("uma despesa nao da' amostra para ranquear risco");
        score.DataUltimaVarredura.ShouldBe(new RelogioFixo().Agora);
    }

    [Fact]
    public async Task Varrer_de_novo_recalcula_em_vez_de_somar()
    {
        // O indice unico de (despesa, regra) rejeitaria a insercao repetida. Mais que
        // isso: somar dobraria o score de um parlamentar so' por rodar a varredura duas
        // vezes, inflando o ranking publico sem nenhum gasto novo ter existido.
        var parlamentarId = await SemearParlamentarComProblemaAsync();

        var primeiro = MontarServico(out var contextoA);
        ResultadoDaVarredura primeira;
        await using (contextoA)
        {
            primeira = await primeiro.VarrerAsync(parlamentarId);
        }

        var segundo = MontarServico(out var contextoB);
        ResultadoDaVarredura segunda;
        await using (contextoB)
        {
            segunda = await segundo.VarrerAsync(parlamentarId);
        }

        segunda.FlagsEmitidas.ShouldBe(primeira.FlagsEmitidas);
        segunda.IndiceDeRisco.ShouldBe(primeira.IndiceDeRisco);
        segunda.VolumeTotalGasto.ShouldBe(primeira.VolumeTotalGasto);

        await using var leitura = banco.CriarContexto();
        var despesaId = await leitura.Despesas
            .Where(d => d.ParlamentarId == parlamentarId).Select(d => d.Id).SingleAsync();

        (await leitura.FlagsAnomalias.CountAsync(f => f.DespesaId == despesaId))
            .ShouldBe(primeira.FlagsEmitidas, "nada foi duplicado");

        (await leitura.ScoresRisco.CountAsync(s => s.ParlamentarId == parlamentarId)).ShouldBe(1);
    }

    [Fact]
    public async Task Parlamentar_sem_despesa_fica_com_score_zero_e_sem_flag()
    {
        await using var contexto = banco.CriarContexto();

        var deputado = Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = Interlocked.Increment(ref _sequencia) + 700_000,
            NomeCivil = "SUPLENTE QUE NAO ASSUMIU",
            NomeParlamentar = "Suplente",
            SiglaPartido = "XYZ",
            Uf = "SP",
            Tipo = TipoParlamentar.DeputadoFederal,
        });

        contexto.Parlamentares.Add(deputado);
        await contexto.SaveChangesAsync();

        var servico = MontarServico(out var contextoVarredura);

        await using (contextoVarredura)
        {
            var resultado = await servico.VarrerAsync(deputado.Id);

            resultado.DespesasAvaliadas.ShouldBe(0);
            resultado.FlagsEmitidas.ShouldBe(0);
            resultado.VolumeTotalGasto.ShouldBe(0m);
            resultado.IndiceDeRisco.ShouldBe(0m);
            resultado.AmostraSuficiente.ShouldBeFalse();
            resultado.TaxaDeDespesasSinalizadas.ShouldBe(0m);
            resultado.RegrasSilenciosas.Count.ShouldBe(11);
        }
    }

    // ------------------------------------------------------------------
    //  Historico: a curva de tendencia da pagina de perfil
    // ------------------------------------------------------------------

    [Fact]
    public async Task Varredura_fotografa_os_dois_numeros_no_historico()
    {
        var parlamentarId = await SemearParlamentarComProblemaAsync();
        var servico = MontarServico(out var contexto);

        ResultadoDaVarredura resultado;

        await using (contexto)
        {
            resultado = await servico.VarrerAsync(parlamentarId);
        }

        await using var leitura = banco.CriarContexto();
        var ponto = await leitura.HistoricoScores.SingleAsync(h => h.ParlamentarId == parlamentarId);

        ponto.Referencia.ShouldBe(new DateOnly(2026, 8, 19));
        ponto.VolumeTotalGasto.ShouldBe(resultado.VolumeTotalGasto);
        ponto.IndiceDeRisco.ShouldBe(resultado.IndiceDeRisco);
        ponto.VersaoDoMotor.ShouldBe(MotorDeRegras.VersaoAtual);
    }

    [Fact]
    public async Task Duas_varreduras_no_mesmo_dia_regravam_o_mesmo_ponto()
    {
        // Cada solicitacao paga dispara uma varredura, entao duas no mesmo dia e' o caso
        // normal. Inserir de novo violaria o indice unico; empilhar daria degraus dentro
        // de um dia so'.
        var parlamentarId = await SemearParlamentarComProblemaAsync();

        var manha = new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero);
        var tarde = new DateTimeOffset(2026, 8, 19, 17, 0, 0, TimeSpan.Zero);

        var primeira = MontarServico(out var contextoA, manha);

        await using (contextoA)
        {
            await primeira.VarrerAsync(parlamentarId);
        }

        var segunda = MontarServico(out var contextoB, tarde);

        await using (contextoB)
        {
            await segunda.VarrerAsync(parlamentarId);
        }

        await using var leitura = banco.CriarContexto();
        var pontos = await leitura.HistoricoScores
            .Where(h => h.ParlamentarId == parlamentarId)
            .ToListAsync();

        pontos.Count.ShouldBe(1);
        pontos[0].CapturadoEm.ShouldBe(tarde, "a segunda varredura regravou a primeira");
    }

    [Fact]
    public async Task Dias_diferentes_produzem_pontos_diferentes()
    {
        var parlamentarId = await SemearParlamentarComProblemaAsync();

        var dia19 = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var dia20 = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

        var primeira = MontarServico(out var contextoA, dia19);

        await using (contextoA)
        {
            await primeira.VarrerAsync(parlamentarId);
        }

        var segunda = MontarServico(out var contextoB, dia20);

        await using (contextoB)
        {
            await segunda.VarrerAsync(parlamentarId);
        }

        await using var leitura = banco.CriarContexto();
        var pontos = await leitura.HistoricoScores
            .Where(h => h.ParlamentarId == parlamentarId)
            .OrderBy(h => h.Referencia)
            .ToListAsync();

        pontos.Count.ShouldBe(2);
        pontos[0].EhComparavelCom(pontos[1]).ShouldBeTrue("mesma versao do motor");
    }

    // ------------------------------------------------------------------
    //  Cobertura
    // ------------------------------------------------------------------

    [Fact]
    public async Task Resultado_da_varredura_carrega_o_que_nao_foi_avaliado()
    {
        // Sem a tabela da CEAP semeada e sem opcao pelo MEI conhecida, duas regras nao
        // tem como rodar — e o resultado precisa dizer isso, nao ficar em silencio.
        var parlamentarId = await SemearParlamentarComProblemaAsync();
        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            var resultado = await servico.VarrerAsync(parlamentarId);

            resultado.Cobertura.PorRegra.Count.ShouldBe(11);
            resultado.Cobertura.DespesasAvaliadas.ShouldBe(resultado.DespesasAvaliadas);
            resultado.Cobertura.SemDado.ShouldNotBeEmpty();
            resultado.Cobertura.VersaoDoMotor.ShouldBe(MotorDeRegras.VersaoAtual);
            resultado.Cobertura.Resumo().ShouldContain("ausência de dado");
        }
    }

    [Fact]
    public async Task Varredura_de_parlamentar_inexistente_falha_com_mensagem_clara()
    {
        var servico = MontarServico(out var contexto);

        await using (contexto)
        {
            var erro = await Should.ThrowAsync<InvalidOperationException>(
                () => servico.VarrerAsync(Guid.NewGuid()));

            erro.Message.ShouldContain("nao encontrado");
        }
    }
}
