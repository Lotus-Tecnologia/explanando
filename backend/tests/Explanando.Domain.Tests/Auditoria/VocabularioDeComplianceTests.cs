using Explanando.Domain.Comum;

namespace Explanando.Domain.Tests.Auditoria;

/// <summary>
/// O ADR-0004 proibe vocabulario acusatorio em toda a superficie do produto. Uma regra
/// escrita so' em documento erode: alguem escreve "fraude" numa descricao de flag seis
/// meses depois e ninguem percebe ate a notificacao judicial chegar.
///
/// Aqui ela vira codigo, e o motor de regras nao consegue emitir uma flag que a viole.
/// </summary>
public sealed class VocabularioDeComplianceTests
{
    [Theory]
    [InlineData("Indício de fraude na nota fiscal")]
    [InlineData("Possível crime contra a administração")]
    [InlineData("Suspeita de corrupção")]
    [InlineData("Padrão típico de lavagem de dinheiro")]
    [InlineData("Desvio de verba pública identificado")]
    [InlineData("FRAUDE")]
    [InlineData("Lavagem")]
    public void Reprova_texto_com_termo_proibido(string texto)
    {
        VocabularioDeCompliance.ContemTermoProibido(texto).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Inconsistência financeira identificada pelo motor de regras")]
    [InlineData("Atipicidade de volume financeiro em relação à média histórica")]
    [InlineData("Fator de risco elevado: fornecedor com situação cadastral irregular")]
    [InlineData("Alerta de compliance: CNAE incompatível com o tipo de despesa")]
    [InlineData("Valor acima de dois desvios padrão da série")]
    public void Aprova_o_vocabulario_tecnico_obrigatorio(string texto)
    {
        VocabularioDeCompliance.ContemTermoProibido(texto).ShouldBeFalse();
    }

    [Fact]
    public void Ignora_acentuacao_ao_procurar_o_termo()
    {
        // "corrupcao" sem cedilha nem til tem de ser pego igual.
        VocabularioDeCompliance.ContemTermoProibido("indicio de corrupcao").ShouldBeTrue();
    }

    [Fact]
    public void Nao_reprova_palavra_que_apenas_contem_o_termo_como_pedaco()
    {
        // "descriminação" contém "crim", mas não é o termo proibido "crime".
        // Casar por substring cega tornaria a regra inutilizável na prática.
        VocabularioDeCompliance.ContemTermoProibido("descrição do documento").ShouldBeFalse();
        VocabularioDeCompliance.ContemTermoProibido("valor discriminado na nota").ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Texto_vazio_nao_viola_nada(string? vazio)
    {
        VocabularioDeCompliance.ContemTermoProibido(vazio).ShouldBeFalse();
    }
}
