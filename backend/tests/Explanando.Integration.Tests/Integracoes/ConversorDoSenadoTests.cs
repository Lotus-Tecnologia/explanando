using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// A esteira do Senado contra os quinze registros reais de
/// <c>docs/contratos/senado-ceaps-amostra.json</c>.
///
/// <para>Os quinze foram escolhidos <b>pelas armadilhas</b>, não pelo caminho feliz: CPF
/// mascarado, CNPJ de 13 dígitos, tipo nulo, data no ano 202, estorno e número de documento
/// não numérico. Um fixture de caminho feliz testaria o que já funciona.</para>
///
/// <para>A prova que importa é de ponta a ponta: cada registro atravessa o conversor <b>e</b>
/// o domínio, até virar uma <c>Despesa</c> persistível. Testar só o conversor deixaria de
/// fora exatamente a fronteira onde dado sujo costuma explodir.</para>
/// </summary>
public sealed class ConversorDoSenadoTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>
    /// A amostra atravessa a desserializacao de producao, e nao um atalho de teste: assim
    /// um [JsonPropertyName] errado quebra aqui em vez de virar campo nulo em producao.
    /// </summary>
    private static readonly IReadOnlyList<DespesaDaFonte> Amostra =
        Explanando.Infrastructure.Integracoes.Senado.ConversorDoSenado.ConverterLote(
            Fixtures.Ler("senado-ceaps-amostra.json"));

    /// <summary>Os quinze registros da amostra, como o xUnit precisa recebê-los.</summary>
    public static TheoryData<int> Indices
    {
        get
        {
            var dados = new TheoryData<int>();

            for (var i = 0; i < Amostra.Count; i++)
            {
                dados.Add(i);
            }

            return dados;
        }
    }

    private static DespesaDaFonte Converter(int indice) => Amostra[indice];

    [Fact]
    public void A_amostra_tem_os_quinze_registros_escolhidos_pelas_armadilhas()
    {
        Amostra.Count.ShouldBe(15);
    }

    // ------------------------------------------------------------------
    //  Nenhum dos quinze pode quebrar a esteira
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Indices))]
    public void Todo_registro_da_amostra_vira_despesa_persistivel(int indice)
    {
        // O teste que a Etapa da ingestão do Senado inteira depende: dado sujo entra e sai
        // como Despesa válida, sem exceção fatal em lugar nenhum.
        var daFonte = Converter(indice);

        var despesa = Should.NotThrow(() =>
            Despesa.Criar(Tradutor.ParaNovaDespesa(daFonte, ParlamentarId)));

        despesa.HashUnico.Length.ShouldBe(16);
        despesa.TipoDespesa.ShouldNotBeNullOrWhiteSpace();
        despesa.NumeroDocumento.ShouldNotBeNullOrWhiteSpace();
        despesa.CodigoDocumento.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Nenhum_registro_do_senado_afirma_glosa(int indice)
    {
        // ADR-0012 §2. Zero diria que houve conferência e nada foi cortado — afirmação que
        // ninguém fez. A UI consulta GlosaConhecida antes de desenhar a quebra de valores.
        var despesa = Despesa.Criar(Tradutor.ParaNovaDespesa(Converter(indice), ParlamentarId));

        despesa.ValorGlosa.ShouldBeNull();
        despesa.GlosaConhecida.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Nenhum_registro_do_senado_promete_comprovante(int indice)
    {
        // ADR-0012 §1. O Senado não publica link para a nota em campo nenhum.
        var despesa = Despesa.Criar(Tradutor.ParaNovaDespesa(Converter(indice), ParlamentarId));

        despesa.TemDocumentoPublicado.ShouldBeFalse();
        despesa.UrlPublica(numeroCarteira: 1472).ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Indices))]
    public void Nenhuma_data_e_anterior_a_constituicao(int indice)
    {
        // O registro com data 0202-02-24 cai aqui. Uma data no ano 202 na série histórica
        // envenena REG_TX_004 e qualquer gráfico.
        Converter(indice).DataDocumento.Year.ShouldBeGreaterThanOrEqualTo(1988);
    }

    // ------------------------------------------------------------------
    //  Cada armadilha, nomeada
    // ------------------------------------------------------------------

    [Fact]
    public void Cpf_mascarado_vira_documento_nao_enriquecivel()
    {
        // "457.***.***-00" tem cinco dígitos visíveis. Não dá para consultar cadastro com
        // isso, e as regras de KYB precisam se abster — não inventar fornecedor.
        var mascarados = Amostra
            .Select((d, _) => d)
            .Where(d => d.CnpjCpfFornecedor?.Contains('*', StringComparison.Ordinal) == true)
            .ToList();

        mascarados.ShouldNotBeEmpty("a amostra foi escolhida para conter estes casos");

        foreach (var despesa in mascarados)
        {
            CpfCnpj.Criar(despesa.CnpjCpfFornecedor).PodeSerEnriquecido.ShouldBeFalse();
        }
    }

    [Fact]
    public void Cnpj_malformado_nao_e_completado_com_zero()
    {
        // Testado contra os onze distintos da base real: só um passa no dígito verificador
        // depois do padding. Preencher inventaria um CNPJ que existe e é de outra empresa.
        var curtos = Amostra
            .Select((d, _) => d)
            .Select(d => d.CnpjCpfFornecedor ?? string.Empty)
            .Where(doc => !doc.Contains('*', StringComparison.Ordinal)
                && doc.Count(char.IsAsciiDigit) is > 0 and not 14)
            .ToList();

        curtos.ShouldNotBeEmpty("a amostra foi escolhida para conter estes casos");
        curtos.ShouldAllBe(doc => !CpfCnpj.Criar(doc).PodeSerEnriquecido);
    }

    [Fact]
    public void Tipo_nulo_vira_marcador_legivel_e_nao_string_vazia()
    {
        var semTipo = Amostra
            .Select((d, _) => d)
            .Where(d => d.TipoDespesa.Contains("não informado", StringComparison.Ordinal))
            .ToList();

        semTipo.ShouldNotBeEmpty("a amostra foi escolhida para conter estes casos");

        // E o marcador nao esta na tabela curada, entao REG_KYB_001 se abstem — que e' o
        // comportamento certo para "a origem nao disse o que foi comprado".
        Domain.Cotas.CompatibilidadeCnae
            .ConheceOTipo(semTipo[0].TipoDespesa)
            .ShouldBeFalse();
    }

    [Fact]
    public void Estorno_chega_com_valor_negativo_e_o_dominio_aceita()
    {
        // Rejeitar estorno inflaria o gasto: uma passagem comprada e cancelada apareceria
        // como gasta. Foi o defeito mais caro da primeira coleta da Câmara.
        var estornos = Amostra
            .Select((d, _) => d)
            .Where(d => d.ValorLiquido < 0)
            .ToList();

        estornos.ShouldNotBeEmpty("a amostra foi escolhida para conter estes casos");

        foreach (var daFonte in estornos)
        {
            var despesa = Despesa.Criar(Tradutor.ParaNovaDespesa(daFonte, ParlamentarId));
            despesa.EhEstorno.ShouldBeTrue();
        }
    }

    [Fact]
    public void Documento_nao_numerico_e_preservado_como_veio()
    {
        // Aparecem "08/2025" e "00000266". REG_KYB_005 procura série sequencial baixa;
        // "arrumar" esses valores criaria numeração que a origem não usou.
        var naoNumericos = Amostra
            .Select((d, _) => d)
            .Where(d => !string.IsNullOrEmpty(d.NumeroDocumento)
                && !d.NumeroDocumento.All(char.IsAsciiDigit))
            .ToList();

        naoNumericos.ShouldNotBeEmpty("a amostra foi escolhida para conter estes casos");
    }

    [Fact]
    public void O_codigo_do_documento_e_o_id_do_lancamento_e_nao_o_numero_da_nota()
    {
        // `documento` repete entre fornecedores — varias notas "1" — e nao serve de chave.
        // O hash de idempotencia depende disso: chave repetida colapsaria despesas
        // distintas numa so'.
        var hashes = Amostra
            .Select(d => Despesa.Criar(Tradutor.ParaNovaDespesa(d, ParlamentarId)))
            .Select(d => Convert.ToHexString(d.HashUnico))
            .ToList();

        hashes.Distinct().Count().ShouldBe(hashes.Count, "cada lançamento tem hash próprio");
    }

    [Fact]
    public void Nota_fiscal_eletronica_conta_como_documento_fiscal()
    {
        // REG_KYB_005 só faz sentido sobre nota fiscal. O Senado entrega o tipo como texto
        // legível, não código, então o mapeamento é onde isso se decide.
        var comTipoFiscal = Amostra
            .Select((d, _) => d)
            .Where(d => d.CodigoTipoDocumento is 0 or 4)
            .ToList();

        foreach (var daFonte in comTipoFiscal)
        {
            Despesa.Criar(Tradutor.ParaNovaDespesa(daFonte, ParlamentarId))
                .EhDocumentoFiscal.ShouldBeTrue();
        }
    }
}
