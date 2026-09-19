using System.Globalization;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Infrastructure.Persistencia.Consultas;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// O mapa de fornecedores compartilhados contra um Postgres de verdade.
///
/// <para><b>Estes testes existem por causa de um erro que só o banco pegou.</b> O
/// agrupamento por CNPJ parecia correto e compilava, mas <c>GroupBy(d =&gt;
/// d.DocumentoFornecedor.Numero)</c> não é traduzível — o documento é um objeto de valor
/// com <c>ValueConverter</c>, e o EF Core só sabe agrupar pela propriedade convertida
/// inteira. O provedor em memória teria passado, porque lá o LINQ roda em objetos.</para>
///
/// <para>O resto do arquivo protege o corte em si: que o fornecedor de seis gabinetes
/// realmente não chega ao desenho, e que um estorno não infla o valor de uma aresta.</para>
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class MapaDaRedeNoBancoTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>Faixa 700.000 — ver a nota de faixas em ProdutoNoBancoTests.</summary>
    private static int ProximoIdExterno() => Interlocked.Increment(ref _sequencia) + 700_000;

    private static readonly DateTimeOffset Nota = new(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

    private static string ProximoCnpjValido()
    {
        var raiz = (Interlocked.Increment(ref _sequencia) + 60_000_000)
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

    private static Parlamentar Deputado(string partido = "MDB", string uf = "AP") =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = ProximoIdExterno(),
            NomeCivil = "DEPUTADO DE TESTE",
            NomeParlamentar = $"Deputado {ProximoIdExterno()}",
            SiglaPartido = partido,
            Uf = uf,
            Tipo = TipoParlamentar.DeputadoFederal,
        });

    private static Fornecedor Fornecedora(string cnpj, string razao) =>
        Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(cnpj),
            RazaoSocial = razao,
            CnaePrincipal = "7311400",
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataInicioAtividade = new DateOnly(2019, 4, 2),
            Endereco = new EnderecoFornecedor { Uf = "DF", Municipio = "BRASILIA" },
        });

    private static Despesa NotaDe(Guid parlamentarId, string cnpj, string razao, decimal valor) =>
        Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar(cnpj),
            NomeFornecedor = razao,
            Competencia = Competencia.Criar(2026, 3),
            TipoDespesa = "DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.",
            CodigoDocumento = $"rede-{ProximoIdExterno()}",
            NumeroDocumento = "7001",
            DataDocumento = Nota,
            ValorDocumento = valor,
            ValorLiquido = valor,
        });

    /// <summary>
    /// Zera as tabelas que o mapa lê.
    ///
    /// <para><b>Precisa ser <c>TRUNCATE ... CASCADE</c>, e não um <c>DELETE</c> por
    /// tabela.</b> O mapa é um agregado da base inteira: qualquer despesa deixada por
    /// outra classe de teste entra na conta e derruba um <c>Assert.Single</c>. E apagar
    /// <c>parlamentares</c> com <c>DELETE</c> esbarra nas chaves estrangeiras de
    /// <c>solicitacoes_varredura</c>, <c>scores_risco</c> e companhia — foi assim que
    /// estes testes passaram sozinhos e falharam na suíte.</para>
    /// </summary>
    private async Task LimparAsync()
    {
        await using var contexto = banco.CriarContexto();

        await contexto.Database.ExecuteSqlRawAsync(
            "TRUNCATE parlamentares, fornecedores, despesas CASCADE");
    }

    private ConsultasDaRedeEfCore Consultas() => new(banco.CriarContexto());

    [Fact]
    public async Task O_agrupamento_por_CNPJ_roda_no_banco_e_devolve_o_mapa()
    {
        // O teste que faltava quando a consulta quebrou em producao: o GroupBy precisa
        // virar SQL. Se voltar a ser client evaluation, isto aqui explode.
        await LimparAsync();

        var cnpj = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.Add(Fornecedora(cnpj, "AGENCIA COMPARTILHADA LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, cnpj, "AGENCIA COMPARTILHADA LTDA", 40_000m),
                NotaDe(outro.Id, cnpj, "AGENCIA COMPARTILHADA LTDA", 30_000m));

            await contexto.SaveChangesAsync();
        }

        var mapa = await Consultas().ObterMapaAsync();

        var ligacao = Assert.Single(mapa.Ligacoes);

        Assert.Equal("AGENCIA COMPARTILHADA LTDA", ligacao.Fornecedor);
        Assert.Equal(70_000m, ligacao.Valor);
        Assert.Equal(2, ligacao.Parlamentares.Count);
        Assert.Equal(2, mapa.Parlamentares.Count);
    }

    [Fact]
    public async Task O_cadastro_do_CNPJ_chega_junto__CNAE_municipio_e_abertura()
    {
        await LimparAsync();

        var cnpj = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.Add(Fornecedora(cnpj, "GRAFICA DUPLA LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, cnpj, "GRAFICA DUPLA LTDA", 60_000m),
                NotaDe(outro.Id, cnpj, "GRAFICA DUPLA LTDA", 10_000m));

            await contexto.SaveChangesAsync();
        }

        var ligacao = Assert.Single((await Consultas().ObterMapaAsync()).Ligacoes);

        Assert.Equal("7311400", ligacao.CnaePrincipal);
        Assert.Equal("BRASILIA", ligacao.Municipio);
        Assert.Equal("DF", ligacao.Uf);
        Assert.Equal(new DateOnly(2019, 4, 2), ligacao.AbertaEm);
    }

    [Fact]
    public async Task O_fornecedor_de_seis_gabinetes_nao_entra_no_desenho()
    {
        // O caso da CASCOL, em miniatura. Dinheiro nao falta; o que falta e' significado.
        await LimparAsync();

        var cnpj = ProximoCnpjValido();
        var deputados = Enumerable.Range(0, 6).Select(_ => Deputado()).ToList();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(deputados);
            contexto.Fornecedores.Add(Fornecedora(cnpj, "POSTO DE TODO MUNDO LTDA"));
            contexto.Despesas.AddRange(
                deputados.Select(d => NotaDe(d.Id, cnpj, "POSTO DE TODO MUNDO LTDA", 90_000m)));

            await contexto.SaveChangesAsync();
        }

        var mapa = await Consultas().ObterMapaAsync();

        Assert.Empty(mapa.Ligacoes);
        Assert.Empty(mapa.Parlamentares);

        // Mas ele continua sendo contado como compartilhado: o leitor precisa saber o
        // tamanho do que ficou de fora.
        Assert.Equal(1, mapa.FornecedoresCompartilhados);
        Assert.Equal("POSTO DE TODO MUNDO LTDA", mapa.MaiorFornecedorCompartilhado);
        Assert.Equal(6, mapa.ParlamentaresAtendidosPeloMaior);
    }

    [Fact]
    public async Task O_estorno_nao_infla_o_valor_de_uma_aresta()
    {
        // Valor negativo e' a anulacao de uma nota. Soma-lo produziria um total que nao
        // corresponde a dinheiro que saiu — e, no limite, uma aresta que nao existe.
        await LimparAsync();

        var cnpj = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.Add(Fornecedora(cnpj, "CONSULTORIA ESTORNADA LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, cnpj, "CONSULTORIA ESTORNADA LTDA", 55_000m),
                NotaDe(outro.Id, cnpj, "CONSULTORIA ESTORNADA LTDA", 20_000m),
                NotaDe(outro.Id, cnpj, "CONSULTORIA ESTORNADA LTDA", -20_000m));

            await contexto.SaveChangesAsync();
        }

        var ligacao = Assert.Single((await Consultas().ObterMapaAsync()).Ligacoes);

        Assert.Equal(75_000m, ligacao.Valor);
        Assert.Equal(2, ligacao.Notas);
    }

    [Fact]
    public async Task Um_fornecedor_so_em_comum_nao_vira_par_no_desenho()
    {
        // 1.139 dos 1.386 pares da base real estao neste caso. Desenha-los poria 454 dos
        // 463 parlamentares num componente unico, e o grafo deixaria de distinguir nada.
        await LimparAsync();

        var cnpj = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.Add(Fornecedora(cnpj, "UNICA LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, cnpj, "UNICA LTDA", 55_000m),
                NotaDe(outro.Id, cnpj, "UNICA LTDA", 55_000m));

            await contexto.SaveChangesAsync();
        }

        var mapa = await Consultas().ObterMapaAsync();

        Assert.Single(mapa.Ligacoes);
        Assert.Empty(mapa.Pares);
        Assert.Equal(1, mapa.ParesComUmFornecedorSo);
    }

    [Fact]
    public async Task Dois_fornecedores_em_comum_viram_um_par__com_a_UF_marcada()
    {
        await LimparAsync();

        var primeiro = ProximoCnpjValido();
        var segundo = ProximoCnpjValido();
        var um = Deputado("PP", "PB");
        var outro = Deputado("PODE", "PB");

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.AddRange(
                Fornecedora(primeiro, "AGENCIA UM LTDA"),
                Fornecedora(segundo, "AGENCIA DOIS LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, primeiro, "AGENCIA UM LTDA", 60_000m),
                NotaDe(outro.Id, primeiro, "AGENCIA UM LTDA", 10_000m),
                NotaDe(um.Id, segundo, "AGENCIA DOIS LTDA", 80_000m),
                NotaDe(outro.Id, segundo, "AGENCIA DOIS LTDA", 20_000m));

            await contexto.SaveChangesAsync();
        }

        var par = Assert.Single((await Consultas().ObterMapaAsync()).Pares);

        Assert.Equal(2, par.Fornecedores);
        Assert.Equal(170_000m, par.Valor);
        Assert.True(par.MesmaUf);
        Assert.False(par.MesmoPartido);
    }

    [Fact]
    public async Task O_grau_e_o_alcance_de_um_parlamentar_sao_coisas_diferentes()
    {
        // Grau e' quantos fornecedores ele divide; alcance e' quantas PESSOAS aqueles
        // fornecedores lhe trazem. Dois fornecedores com o mesmo par dao grau 2 e
        // alcance 1 — e e' essa diferenca que revela um agrupamento fechado.
        await LimparAsync();

        var primeiro = ProximoCnpjValido();
        var segundo = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro);
            contexto.Fornecedores.AddRange(
                Fornecedora(primeiro, "AGENCIA UM LTDA"),
                Fornecedora(segundo, "AGENCIA DOIS LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, primeiro, "AGENCIA UM LTDA", 60_000m),
                NotaDe(outro.Id, primeiro, "AGENCIA UM LTDA", 10_000m),
                NotaDe(um.Id, segundo, "AGENCIA DOIS LTDA", 80_000m),
                NotaDe(outro.Id, segundo, "AGENCIA DOIS LTDA", 20_000m));

            await contexto.SaveChangesAsync();
        }

        var pessoa = (await Consultas().ObterMapaAsync())
            .Parlamentares
            .Single(p => p.Id == um.Id);

        Assert.Equal(2, pessoa.Ligacoes);
        Assert.Equal(1, pessoa.Vizinhos);
        Assert.Equal(170_000m, pessoa.ValorNoMapa);
    }

    [Fact]
    public async Task As_ligacoes_de_um_parlamentar_sao_so_as_dele()
    {
        await LimparAsync();

        var nosso = ProximoCnpjValido();
        var alheio = ProximoCnpjValido();
        var um = Deputado();
        var outro = Deputado();
        var estranho = Deputado();
        var parDoEstranho = Deputado();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.Parlamentares.AddRange(um, outro, estranho, parDoEstranho);
            contexto.Fornecedores.AddRange(
                Fornecedora(nosso, "NOSSA AGENCIA LTDA"),
                Fornecedora(alheio, "OUTRA AGENCIA LTDA"));
            contexto.Despesas.AddRange(
                NotaDe(um.Id, nosso, "NOSSA AGENCIA LTDA", 60_000m),
                NotaDe(outro.Id, nosso, "NOSSA AGENCIA LTDA", 10_000m),
                NotaDe(estranho.Id, alheio, "OUTRA AGENCIA LTDA", 60_000m),
                NotaDe(parDoEstranho.Id, alheio, "OUTRA AGENCIA LTDA", 10_000m));

            await contexto.SaveChangesAsync();
        }

        var ligacoes = await Consultas().ObterLigacoesAsync(um.Id);

        Assert.Equal("NOSSA AGENCIA LTDA", Assert.Single(ligacoes).Fornecedor);
    }

    [Fact]
    public async Task Um_parlamentar_fora_do_mapa_devolve_lista_vazia__e_nao_erro()
    {
        // O caso da maioria: 463 dos 513 entram, e nao estar no mapa nao diz nada sobre
        // ninguem. A API tem que responder isso sem parecer falha.
        await LimparAsync();

        Assert.Empty(await Consultas().ObterLigacoesAsync(Guid.NewGuid()));
    }
}
