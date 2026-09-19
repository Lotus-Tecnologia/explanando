using Explanando.Domain.Estado;

namespace Explanando.Domain.Tests.Estado;

/// <summary>
/// A ponte entre os dois cadastros do Estado.
///
/// <para>O SIAPE identifica o órgão pelo <b>código SIAFI</b> (25201 é o Banco Central); o
/// SIORG, pelo <c>codigoUnidade</c> dele. Não existe tabela oficial ligando os dois — foi
/// procurada: o parâmetro <c>codigoSIAFI</c> é aceito e ignorado pela API do SIORG, e não há
/// campo de código SIAFI em resposta nenhuma.</para>
///
/// <para>Então a ponte é <b>pelo nome</b>, e por isso ela precisa de duas garantias que
/// estes testes fixam: a normalização só pode aproximar o que é a mesma casa, e o que não
/// casa com <b>exatidão</b> fica sem par em vez de ser adivinhado. Um palpite aqui não
/// produz um erro visível — produz um número de servidores atribuído ao órgão errado.</para>
/// </summary>
public sealed class PonteSiapeTests
{
    // ------------------------------------------------------------------
    //  A normalizacao aproxima a mesma casa
    // ------------------------------------------------------------------

    [Theory]
    // Sufixo de UF: o SIAPE carimba a unidade da federacao no fim do nome.
    [InlineData("Universidade Federal de Viçosa - MG", "Universidade Federal de Viçosa")]
    // "Fundacao Universidade Federal X" e "Universidade Federal de X" sao a mesma casa: a
    // forma juridica entrou no nome de umas e nao de outras.
    [InlineData("Fundação Universidade Federal Uberlândia", "Universidade Federal de Uberlândia")]
    // A familia dos institutos federais: o SIAPE encurta o nome legal inteiro.
    [InlineData(
        "Instituto Federal de São Paulo",
        "Instituto Federal de Educação, Ciência e Tecnologia de São Paulo")]
    // Acentuacao, caixa e pontuacao nao distinguem casa nenhuma.
    [InlineData("MINISTERIO DO MEIO AMBIENTE", "Ministério do Meio Ambiente")]
    public void Nomes_da_mesma_casa_normalizam_igual(string doSiape, string doSiorg)
    {
        PonteSiape.Normalizar(doSiape).ShouldBe(PonteSiape.Normalizar(doSiorg));
    }

    [Theory]
    // Duas casas diferentes que se parecem. Se estas colidissem, os servidores de uma
    // apareceriam somados aos da outra — e ninguem veria o erro na tela.
    [InlineData("Ministério da Educação", "Ministério da Cultura")]
    [InlineData("Universidade Federal do Pará", "Universidade Federal do Paraná")]
    [InlineData(
        "Instituto Federal de Educação, Ciência e Tecnologia do Sul de Minas Gerais",
        "Instituto Federal de Educação, Ciência e Tecnologia do Norte de Minas Gerais")]
    [InlineData("Universidade Federal de Goiás", "Universidade Federal de Jataí")]
    public void Casas_diferentes_nunca_normalizam_igual(string um, string outro)
    {
        PonteSiape.Normalizar(um).ShouldNotBe(PonteSiape.Normalizar(outro));
    }

    [Fact]
    public void Nome_vazio_normaliza_para_vazio_e_nao_casa_com_nada()
    {
        // Sem isto, dois registros sem nome casariam um com o outro.
        PonteSiape.Normalizar("").ShouldBeEmpty();
        PonteSiape.Normalizar("   ").ShouldBeEmpty();
        PonteSiape.Normalizar("- SP").ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  Os baldes, que nao sao orgaos
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(99991)] // "Estados / Municípios / Empresas" — 16.689 vínculos
    [InlineData(99990)] // "Estados / Municípios / Administração Direta Autárquica…"
    [InlineData(-20)]   // "Poder Executivo Federal", com código negativo
    public void Balde_de_agregacao_nunca_vira_no_do_grafo(int codigoSiafi)
    {
        // Sao rubricas de totalizacao do proprio SIAPE, nao orgaos. Liga-las a um no' do
        // grafo poria 16 mil servidores dentro de alguma caixa que nao os tem.
        PonteSiape.EhAgregado(codigoSiafi).ShouldBeTrue();
    }

    [Theory]
    [InlineData(25201)] // Banco Central
    [InlineData(26439)] // Instituto Federal de São Paulo
    [InlineData(17500)] // Ministério da Gestão
    public void Orgao_de_verdade_nao_e_balde(int codigoSiafi)
    {
        PonteSiape.EhAgregado(codigoSiafi).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  As excecoes curadas
    // ------------------------------------------------------------------

    [Fact]
    public void Toda_excecao_aponta_para_um_codigo_do_siorg_plausivel()
    {
        // Trava de digitacao: um zero a mais numa entrada curada liga um ministerio
        // inteiro a uma unidade aleatoria, e nada no sistema reclamaria.
        PonteSiape.Excecoes.ShouldNotBeEmpty();

        foreach (var (siafi, siorg) in PonteSiape.Excecoes)
        {
            siafi.ShouldBeGreaterThan(0, "código SIAFI de exceção precisa ser positivo");
            siorg.ShouldBeGreaterThan(0, $"o alvo SIORG de {siafi} precisa ser positivo");
        }
    }

    [Fact]
    public void Nenhuma_excecao_manda_dois_orgaos_para_a_mesma_unidade()
    {
        // Dois codigos SIAFI apontando para o mesmo no' somariam dois quadros de pessoal
        // num so'. Pode ser legitimo numa fusao de orgaos — mas precisa ser uma decisao
        // tomada, nao um descuido de copiar e colar.
        PonteSiape.Excecoes.Values.Distinct().Count()
            .ShouldBe(PonteSiape.Excecoes.Count, "há duas exceções apontando para o mesmo nó");
    }

    [Fact]
    public void Nenhum_balde_aparece_entre_as_excecoes()
    {
        // As duas listas dizem coisas opostas. Um codigo nas duas seria contradicao.
        foreach (var siafi in PonteSiape.Excecoes.Keys)
        {
            PonteSiape.EhAgregado(siafi).ShouldBeFalse(
                $"o código {siafi} está listado como balde E como exceção curada");
        }
    }
}
