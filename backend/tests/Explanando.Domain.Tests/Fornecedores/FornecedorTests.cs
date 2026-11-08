using Explanando.Domain.Comum;
using Explanando.Domain.Fornecedores;

namespace Explanando.Domain.Tests.Fornecedores;

/// <summary>
/// O fornecedor concentra o que a Etapa 01 confirmou que a BrasilAPI entrega e que a
/// especificacao original nao modelava: CNAEs secundarios, situacao cadastral com data,
/// data de inicio de atividade, opcao pelo MEI e endereco estruturado.
///
/// Cada metodo de consulta aqui existe para uma regra especifica do motor. As perguntas
/// vivem no dominio; a Etapa 04 so' as encadeia.
/// </summary>
public sealed class FornecedorTests
{
    private static Fornecedor PostoReal(Action<NovoFornecedorBuilder>? ajuste = null)
    {
        var builder = new NovoFornecedorBuilder();
        ajuste?.Invoke(builder);
        return Fornecedor.Criar(builder.Construir());
    }

    // ------------------------------------------------------------------
    //  REG_KYB_001 — CNAE divergente do tipo de despesa
    // ------------------------------------------------------------------

    [Fact]
    public void Reconhece_o_cnae_principal()
    {
        PostoReal().PossuiCnae("4731800").ShouldBeTrue();
    }

    [Fact]
    public void Reconhece_tambem_os_cnaes_secundarios()
    {
        // O posto real tem 4 secundarios, entre eles "bares" e "distribuicao de agua".
        // Ignorar os secundarios geraria falso positivo em REG_KYB_001.
        var posto = PostoReal();

        posto.PossuiCnae("4732600").ShouldBeTrue("comercio varejista de lubrificantes");
        posto.PossuiCnae("5611204").ShouldBeTrue("bares");
        posto.PossuiCnae("6201500").ShouldBeFalse("desenvolvimento de software nao esta na lista");
    }

    [Fact]
    public void Compara_cnae_ignorando_pontuacao()
    {
        PostoReal().PossuiCnae("47.31-8-00").ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    //  REG_KYB_002 — nota emitida apos baixa ou irregularidade cadastral
    // ------------------------------------------------------------------

    [Fact]
    public void Empresa_ativa_nunca_esta_irregular()
    {
        var posto = PostoReal();

        posto.EstavaIrregularEm(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero))
            .ShouldBeFalse();
    }

