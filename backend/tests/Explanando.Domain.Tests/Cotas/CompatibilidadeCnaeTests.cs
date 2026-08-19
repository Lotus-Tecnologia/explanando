using Explanando.Domain.Cotas;

namespace Explanando.Domain.Tests.Cotas;

/// <summary>
/// A tabela de compatibilidade entre CNAE e tipo de despesa e' o insumo de REG_KYB_001.
///
/// A postura e' <b>permissiva</b> por decisao de produto: cada falso positivo e' uma
/// imputacao indevida numa pagina publica indexada, com nome e foto de uma pessoa real.
/// Na duvida, o motor se cala. Ver ADR-0007.
/// </summary>
public sealed class CompatibilidadeCnaeTests
{
    private const string Combustivel = "COMBUSTÍVEIS E LUBRIFICANTES.";
    private const string Consultoria = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.";
    private const string Seguranca = "SERVIÇO DE SEGURANÇA PRESTADO POR EMPRESA ESPECIALIZADA.";

    // ------------------------------------------------------------------
    //  Cobertura da tabela
    // ------------------------------------------------------------------

    [Fact]
    public void Cobre_os_24_tipos_de_despesa_que_a_camara_publica()
    {
        // A lista veio de /referencias/deputados/tipoDespesa (docs/contratos/).
        // Se a Camara criar um tipo novo, o teste nao quebra — mas o tipo desconhecido
        // faz a regra se abster, e isso e' visivel em ConhecidoOTipo.
        CompatibilidadeCnae.TiposCobertos.Count.ShouldBe(24);
    }

    [Fact]
    public void Toda_entrada_da_tabela_tem_ao_menos_um_prefixo()
    {
        foreach (var tipo in CompatibilidadeCnae.TiposCobertos)
        {
            CompatibilidadeCnae.PrefixosPara(tipo).ShouldNotBeEmpty(
                $"'{tipo}' sem prefixo nenhum faria a regra acusar todo fornecedor");
        }
    }

    [Fact]
    public void Prefixos_sao_numericos_e_curtos_o_bastante_para_serem_permissivos()
    {
        foreach (var tipo in CompatibilidadeCnae.TiposCobertos)
        {
            foreach (var prefixo in CompatibilidadeCnae.PrefixosPara(tipo))
            {
                prefixo.ShouldAllBe(caractere => char.IsDigit(caractere));
                prefixo.Length.ShouldBeInRange(2, 5,
                    "prefixo de 7 digitos seria codigo exato, e exigiria acerto perfeito");
            }
        }
    }

