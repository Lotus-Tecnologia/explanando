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
    [InlineData("Valor acima de dois desvios padrão da série histórica")]
    [InlineData("Desvio Padrão elevado no período")]
    [InlineData("desvio padrao de 3,2 no trimestre")]
    public void Desvio_padrao_e_permitido_apesar_de_conter_termo_proibido(string texto)
    {
        // "desvio" é proibido por imputar conduta, mas "Desvio Padrão" consta entre os
        // termos OBRIGATÓRIOS do mesmo documento de Negócios. Sem esta exceção, o
        // vocabulário recomendado seria recusado pelo próprio domínio.
        VocabularioDeCompliance.ContemTermoProibido(texto).ShouldBeFalse();
    }

    [Fact]
    public void A_excecao_do_desvio_padrao_nao_libera_desvio_sozinho()
    {
        VocabularioDeCompliance.ContemTermoProibido("houve desvio de verba").ShouldBeTrue();
        VocabularioDeCompliance.ContemTermoProibido("desvio padrão alto, e também desvio de recursos")
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Texto_vazio_nao_viola_nada(string? vazio)
    {
        VocabularioDeCompliance.ContemTermoProibido(vazio).ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  O lava-jato: dado da origem nao e' vocabulario nosso
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("MOREIRA LAVAGEM E POLIMENTOS LTDA")]
    [InlineData("ESQUILOS LAVAGEM AUTOMOTIVA LTDA")]
    [InlineData("RODO SERVICE LAVAGEM E CONSERVACAO LTDA")]
    public void Razao_social_citada_nao_derruba_a_descricao(string razaoSocial)
    {
        // Os tres derrubaram REG_KYB_001 seis vezes na varredura dos 513 deputados, porque
        // "lavagem" esta na lista por causa de lavagem de dinheiro. Seis achados legitimos
        // se perderam por causa do ramo de atividade do fornecedor.
        var descricao =
            $"Alerta de compliance: a despesa de 'COMBUSTÍVEIS E LUBRIFICANTES.' foi paga a "
            + $"'{razaoSocial}', cujo ramo registrado na Receita Federal (CNAE 4520005) "
            + "pertence a outra atividade econômica.";

        VocabularioDeCompliance.ContemTermoProibido(descricao).ShouldBeFalse();
        Should.NotThrow(() =>
            VocabularioDeCompliance.GarantirVocabularioTecnico(descricao, "descricao"));
    }

    [Fact]
    public void Termo_proibido_FORA_das_aspas_continua_sendo_recusado()
    {
        // O que a guarda existe para pegar. A isencao vale so' para o dado citado.
        VocabularioDeCompliance
            .ContemTermoProibido("Indício de fraude na despesa paga a 'EMPRESA X LTDA'.")
            .ShouldBeTrue();
    }

    [Fact]
    public void Aspa_sem_par_fecha_no_fim_do_texto()
    {
        // Razao social truncada deixaria o resto exposto e reintroduziria o problema pela
        // borda.
        VocabularioDeCompliance
            .ContemTermoProibido("Despesa paga a 'MOREIRA LAVAGEM E POLIMENTOS")
            .ShouldBeFalse();
    }

    [Fact]
    public void A_isencao_e_por_aspas_e_nao_por_lista_de_palavras()
    {
        // Nao adianta catalogar "lavagem automotiva": a proxima empresa se chama outra
        // coisa. A isencao precisa valer para qualquer nome que a origem entregue.
        VocabularioDeCompliance
            .ContemTermoProibido("Fornecedor 'DESVIO E ROUBO TRANSPORTES LTDA' com CNAE alheio.")
            .ShouldBeFalse();
    }

    [Fact]
    public void Texto_sem_aspa_nenhuma_segue_a_regra_de_sempre()
    {
        VocabularioDeCompliance.ContemTermoProibido("Padrão de lavagem de dinheiro").ShouldBeTrue();
        VocabularioDeCompliance.ContemTermoProibido("Inconsistência financeira").ShouldBeFalse();
    }
}
