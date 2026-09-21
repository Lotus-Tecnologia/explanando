using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Cadeiras;

/// <summary>
/// A cadeira de comando.
///
/// <para><b>O que estes testes guardam é a procedência.</b> Esta é a única tabela do
/// sistema que armazena nome de pessoa, e ela só se justifica porque cada linha carrega
/// de onde veio e quando foi vista. Uma cadeira sem fonte não é um dado incompleto — é um
/// nome de gente afirmado sem lastro, que é exatamente o que o produto existe para não
/// fazer.</para>
/// </summary>
public class CadeiraDeComandoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static NovaCadeira Nova() => new()
    {
        Poder = Poder.Executivo,
        Camada = CamadaDeComando.Cupula,
        Cargo = "Ministro de Estado da Fazenda",
        Ocupante = "Fulano de Tal",
        Fonte = "https://www.gov.br/planalto/exemplo",
        Origem = OrigemDaCadeira.Automatica,
    };

    [Fact]
    public void Uma_cadeira_sem_fonte_nao_existe()
    {
        // A regra que sustenta a excecao ao "nenhum nome no banco": nome de pessoa so'
        // entra acompanhado do endereco de onde o governo o publicou.
        var sem = Nova() with { Fonte = "   " };

        Assert.Throws<ArgumentException>(() => CadeiraDeComando.Criar(sem, "planalto", Agora));
    }

    [Fact]
    public void Uma_cadeira_sem_COLETOR_nao_existe()
    {
        // O coletor e' a chave de substituicao. Sem ele, a proxima coleta nao sabe o que
        // apagar e passa a DOBRAR as cadeiras a cada passada, silenciosamente.
        Assert.Throws<ArgumentException>(() => CadeiraDeComando.Criar(Nova(), "  ", Agora));
    }

    [Fact]
    public void Uma_cadeira_sem_cargo_nao_existe()
    {
        Assert.Throws<ArgumentException>(() =>
            CadeiraDeComando.Criar(Nova() with { Cargo = "" }, "planalto", Agora));
    }

    [Fact]
    public void A_data_de_verificacao_e_a_da_coleta__e_fica_guardada()
    {
        // Sem ela a tabela envelhece em silencio: o ministro troca e a nossa linha
        // continua igual, com a mesma cara de verdade.
        var cadeira = CadeiraDeComando.Criar(Nova(), "planalto", Agora);

        Assert.Equal(Agora, cadeira.VerificadoEm);
    }

    [Fact]
    public void Diz_ha_quantos_dias_nao_e_verificada()
    {
        var cadeira = CadeiraDeComando.Criar(Nova(), "planalto", Agora);

        Assert.Equal(45, cadeira.DiasSemVerificar(Agora.AddDays(45)));
    }

    [Fact]
    public void Relogio_para_tras_nao_produz_idade_negativa()
    {
        var cadeira = CadeiraDeComando.Criar(Nova(), "planalto", Agora);

        Assert.Equal(0, cadeira.DiasSemVerificar(Agora.AddDays(-10)));
    }

    [Fact]
    public void Ocupante_nulo_e_cadeira_VAGA__e_isso_e_um_fato()
    {
        // ADR-0009 aplicado a pessoas: "vaga" e "nao sabemos" sao coisas diferentes. Quem
        // nao sabe nao coleta a cadeira; quem coleta com ocupante nulo esta' dizendo que a
        // cadeira existe e esta' vazia.
        var cadeira = CadeiraDeComando.Criar(Nova() with { Ocupante = null }, "planalto", Agora);

        Assert.True(cadeira.Vaga);
        Assert.Null(cadeira.Ocupante);
    }

    [Fact]
    public void Texto_em_branco_no_ocupante_vira_nulo__e_nao_string_vazia()
    {
        // Uma celula vazia na fonte nao e' um ocupante chamado "". Deixar passar faria a
        // tela desenhar um retrato sem nome em vez de uma cadeira vaga.
        var cadeira = CadeiraDeComando.Criar(Nova() with { Ocupante = "   " }, "planalto", Agora);

        Assert.True(cadeira.Vaga);
    }

    [Fact]
    public void Espacos_ao_redor_do_nome_somem()
    {
        var cadeira = CadeiraDeComando.Criar(Nova() with { Ocupante = "  Fulano  " }, "planalto", Agora);

        Assert.Equal("Fulano", cadeira.Ocupante);
    }

    [Fact]
    public void O_codigo_do_grafo_e_opcional__e_a_cadeira_nao_e_descartada_sem_ele()
    {
        // E' o campo que liga as duas vistas. Exigi-lo faria a ponte por nome, que falha em
        // alguns casos, apagar ministerios do mapa de poder.
        var cadeira = CadeiraDeComando.Criar(Nova() with { CodigoSiorg = null }, "planalto", Agora);

        Assert.Null(cadeira.CodigoSiorg);
        Assert.Equal("Ministro de Estado da Fazenda", cadeira.Cargo);
    }
}