    // ------------------------------------------------------------------
    //  Casos claros de compatibilidade
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("4731800", "posto de combustível")]
    [InlineData("4732600", "comércio de lubrificantes")]
    [InlineData("4681801", "atacadista de combustíveis")]
    public void Posto_de_combustivel_e_compativel_com_despesa_de_combustivel(string cnae, string caso)
    {
        CompatibilidadeCnae.Avaliar(Combustivel, [cnae]).ShouldBe(ResultadoDeCompatibilidade.Compativel, caso);
    }

    [Theory]
    [InlineData("6204000", "consultoria em TI")]
    [InlineData("7020400", "consultoria em gestão")]
    [InlineData("7112000", "serviços de engenharia")]
    public void Consultoria_tecnica_aceita_a_familia_de_servicos_profissionais(string cnae, string caso)
    {
        CompatibilidadeCnae.Avaliar(Consultoria, [cnae]).ShouldBe(ResultadoDeCompatibilidade.Compativel, caso);
    }

    [Fact]
    public void Basta_um_cnae_secundario_bater_para_ser_compativel()
    {
        // Caso real: o posto da amostra tem "bares" como CNAE secundário. Exigir que
        // o principal batesse marcaria empresas legítimas de atividade mista.
        var doPostoReal = new[] { "5611204", "3600601", "4731800" };

        CompatibilidadeCnae.Avaliar(Combustivel, doPostoReal)
            .ShouldBe(ResultadoDeCompatibilidade.Compativel);
    }

    // ------------------------------------------------------------------
    //  O unico caso em que a regra fala
    // ------------------------------------------------------------------

    [Fact]
    public void Empresa_de_ramo_claramente_alheio_e_incompativel()
    {
        // O exemplo do documento de Motor de Regras: cota de consultoria técnica
        // paga a empresa cujo CNAE é comércio varejista de bebidas.
        CompatibilidadeCnae.Avaliar(Consultoria, ["4723700"])
            .ShouldBe(ResultadoDeCompatibilidade.Incompativel);
    }

    [Fact]
    public void Papelaria_prestando_seguranca_e_incompativel()
    {
        CompatibilidadeCnae.Avaliar(Seguranca, ["4761003"])
            .ShouldBe(ResultadoDeCompatibilidade.Incompativel);
    }

    // ------------------------------------------------------------------
    //  Abstencao — a postura permissiva do ADR-0007
    // ------------------------------------------------------------------

    [Fact]
    public void Abstem_se_quando_o_fornecedor_nao_tem_cnae()
    {
        // Sem CNAE nao ha o que comparar. Acusar aqui seria acusar a ausencia de dado.
        CompatibilidadeCnae.Avaliar(Combustivel, [])
            .ShouldBe(ResultadoDeCompatibilidade.SemBaseParaAvaliar);
    }

    [Fact]
    public void Abstem_se_quando_o_tipo_de_despesa_e_desconhecido()
    {
        // A Camara pode criar um tipo novo. Ate a tabela ser atualizada, silencio.
        CompatibilidadeCnae.Avaliar("TIPO QUE A CAMARA INVENTOU DEPOIS", ["4731800"])
            .ShouldBe(ResultadoDeCompatibilidade.SemBaseParaAvaliar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Abstem_se_quando_o_tipo_vem_vazio(string? vazio)
    {
        CompatibilidadeCnae.Avaliar(vazio, ["4731800"])
            .ShouldBe(ResultadoDeCompatibilidade.SemBaseParaAvaliar);
    }

    [Fact]
    public void Cnae_ilegivel_nao_conta_como_incompativel()
    {
        CompatibilidadeCnae.Avaliar(Combustivel, ["", "  ", "abc"])
            .ShouldBe(ResultadoDeCompatibilidade.SemBaseParaAvaliar);
    }

    [Fact]
    public void Compara_ignorando_pontuacao_do_cnae()
    {
        CompatibilidadeCnae.Avaliar(Combustivel, ["47.31-8/00"])
            .ShouldBe(ResultadoDeCompatibilidade.Compativel);
    }

    [Fact]
    public void Compara_o_tipo_de_despesa_ignorando_acento_e_caixa()
    {
        // O texto vem da API com acento e em caixa alta; nao pode depender disso.
        CompatibilidadeCnae.Avaliar("combustiveis e lubrificantes.", ["4731800"])
            .ShouldBe(ResultadoDeCompatibilidade.Compativel);
    }

    // ------------------------------------------------------------------
    //  Proteção contra tabela restritiva demais
    // ------------------------------------------------------------------

    [Fact]
    public void Locacao_de_veiculos_aceita_tanto_locadora_quanto_transportadora()
    {
        // Fretamento com motorista é transporte (49), locação pura é aluguel (77).
        // Uma tabela que só aceitasse uma das duas marcaria metade dos contratos legítimos.
        const string locacao = "LOCAÇÃO OU FRETAMENTO DE VEÍCULOS AUTOMOTORES";

        CompatibilidadeCnae.Avaliar(locacao, ["7711000"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
        CompatibilidadeCnae.Avaliar(locacao, ["4929901"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
    }

    [Fact]
    public void Divulgacao_parlamentar_aceita_grafica_agencia_e_producao_audiovisual()
    {
        const string divulgacao = "DIVULGAÇÃO DA ATIVIDADE PARLAMENTAR.";

        CompatibilidadeCnae.Avaliar(divulgacao, ["1813001"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
        CompatibilidadeCnae.Avaliar(divulgacao, ["7311400"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
        CompatibilidadeCnae.Avaliar(divulgacao, ["5911199"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
        CompatibilidadeCnae.Avaliar(divulgacao, ["6391700"]).ShouldBe(ResultadoDeCompatibilidade.Compativel);
    }

    [Fact]
    public void Manutencao_de_escritorio_e_deliberadamente_ampla()
    {
        // Aluguel, energia, água, limpeza, internet e reforma cabem todos aqui.
        const string escritorio = "MANUTENÇÃO DE ESCRITÓRIO DE APOIO À ATIVIDADE PARLAMENTAR";

        foreach (var cnae in new[] { "6810201", "3511501", "3600601", "8121400", "6110801", "4321500" })
        {
            CompatibilidadeCnae.Avaliar(escritorio, [cnae])
                .ShouldBe(ResultadoDeCompatibilidade.Compativel, $"CNAE {cnae}");
        }
    }
}
