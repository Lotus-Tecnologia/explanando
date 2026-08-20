using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Tests.Despesas;

/// <summary>
/// Modelada a partir do contrato real capturado na Etapa 01
/// (docs/contratos/camara-despesas-pagina-100.json), nao da especificacao original —
/// que previa um unico campo `Valor` e nao previa `numDocumento`.
/// </summary>
public sealed class DespesaTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Espelha uma despesa real: abastecimento de R$ 385,92 em julho de 2026.</summary>
    private static NovaDespesa Real(Action<NovaDespesaBuilder>? ajuste = null)
    {
        var builder = new NovaDespesaBuilder();
        ajuste?.Invoke(builder);
        return builder.Construir();
    }

    [Fact]
    public void Preserva_o_numero_da_nota_fiscal()
    {
        // Sem este campo REG_KYB_005 (nota inicial/sequencial) nao existe.
        // A especificacao original nao o modelava; a API entrega, 100% preenchido.
        var despesa = Despesa.Criar(Real(b => b.NumeroDocumento = "8887"));

        despesa.NumeroDocumento.ShouldBe("8887");
    }

    [Fact]
    public void Guarda_os_tres_valores_separadamente()
    {
        // A especificacao previa um `Valor` so'. O contrato real tem tres, e eles
        // significam coisas diferentes: o cobrado, o pago e o cortado.
        var despesa = Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = 262.99m;
            b.ValorLiquido = 250.00m;
            b.ValorGlosa = 12.99m;
        }));

        despesa.ValorDocumento.ShouldBe(262.99m);
        despesa.ValorLiquido.ShouldBe(250.00m);
        despesa.ValorGlosa.ShouldBe(12.99m);
        despesa.TemGlosa.ShouldBeTrue();
    }

    [Fact]
    public void Valor_liquido_e_a_base_do_score()
    {
        // E' o que o erario efetivamente pagou. Usar o valor do documento inflaria
        // o ranking de quem teve despesa glosada — justamente quem foi barrado.
        var despesa = Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = 262.99m;
            b.ValorLiquido = 250.00m;
            b.ValorGlosa = 12.99m;
        }));

        despesa.ValorParaScore.ShouldBe(250.00m);
    }

    [Fact]
    public void Despesa_sem_glosa_nao_marca_glosa()
    {
        Despesa.Criar(Real()).TemGlosa.ShouldBeFalse();
    }

    [Fact]
    public void Rejeita_glosa_inconsistente_com_os_valores()
    {
        var erro = Should.Throw<ArgumentException>(() => Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = 100m;
            b.ValorLiquido = 90m;
            b.ValorGlosa = 50m;   // deveria ser 10
        })));

        erro.Message.ShouldContain("glosa");
    }

    // ------------------------------------------------------------------
    //  Estorno — 4,3% dos registros reais
    // ------------------------------------------------------------------

    [Fact]
    public void Aceita_valor_negativo_como_estorno()
    {
        // A primeira coleta real recusou 1.223 registros por "valor negativo". São
        // cancelamentos de passagem aérea (SIGEPA), dado legítimo.
        var estorno = Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = -1846.10m;
            b.ValorLiquido = -1846.10m;
        }));

        estorno.EhEstorno.ShouldBeTrue();
        estorno.ValorLiquido.ShouldBe(-1846.10m);
    }

    [Fact]
    public void Estorno_abate_o_valor_que_entra_no_score()
    {
        // É o ponto todo: rejeitar estorno não perderia só um registro, inflaria o gasto.
        // Quem comprou passagem de R$ 1.846 e cancelou apareceria tendo gasto os R$ 1.846.
        var compra = Despesa.Criar(Real(b => b.ValorLiquido = 1846.10m));
        var estorno = Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = -1846.10m;
            b.ValorLiquido = -1846.10m;
        }));

        (compra.ValorParaScore + estorno.ValorParaScore).ShouldBe(0m);
    }

    [Fact]
    public void Despesa_comum_nao_e_estorno()
    {
        Despesa.Criar(Real()).EhEstorno.ShouldBeFalse();
    }

    [Fact]
    public void Estorno_com_documento_e_liquido_diferentes_e_aceito()
    {
        // Caso real da amostra: documento=-45,00 e liquido=-90,00. A aritmética de glosa
        // não se aplica a estorno, então a validação de consistência não pode disparar.
        var estorno = Despesa.Criar(Real(b =>
        {
            b.ValorDocumento = -45.00m;
            b.ValorLiquido = -90.00m;
        }));

        estorno.EhEstorno.ShouldBeTrue();
    }

    [Fact]
    public void Glosa_continua_nao_podendo_ser_negativa()
    {
        // Glosa é um corte aplicado ao valor, não um lançamento: negativa não significa nada.
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Despesa.Criar(Real(b => b.ValorGlosa = -10m)));
    }

    [Fact]
    public void Aceita_fornecedor_sentinela_sem_recusar_a_despesa()
    {
        // 2 em 100 despesas reais usam o CNPJ 00000000000001. Descartar essas
        // despesas esconderia gasto publico — o oposto do proposito do produto.
        var despesa = Despesa.Criar(Real(b =>
        {
            b.DocumentoFornecedor = CpfCnpj.Criar("00000000000001");
            b.NomeFornecedor = "";
        }));

        despesa.ShouldNotBeNull();
        despesa.DocumentoFornecedor.EhSentinela.ShouldBeTrue();
        despesa.FornecedorId.ShouldBeNull("nao ha pessoa juridica a associar");
    }

    // ---------------------------------------------------------------------
    //  Idempotencia. A ingestao roda de novo sobre os mesmos meses varias vezes;
    //  o hash e' o que impede a mesma nota de entrar duas vezes no banco.
    // ---------------------------------------------------------------------

    [Fact]
    public void Hash_e_deterministico_para_a_mesma_despesa()
    {
        var primeira = Despesa.Criar(Real());
        var segunda = Despesa.Criar(Real());

        segunda.HashUnico.ShouldBe(primeira.HashUnico);
    }

    [Fact]
    public void Hash_nao_depende_de_campos_que_a_camara_pode_corrigir()
    {
        // Nome de fornecedor e URL do documento mudam sem que a despesa seja outra.
        // Se entrassem no hash, uma correcao na origem viraria duplicata no banco.
        var original = Despesa.Criar(Real());
        var comCorrecao = Despesa.Criar(Real(b =>
        {
            b.NomeFornecedor = "AUTO POSTO BOM TEMPO LTDA - ME";
            b.UrlDocumento = "http://outra.url/nota";
        }));

        comCorrecao.HashUnico.ShouldBe(original.HashUnico);
    }

    [Fact]
    public void Hash_difere_quando_o_documento_e_outro()
    {
        var primeira = Despesa.Criar(Real(b => b.CodigoDocumento = "8132888"));
        var outra = Despesa.Criar(Real(b => b.CodigoDocumento = "8132889"));

        outra.HashUnico.ShouldNotBe(primeira.HashUnico);
    }

    [Fact]
    public void Hash_difere_entre_parlamentares_com_o_mesmo_documento()
    {
        var deUm = Despesa.Criar(Real());
        var deOutro = Despesa.Criar(Real(b => b.ParlamentarId = Guid.NewGuid()));

        deOutro.HashUnico.ShouldNotBe(deUm.HashUnico);
    }

    [Fact]
    public void Hash_difere_entre_parcelas_do_mesmo_lote()
    {
        var primeira = Despesa.Criar(Real(b => b.Parcela = 0));
        var segunda = Despesa.Criar(Real(b => b.Parcela = 1));

        segunda.HashUnico.ShouldNotBe(primeira.HashUnico);
    }

    [Fact]
    public void Hash_cabe_na_coluna_de_64_caracteres()
    {
        Despesa.Criar(Real()).HashUnico.Length.ShouldBe(64, "SHA-256 em hexadecimal");
    }

    /// <summary>Builder so' de teste, para que cada caso altere apenas o que lhe importa.</summary>
    private sealed class NovaDespesaBuilder
    {
        public Guid ParlamentarId { get; set; } = DespesaTests.ParlamentarId;
        public CpfCnpj DocumentoFornecedor { get; set; } = CpfCnpj.Criar("36826246000164");
        public string NomeFornecedor { get; set; } = "Auto Posto Bom Tempo Ltda";
        public Competencia Competencia { get; set; } = Competencia.Criar(2026, 7);
        public string TipoDespesa { get; set; } = "COMBUSTÍVEIS E LUBRIFICANTES.";
        public string CodigoDocumento { get; set; } = "8132888";
        public string NumeroDocumento { get; set; } = "8887";
        public DateTimeOffset DataDocumento { get; set; } = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
        public decimal ValorDocumento { get; set; } = 385.92m;
        public decimal ValorLiquido { get; set; } = 385.92m;
        public decimal ValorGlosa { get; set; }
        public string? UrlDocumento { get; set; } = "http://www.camara.leg.br/cota-parlamentar/nota";
        public long CodigoLote { get; set; } = 2253045;
        public int Parcela { get; set; }

        public NovaDespesa Construir() => new()
        {
            ParlamentarId = ParlamentarId,
            DocumentoFornecedor = DocumentoFornecedor,
            NomeFornecedor = NomeFornecedor,
            Competencia = Competencia,
            TipoDespesa = TipoDespesa,
            CodigoDocumento = CodigoDocumento,
            NumeroDocumento = NumeroDocumento,
            DataDocumento = DataDocumento,
            ValorDocumento = ValorDocumento,
            ValorLiquido = ValorLiquido,
            ValorGlosa = ValorGlosa,
            UrlDocumento = UrlDocumento,
            CodigoLote = CodigoLote,
            Parcela = Parcela,
        };
    }
}