/// <summary>
/// O vocabulário das camadas.
///
/// <para>O teste que importa aqui é o de que <b>a mesma camada muda de nome conforme o
/// poder</b>. No Brasil "ministro" significa duas coisas muito diferentes, e um mapa do
/// Estado que chamasse as duas de "Cúpula" empurraria a tradução para o leitor.</para>
/// </summary>
public class VocabularioDasCadeirasTests
{
    [Fact]
    public void Cupula_tem_nome_proprio_em_cada_poder()
    {
        Assert.Equal(
            "Ministros de Estado",
            VocabularioDasCadeiras.De(Poder.Executivo, CamadaDeComando.Cupula));

        Assert.Equal(
            "Ministros do Supremo",
            VocabularioDasCadeiras.De(Poder.Judiciario, CamadaDeComando.Cupula));

        Assert.Equal(
            "Mesa Diretora",
            VocabularioDasCadeiras.De(Poder.Legislativo, CamadaDeComando.Cupula));
    }

    [Theory]
    [InlineData(Poder.Executivo)]
    [InlineData(Poder.Legislativo)]
    [InlineData(Poder.Judiciario)]
    [InlineData(Poder.FuncoesEssenciaisAJustica)]
    public void Toda_combinacao_de_poder_e_camada_tem_rotulo(Poder poder)
    {
        var rotulos = Enum.GetValues<CamadaDeComando>()
            .Select(camada => VocabularioDasCadeiras.De(poder, camada))
            .ToList();

        Assert.All(rotulos, rotulo => Assert.False(string.IsNullOrWhiteSpace(rotulo)));

        // Nenhum poder pode ter duas camadas com o mesmo nome: seriam duas faixas
        // indistinguiveis no desenho, e o leitor nao saberia qual esta' vendo.
        Assert.Equal(rotulos.Count, rotulos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_chave_e_estavel_e_nao_e_o_texto_de_tela()
    {
        // Se a faixa do desenho casasse com o rotulo, melhorar a redacao mudaria o layout
        // sem ninguem pedir e sem nenhum teste ficar vermelho.
        Assert.Equal("cupula", VocabularioDasCadeiras.ChaveDe(CamadaDeComando.Cupula));
        Assert.Equal("chefia", VocabularioDasCadeiras.ChaveDe(CamadaDeComando.Chefia));
    }

    [Fact]
    public void A_explicacao_da_ausencia_nomeia_o_MOTIVO__e_nao_so_o_buraco()
    {
        // "Sem dados" faria parecer desleixo nosso. O motivo real e' que o cadastro oficial
        // nao publica titular, e quem publica exige credencial.
        Assert.Contains("SIORG", VocabularioDasCadeiras.PorQueFaltaOcupante, StringComparison.Ordinal);
        Assert.Contains("Diário", VocabularioDasCadeiras.PorQueFaltaOcupante, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ressalva_da_curadoria_manda_conferir_a_data()
    {
        Assert.Contains("data de verificação", VocabularioDasCadeiras.RessalvaDaCuradoria, StringComparison.Ordinal);
    }
}
