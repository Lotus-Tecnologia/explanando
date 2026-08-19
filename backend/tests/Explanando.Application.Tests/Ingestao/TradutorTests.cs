using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Fornecedores;

namespace Explanando.Application.Tests.Ingestao;

/// <summary>
/// A traducao da fonte externa para o dominio e' onde os defeitos de dado viram defeitos
/// de sistema. Cada caso aqui saiu de algo observado no contrato real da Etapa 01.
/// </summary>
public sealed class TradutorTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static DespesaDaFonte DespesaReal(Action<Ajuste>? ajuste = null)
    {
        var a = new Ajuste();
        ajuste?.Invoke(a);

        return new DespesaDaFonte
        {
            Ano = a.Ano,
            Mes = a.Mes,
            TipoDespesa = "COMBUSTÍVEIS E LUBRIFICANTES.",
            CodigoDocumento = "8132888",
            NumeroDocumento = a.NumeroDocumento,
            DataDocumento = a.DataDocumento,
            ValorDocumento = a.ValorDocumento,
            ValorLiquido = a.ValorLiquido,
            ValorGlosa = a.ValorGlosa,
            UrlDocumento = "http://www.camara.leg.br/cota-parlamentar/nota",
            NomeFornecedor = "Auto Posto Bom Tempo Ltda",
            CnpjCpfFornecedor = a.CnpjCpfFornecedor,
            CodigoLote = 2253045,
            Parcela = 0,
        };
    }

    // ------------------------------------------------------------------
    //  Despesa
    // ------------------------------------------------------------------

    [Fact]
    public void Traduz_a_despesa_real_para_o_dominio()
    {
        var nova = Tradutor.ParaNovaDespesa(DespesaReal(), ParlamentarId);

        nova.Competencia.ShouldBe(Competencia.Criar(2026, 7));
        nova.NumeroDocumento.ShouldBe("8887");
        nova.ValorLiquido.ShouldBe(385.92m);
        nova.DocumentoFornecedor.Tipo.ShouldBe(TipoDocumento.Cnpj);
    }

    [Fact]
    public void Reconhece_o_cnpj_sentinela_sem_recusar_a_despesa()
    {
        var nova = Tradutor.ParaNovaDespesa(
            DespesaReal(a => a.CnpjCpfFornecedor = "00000000000001"),
            ParlamentarId);

        nova.DocumentoFornecedor.EhSentinela.ShouldBeTrue();
        nova.DocumentoFornecedor.PodeSerEnriquecido.ShouldBeFalse();
    }

    [Fact]
    public void Aceita_despesa_sem_fornecedor_informado()
    {
        var nova = Tradutor.ParaNovaDespesa(
            DespesaReal(a => a.CnpjCpfFornecedor = null),
            ParlamentarId);

        nova.DocumentoFornecedor.EhVazio.ShouldBeTrue();
    }

    [Fact]
    public void Deriva_a_competencia_da_data_quando_ano_e_mes_vem_zerados()
    {
        // Defesa contra registro antigo da base da Camara: se ano/mes vierem invalidos,
        // a data do documento ainda diz de que mes a despesa e'. Rejeitar seria perder
        // gasto publico por um campo de metadado.
        var nova = Tradutor.ParaNovaDespesa(
            DespesaReal(a =>
            {
                a.Ano = 0;
                a.Mes = 0;
            }),
            ParlamentarId);

        nova.Competencia.ShouldBe(Competencia.Criar(2026, 7), "veio de dataDocumento");
    }

    [Fact]
    public void Despesa_sem_numero_de_nota_recebe_marcador_explicito()
    {
        // REG_KYB_005 depende deste campo. String vazia silenciosa faria a regra
        // tratar "sem numero" como "numero baixo" e gerar falso positivo.
        var nova = Tradutor.ParaNovaDespesa(
            DespesaReal(a => a.NumeroDocumento = null),
            ParlamentarId);

        nova.NumeroDocumento.ShouldBe(Tradutor.NumeroDocumentoAusente);
    }

    [Fact]
    public void Preserva_a_glosa_informada_pela_fonte()
    {
        var nova = Tradutor.ParaNovaDespesa(
            DespesaReal(a =>
            {
                a.ValorDocumento = 262.99m;
                a.ValorLiquido = 250.00m;
                a.ValorGlosa = 12.99m;
            }),
            ParlamentarId);

        nova.ValorGlosa.ShouldBe(12.99m);
    }

    // ------------------------------------------------------------------
    //  Situacao cadastral — REG_KYB_002
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ATIVA", SituacaoCadastral.Ativa)]
    [InlineData("BAIXADA", SituacaoCadastral.Baixada)]
    [InlineData("SUSPENSA", SituacaoCadastral.Suspensa)]
    [InlineData("INAPTA", SituacaoCadastral.Inapta)]
    [InlineData("NULA", SituacaoCadastral.Nula)]
    [InlineData("ativa", SituacaoCadastral.Ativa)]
    public void Traduz_a_descricao_de_situacao_cadastral(string descricao, SituacaoCadastral esperada)
    {
        Tradutor.ParaSituacaoCadastral(descricao).ShouldBe(esperada);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ALGO QUE A RECEITA INVENTOU DEPOIS")]
    public void Situacao_desconhecida_nao_vira_ativa(string? descricao)
    {
        // Colapsar desconhecido em "Ativa" faria REG_KYB_002 se calar; colapsar em
        // "Baixada" acusaria empresa em dia. Desconhecido tem de ser desconhecido.
        Tradutor.ParaSituacaoCadastral(descricao).ShouldBe(SituacaoCadastral.Desconhecida);
    }

    // ------------------------------------------------------------------
    //  Empresa
    // ------------------------------------------------------------------

    [Fact]
    public void Traduz_a_empresa_real_para_o_dominio()
    {
        var novo = Tradutor.ParaNovoFornecedor(new EmpresaDaFonte
        {
            Cnpj = "36826246000164",
            RazaoSocial = "AUTO POSTO BOMTEMPO LTDA",
            NomeFantasia = "POSTO BOMTEMPO",
            CnaePrincipal = "4731800",
            CnaesSecundarios = ["3600601", "4732600"],
            DescricaoSituacaoCadastral = "ATIVA",
            DataInicioAtividade = new DateOnly(1991, 7, 23),
            OptantePeloMei = null,
            Municipio = "GOIANIA",
            Uf = "GO",
            CodigoMunicipioIbge = 5208707,
        });

        novo.Documento.Tipo.ShouldBe(TipoDocumento.Cnpj);
        novo.CnaesSecundarios.Count.ShouldBe(2);
        novo.SituacaoCadastral.ShouldBe(SituacaoCadastral.Ativa);
        novo.OptantePeloMei.ShouldBeNull("nulo da fonte continua nulo");
        novo.Endereco.Uf.ShouldBe("GO");
    }

    // ------------------------------------------------------------------
    //  Parlamentar
    // ------------------------------------------------------------------

    [Fact]
    public void Traduz_o_deputado_para_o_dominio()
    {
        var novo = Tradutor.ParaNovoParlamentar(new DeputadoDaFonte
        {
            IdExterno = 204379,
            NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
            NomeParlamentar = "Acácio Favacho",
            SiglaPartido = "MDB",
            Uf = "AP",
            FotoUrl = "https://www.camara.leg.br/internet/deputado/bandep/204379.jpg",
        });

        novo.IdExterno.ShouldBe(204379);
        novo.Tipo.ShouldBe(Domain.Parlamentares.TipoParlamentar.DeputadoFederal);
        novo.Uf.ShouldBe("AP");
    }

    private sealed class Ajuste
    {
        public int Ano { get; set; } = 2026;
        public int Mes { get; set; } = 7;
        public string? NumeroDocumento { get; set; } = "8887";
        public DateTimeOffset DataDocumento { get; set; } = new(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);
        public decimal ValorDocumento { get; set; } = 385.92m;
        public decimal ValorLiquido { get; set; } = 385.92m;
        public decimal ValorGlosa { get; set; }
        public string? CnpjCpfFornecedor { get; set; } = "36826246000164";
    }
}