    [Fact]
    public void Empresa_baixada_esta_irregular_para_notas_posteriores_a_baixa()
    {
        var baixada = PostoReal(b =>
        {
            b.SituacaoCadastral = SituacaoCadastral.Baixada;
            b.DataSituacaoCadastral = new DateOnly(2026, 1, 15);
        });

        baixada.EstavaIrregularEm(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero))
            .ShouldBeTrue("a nota e' de julho e a baixa foi em janeiro");
    }

    [Fact]
    public void Empresa_baixada_nao_contamina_notas_anteriores_a_baixa()
    {
        // Se a empresa fechou depois, a nota antiga era legitima. Sinalizar seria
        // acusar retroativamente — exatamente o que ADR-0004 manda evitar.
        var baixada = PostoReal(b =>
        {
            b.SituacaoCadastral = SituacaoCadastral.Baixada;
            b.DataSituacaoCadastral = new DateOnly(2026, 1, 15);
        });

        baixada.EstavaIrregularEm(new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero))
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData(SituacaoCadastral.Suspensa)]
    [InlineData(SituacaoCadastral.Inapta)]
    [InlineData(SituacaoCadastral.Nula)]
    public void Suspensa_inapta_e_nula_tambem_contam_como_irregular(SituacaoCadastral situacao)
    {
        var irregular = PostoReal(b =>
        {
            b.SituacaoCadastral = situacao;
            b.DataSituacaoCadastral = new DateOnly(2026, 1, 15);
        });

        irregular.EstavaIrregularEm(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero))
            .ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    //  REG_KYB_003 — empresa recem-nascida faturando alto
    // ------------------------------------------------------------------

    [Fact]
    public void Calcula_a_idade_da_empresa_na_data_da_nota()
    {
        var novinha = PostoReal(b => b.DataInicioAtividade = new DateOnly(2026, 5, 1));

        novinha.DiasDeAtividadeAte(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero))
            .ShouldBe(66);
    }

    [Fact]
    public void Empresa_de_1991_nao_e_recem_nascida()
    {
        var antiga = PostoReal();

        antiga.DiasDeAtividadeAte(new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero))
            .ShouldNotBeNull()
            .ShouldBeGreaterThan(90 * 100);
    }

    [Fact]
    public void Idade_desconhecida_quando_a_fonte_nao_informa_abertura()
    {
        var semData = PostoReal(b => b.DataInicioAtividade = null);

        semData.DiasDeAtividadeAte(DateTimeOffset.UnixEpoch).ShouldBeNull(
            "sem data de abertura a regra tem de se abster, nao chutar");
    }

    // ------------------------------------------------------------------
    //  REG_KYB_004 — MEI faturando acima do teto legal
    // ------------------------------------------------------------------

    [Fact]
    public void Sabe_quando_e_mei()
    {
        PostoReal(b => b.OptantePeloMei = true).EhMei.ShouldBeTrue();
        PostoReal(b => b.OptantePeloMei = false).EhMei.ShouldBeFalse();
    }

    [Fact]
    public void Opcao_pelo_mei_desconhecida_nao_e_tratada_como_falso()
    {
        // No CNPJ real da amostra, `opcao_pelo_mei` veio null. Tratar null como
        // "nao e' MEI" faria REG_KYB_004 silenciar sem que ninguem percebesse.
        var indefinido = PostoReal(b => b.OptantePeloMei = null);

        indefinido.EhMei.ShouldBeFalse();
        indefinido.OpcaoPeloMeiConhecida.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, "2025-03-01", null, "2024-06-10", false)]
    [InlineData(true, "2025-03-01", null, "2025-03-01", true)]
    [InlineData(false, "2020-01-01", "2024-12-31", "2024-12-31", true)]
    [InlineData(false, "2020-01-01", "2024-12-31", "2025-01-02", false)]
    [InlineData(true, null, null, "2025-06-10", null)]
    [InlineData(null, null, null, "2025-06-10", null)]
    [InlineData(false, null, null, "2025-06-10", false)]
    public void Era_mei_na_data_segue_a_opcao_e_a_exclusao(
        bool? optante, string? opcao, string? exclusao, string data, bool? esperado)
    {
        var fornecedor = PostoReal(b =>
        {
            b.OptantePeloMei = optante;
            b.DataOpcaoPeloMei = opcao is null ? null : DateOnly.Parse(opcao, System.Globalization.CultureInfo.InvariantCulture);
            b.DataExclusaoDoMei = exclusao is null ? null : DateOnly.Parse(exclusao, System.Globalization.CultureInfo.InvariantCulture);
        });

        fornecedor.EraMeiEm(DateOnly.Parse(data, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(esperado);
    }

    [Fact]
    public void Datas_do_mei_podem_ser_registradas_depois()
    {
        var fornecedor = PostoReal(b => b.OptantePeloMei = true);

        fornecedor.RegistrarDatasDoMei(true, new DateOnly(2026, 3, 30), null);

        fornecedor.DataOpcaoPeloMei.ShouldBe(new DateOnly(2026, 3, 30));
        fornecedor.EraMeiEm(new DateOnly(2026, 4, 1)).ShouldBe(true);
    }

    // ------------------------------------------------------------------
    //  REG_GEO_002 — gasto fora da base eleitoral (via UF do fornecedor)
    // ------------------------------------------------------------------

    [Fact]
    public void Expoe_a_uf_para_confronto_com_a_base_eleitoral()
    {
        // Caso real da amostra: deputado do AP abastecendo em Goiania/GO.
        var posto = PostoReal();

        posto.Endereco.Uf.ShouldBe("GO");
        posto.Endereco.Municipio.ShouldBe("GOIANIA");
    }

    [Fact]
    public void Normaliza_a_uf_para_maiusculas()
    {
        PostoReal(b => b.Uf = "go").Endereco.Uf.ShouldBe("GO");
    }

    // ------------------------------------------------------------------
    //  Identidade
    // ------------------------------------------------------------------

    [Fact]
    public void Guarda_o_documento_como_objeto_de_valor()
    {
        PostoReal().Documento.Tipo.ShouldBe(TipoDocumento.Cnpj);
        PostoReal().Documento.Formatado.ShouldBe("36.826.246/0001-64");
    }

    [Fact]
    public void Recusa_ser_criado_a_partir_de_documento_nao_enriquecivel()
    {
        // Sentinela e invalido nunca viram registro de fornecedor: nao ha empresa ali.
        Should.Throw<ArgumentException>(() =>
            PostoReal(b => b.Documento = CpfCnpj.Criar("00000000000001")));
    }

    private sealed class NovoFornecedorBuilder
    {
        public CpfCnpj Documento { get; set; } = CpfCnpj.Criar("36826246000164");
        public string RazaoSocial { get; set; } = "AUTO POSTO BOMTEMPO LTDA";
        public string? NomeFantasia { get; set; } = "POSTO BOMTEMPO";
        public string CnaePrincipal { get; set; } = "4731800";
        public IReadOnlyList<string> CnaesSecundarios { get; set; } =
            ["3600601", "4732600", "4784900", "5611204"];
        public SituacaoCadastral SituacaoCadastral { get; set; } = SituacaoCadastral.Ativa;
        public DateOnly? DataSituacaoCadastral { get; set; } = new(2005, 9, 24);
        public DateOnly? DataInicioAtividade { get; set; } = new(1991, 7, 23);
        public bool? OptantePeloMei { get; set; }
        public DateOnly? DataOpcaoPeloMei { get; set; }
        public DateOnly? DataExclusaoDoMei { get; set; }
        public string? Porte { get; set; } = "DEMAIS";
        public string Uf { get; set; } = "GO";
        public string Municipio { get; set; } = "GOIANIA";

        public NovoFornecedor Construir() => new()
        {
            Documento = Documento,
            RazaoSocial = RazaoSocial,
            NomeFantasia = NomeFantasia,
            CnaePrincipal = CnaePrincipal,
            CnaesSecundarios = CnaesSecundarios,
            SituacaoCadastral = SituacaoCadastral,
            DataSituacaoCadastral = DataSituacaoCadastral,
            DataInicioAtividade = DataInicioAtividade,
            OptantePeloMei = OptantePeloMei,
            DataOpcaoPeloMei = DataOpcaoPeloMei,
            DataExclusaoDoMei = DataExclusaoDoMei,
            Porte = Porte,
            Endereco = new EnderecoFornecedor
            {
                Logradouro = "RODOVIA GO070",
                Numero = "S/N",
                Bairro = "FAZ CAVEIRAS",
                Municipio = Municipio,
                CodigoMunicipioIbge = 5208707,
                Uf = Uf,
                Cep = "74470440",
            },
        };
    }
}
