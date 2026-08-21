using System.Globalization;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Explanando.Integration.Tests;

/// <summary>
/// Prova que o mapeamento EF Core funciona contra Postgres de verdade. Os testes unitarios
/// do dominio provam que a regra esta certa; estes provam que ela sobrevive ao banco.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class MapeamentoTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>Id externo distinto por teste: a tabela tem indice unico em (id_externo, tipo).</summary>
    private static int ProximoIdExterno() => Interlocked.Increment(ref _sequencia) + 900_000;

    /// <summary>
    /// Gera um CNPJ distinto e <b>valido</b> a cada chamada. Numero inventado nao serve:
    /// Fornecedor.Criar recusa documento que nao passa no digito verificador, entao o
    /// teste tem de calcular os digitos como a Receita calcula.
    /// </summary>
    private static string ProximoCnpjValido()
    {
        var raiz = (Interlocked.Increment(ref _sequencia) + 10_000_000)
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

    private static Parlamentar NovoDeputado() => Parlamentar.Criar(new NovoParlamentar
    {
        IdExterno = ProximoIdExterno(),
        NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
        NomeParlamentar = "Acácio Favacho",
        SiglaPartido = "MDB",
        Uf = "AP",
        Tipo = TipoParlamentar.DeputadoFederal,
        FotoUrl = "https://www.camara.leg.br/internet/deputado/bandep/204379.jpg",
    });

    private static Fornecedor NovoPosto(string cnpj = "36826246000164") =>
        Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(cnpj),
            RazaoSocial = "AUTO POSTO BOMTEMPO LTDA",
            NomeFantasia = "POSTO BOMTEMPO",
            CnaePrincipal = "4731800",
            CnaesSecundarios = ["3600601", "4732600", "4784900", "5611204"],
            SituacaoCadastral = SituacaoCadastral.Ativa,
            DataSituacaoCadastral = new DateOnly(2005, 9, 24),
            DataInicioAtividade = new DateOnly(1991, 7, 23),
            OptantePeloMei = null,
            Porte = "DEMAIS",
            Endereco = new EnderecoFornecedor
            {
                Logradouro = "RODOVIA GO070",
                Numero = "S/N",
                Bairro = "FAZ CAVEIRAS",
                Municipio = "GOIANIA",
                CodigoMunicipioIbge = 5208707,
                Uf = "GO",
                Cep = "74470440",
            },
        });

    private static NovaDespesa DadosDeDespesa(Guid parlamentarId, string codigoDocumento) => new()
    {
        ParlamentarId = parlamentarId,
        DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
        NomeFornecedor = "Auto Posto Bom Tempo Ltda",
        Competencia = Competencia.Criar(2026, 7),
        TipoDespesa = "COMBUSTÍVEIS E LUBRIFICANTES.",
        CodigoDocumento = codigoDocumento,
        NumeroDocumento = "8887",
        DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
        ValorDocumento = 385.92m,
        ValorLiquido = 385.92m,
        UrlDocumento = "http://www.camara.leg.br/cota-parlamentar/nota",
        CodigoLote = 2253045,
        Parcela = 0,
    };

    // ------------------------------------------------------------------
    //  Estrutura
    // ------------------------------------------------------------------

    [Fact]
    public async Task Migration_cria_o_esquema_em_snake_case()
    {
        await using var contexto = banco.CriarContexto();

        var tabelas = await contexto.Database
            .SqlQuery<string>($"""
                SELECT table_name AS "Value" FROM information_schema.tables
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
                """)
            .ToListAsync();

        tabelas.ShouldContain("parlamentares");
        tabelas.ShouldContain("flags_anomalias");
        tabelas.ShouldContain("scores_risco");
        tabelas.ShouldContain("limites_cota");

        // __EFMigrationsHistory e' bookkeeping do proprio EF Core, nao tabela nossa:
        // o nome dela e' fixo no provedor e nao passa pela convencao de nomenclatura.
        tabelas
            .Where(t => !t.StartsWith("__", StringComparison.Ordinal))
            .ShouldAllBe(t => !t.Any(char.IsUpper) && !t.Contains(' '));
    }

    [Fact]
    public async Task Nao_restam_migrations_pendentes()
    {
        await using var contexto = banco.CriarContexto();

        (await contexto.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  Ida e volta dos objetos de valor
    // ------------------------------------------------------------------

    [Fact]
    public async Task Parlamentar_com_mandato_sobrevive_ao_banco()
    {
        var deputado = NovoDeputado();
        deputado.AdicionarMandato(57, new DateOnly(2023, 2, 1), null);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.Parlamentares
            .Include(p => p.Mandatos)
            .SingleAsync(p => p.Id == deputado.Id);

        lido.NomeParlamentar.ShouldBe("Acácio Favacho");
        lido.Uf.ShouldBe("AP");
        lido.Tipo.ShouldBe(TipoParlamentar.DeputadoFederal);
        lido.Mandatos.Single().Legislatura.ShouldBe(57);
        lido.Mandatos.Single().Ativo.ShouldBeTrue();
    }

    [Fact]
    public async Task Fornecedor_preserva_cnaes_secundarios_como_array_do_postgres()
    {
        var posto = NovoPosto(ProximoCnpjValido());

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Fornecedores.Add(posto);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.Fornecedores.SingleAsync(f => f.Id == posto.Id);

        lido.CnaesSecundarios.Count.ShouldBe(4);
        lido.PossuiCnae("5611204").ShouldBeTrue("o comportamento do dominio vale para dado do banco");
        lido.Endereco.Uf.ShouldBe("GO");
        lido.Endereco.CodigoMunicipioIbge.ShouldBe(5208707);
    }

    [Fact]
    public async Task Opcao_pelo_mei_desconhecida_continua_nula_apos_o_banco()
    {
        // Se o mapeamento colapsasse null em false, REG_KYB_004 silenciaria.
        var posto = NovoPosto(ProximoCnpjValido());

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Fornecedores.Add(posto);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.Fornecedores.SingleAsync(f => f.Id == posto.Id);

        lido.OptantePeloMei.ShouldBeNull();
        lido.OpcaoPeloMeiConhecida.ShouldBeFalse();
    }

    [Fact]
    public async Task Documento_do_fornecedor_volta_como_objeto_de_valor_valido()
    {
        var deputado = NovoDeputado();
        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}"));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Despesas.SingleAsync(d => d.Id == despesa.Id);

        lida.DocumentoFornecedor.Tipo.ShouldBe(TipoDocumento.Cnpj);
        lida.DocumentoFornecedor.EhValido.ShouldBeTrue(
            "a validacao de digito verificador roda de novo na leitura");
        lida.DocumentoFornecedor.Formatado.ShouldBe("36.826.246/0001-64");
        lida.Competencia.ShouldBe(Competencia.Criar(2026, 7));
        lida.NumeroDocumento.ShouldBe("8887");
        lida.ValorLiquido.ShouldBe(385.92m);
    }

    [Fact]
    public async Task Cpf_parcial_do_socio_preserva_os_seis_digitos()
    {
        var posto = NovoPosto(ProximoCnpjValido());
        var socio = Sociedade.Criar(
            posto.Id, "FULANO DE TAL PRIMEIRO SOCIO", "***456789**",
            "Sócio-Administrador", new DateOnly(2011, 7, 18));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Fornecedores.Add(posto);
            escrita.Sociedades.Add(socio);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.Sociedades.SingleAsync(s => s.Id == socio.Id);

        lido.Cpf.DigitosVisiveis.ShouldBe("456789");
        lido.Cpf.Mascarado.ShouldBe("***456789**");
        lido.Cpf.EhCompativelCom("12345678909").ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    //  Restricoes que so' o banco garante
    // ------------------------------------------------------------------

    [Fact]
    public async Task Indice_unico_do_hash_impede_a_mesma_nota_entrar_duas_vezes()
    {
        // A garantia de idempotencia da ingestao. Se esse indice nao existisse,
        // reprocessar um mes duplicaria todas as despesas dele.
        var deputado = NovoDeputado();
        var codigo = $"doc-{ProximoIdExterno()}";

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(Despesa.Criar(DadosDeDespesa(deputado.Id, codigo)));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();
        segunda.Despesas.Add(Despesa.Criar(DadosDeDespesa(deputado.Id, codigo)));

        var erro = await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());
        erro.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe("23505", "violacao de unicidade");
    }

    [Fact]
    public async Task Hash_sobrevive_ao_banco_como_dezesseis_bytes()
    {
        var deputado = NovoDeputado();
        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}"));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Despesas.SingleAsync(d => d.Id == despesa.Id);

        lida.HashUnico.Length.ShouldBe(16);
        lida.HashUnico.ShouldBe(despesa.HashUnico);
    }

    [Fact]
    public async Task Url_do_documento_e_reconstruida_a_partir_da_carteira()
    {
        // A coluna url_documento nao existe mais. Este teste prova que o endereco volta
        // identico ao que a origem trouxe — se falhar, a decisao de descartar a coluna
        // custou os links de todas as notas.
        const string Original =
            "https://www.camara.leg.br/cota-parlamentar/documentos/publ/1472/2023/7502460.pdf";

        var deputado = NovoDeputado();
        deputado.RegistrarNumeroDeCarteira(1472);

        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}") with
        {
            CodigoDocumento = "7502460",
            CodigoTipoDocumento = 0,
            DataDocumento = new DateTimeOffset(2023, 5, 10, 0, 0, 0, TimeSpan.Zero),
            UrlDocumento = Original,
        });

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Despesas.SingleAsync(d => d.Id == despesa.Id);
        var carteira = (await leitura.Parlamentares.SingleAsync(p => p.Id == deputado.Id))
            .NumeroCarteira;

        lida.TemDocumentoPublicado.ShouldBeTrue();
        lida.AnoDoArquivo.ShouldBeNull("2023 e' o mesmo ano da nota, entao nada a guardar");
        lida.UrlPublica(carteira).ShouldBe(Original);
    }

    [Fact]
    public async Task Ano_da_pasta_e_guardado_quando_diverge_do_ano_da_nota()
    {
        // 2,3% dos casos reais: nota de novembro ou dezembro arquivada no ano seguinte.
        // Sem esta coluna, o link apontaria para a pasta errada.
        const string Original =
            "https://www.camara.leg.br/cota-parlamentar/documentos/publ/1472/2024/7502461.pdf";

        var deputado = NovoDeputado();
        deputado.RegistrarNumeroDeCarteira(1472);

        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}") with
        {
            CodigoDocumento = "7502461",
            CodigoTipoDocumento = 0,
            DataDocumento = new DateTimeOffset(2023, 11, 20, 0, 0, 0, TimeSpan.Zero),
            UrlDocumento = Original,
        });

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Despesas.SingleAsync(d => d.Id == despesa.Id);

        lida.AnoDoArquivo.ShouldBe((short)2024);
        lida.UrlPublica(1472).ShouldBe(Original);
    }

    [Fact]
    public async Task Nota_sem_documento_publicado_nao_produz_link()
    {
        // 52% das notas de tipo 0 nao tem arquivo na origem. Devolver um link para elas
        // levaria o leitor a um 404 e a concluir que o documento foi retirado.
        var deputado = NovoDeputado();
        deputado.RegistrarNumeroDeCarteira(1472);

        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}") with
        {
            UrlDocumento = null,
        });

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Despesas.SingleAsync(d => d.Id == despesa.Id);

        lida.TemDocumentoPublicado.ShouldBeFalse();
        lida.UrlPublica(1472).ShouldBeNull();
    }

    [Fact]
    public async Task Despesa_com_fornecedor_sentinela_grava_sem_vinculo()
    {
        var deputado = NovoDeputado();
        var dados = DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}") with
        {
            DocumentoFornecedor = CpfCnpj.Criar("00000000000001"),
            NomeFornecedor = string.Empty,
        };

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(Despesa.Criar(dados));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();

        // Comparacao pelo objeto de valor inteiro, e nao por `.Numero`: com value
        // converter o EF Core mapeia CpfCnpj para uma coluna so' e nao consegue
        // navegar para dentro do tipo. Restricao real para as consultas da Etapa 04.
        var sentinela = CpfCnpj.Criar("00000000000001");
        var lida = await leitura.Despesas.SingleAsync(d =>
            d.DocumentoFornecedor == sentinela && d.ParlamentarId == deputado.Id);

        lida.FornecedorId.ShouldBeNull();
        lida.DocumentoFornecedor.EhSentinela.ShouldBeTrue();
        lida.ValorLiquido.ShouldBe(385.92m, "o gasto publico continua registrado");
    }

    [Fact]
    public async Task Mesma_regra_nao_marca_a_mesma_despesa_duas_vezes()
    {
        var deputado = NovoDeputado();
        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}"));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            escrita.FlagsAnomalias.Add(FlagAnomalia.Criar(
                despesa.Id, "REG_KYB_001", Gravidade.Alta, "Inconsistência financeira.", 1.0m));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();
        segunda.FlagsAnomalias.Add(FlagAnomalia.Criar(
            despesa.Id, "REG_KYB_001", Gravidade.Alta, "Inconsistência financeira.", 1.0m));

        await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());
    }

    [Fact]
    public async Task Grau_de_confianca_sobrevive_com_tres_casas_decimais()
    {
        var deputado = NovoDeputado();
        var despesa = Despesa.Criar(DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}"));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            escrita.FlagsAnomalias.Add(FlagAnomalia.Criar(
                despesa.Id, "REG_NET_001", Gravidade.Media,
                "Sócio compatível com doador de campanha.", 0.85m));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var flag = await leitura.FlagsAnomalias.SingleAsync(f => f.DespesaId == despesa.Id);

        flag.GrauDeConfianca.ShouldBe(0.85m);
        flag.EhDeterministica.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  Consultas que o motor e o ranking vao fazer
    // ------------------------------------------------------------------

    [Fact]
    public async Task Competencia_ordena_cronologicamente_no_sql()
    {
        // A codificacao AAAAMM tem de preservar a ordem no banco, senao a janela
        // historica de REG_TX_004 (spike) sai embaralhada.
        var deputado = NovoDeputado();
        var meses = new[]
        {
            Competencia.Criar(2025, 12),
            Competencia.Criar(2026, 1),
            Competencia.Criar(2026, 10),
        };

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);

            foreach (var mes in meses)
            {
                var dados = DadosDeDespesa(deputado.Id, $"doc-{ProximoIdExterno()}") with
                {
                    Competencia = mes,
                };
                escrita.Despesas.Add(Despesa.Criar(dados));
            }

            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var ordenadas = await leitura.Despesas
            .Where(d => d.ParlamentarId == deputado.Id)
            .OrderBy(d => d.Competencia)
            .Select(d => d.Competencia)
            .ToListAsync();

        ordenadas.ShouldBe(
            meses.ToList(),
            ignoreOrder: false,
            customMessage: "dezembro de 2025 vem antes de janeiro de 2026");
    }

    [Fact]
    public async Task Score_de_risco_e_um_para_um_com_parlamentar()
    {
        var deputado = NovoDeputado();

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.ScoresRisco.Add(ScoreRiscoParlamentar.Criar(deputado.Id));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();
        segunda.ScoresRisco.Add(ScoreRiscoParlamentar.Criar(deputado.Id));

        await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());
    }

    [Fact]
    public async Task Limite_de_cota_e_unico_por_uf_e_vigencia()
    {
        var vigencia = new DateOnly(2023, 1, 1);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.LimitesCota.Add(LimiteCota.Criar("AP", 44_632.46m, vigencia));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();
        segunda.LimitesCota.Add(LimiteCota.Criar("AP", 50_000m, vigencia));

        await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());
    }
}
