using Explanando.Domain.Comum;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// O catálogo existe porque o código da regra vazava para a tela.
///
/// <para><c>REG_TX_002</c> é identificador estável de metodologia para quem consome a API —
/// e ruído para o cidadão que abriu a página do próprio deputado. A frase que a substitui
/// não é a de <see cref="IRegraAnomalia.Descricao"/>, que nasceu para documentação e
/// telemetria e fala a linguagem de quem escreveu a regra ("cliente pioneiro", "teto da
/// cota da UF"). O catálogo guarda a redação <b>de leitor</b>, separada de propósito.</para>
///
/// <para>Separada, ela pode divergir em silêncio — daí os dois testes de deriva. E, por
/// passar a ser texto que o produto afirma sobre pessoa nomeada, ela entra sob o
/// <c>ADR-0004</c> como qualquer descrição de flag.</para>
/// </summary>
public sealed class CatalogoDeRegrasTests
{
    /// <summary>As 12 do motor por parlamentar mais a passada de rede.</summary>
    private static IReadOnlyList<string> CodigosQueOMotorConhece() =>
    [
        .. MotorDeRegras.Padrao().Regras.Select(regra => regra.Codigo),
        new RegraConcentracaoPartidaria().Codigo,
    ];

    // ------------------------------------------------------------------
    //  Deriva — nos dois sentidos
    // ------------------------------------------------------------------

    [Fact]
    public void Toda_regra_que_o_motor_executa_tem_redacao_de_leitor()
    {
        // Regra nova sem entrada aqui voltaria a imprimir o codigo cru na pagina, que e'
        // exatamente o defeito que o catalogo veio fechar.
        foreach (var codigo in CodigosQueOMotorConhece())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(CatalogoDeRegras.DescricaoDe(codigo)),
                $"{codigo} roda no motor e ficou sem redação de leitor no catálogo.");
        }
    }

    [Fact]
    public void O_catalogo_nao_descreve_regra_que_ninguem_executa()
    {
        // O outro sentido da deriva: regra removida do motor e esquecida aqui deixaria o
        // bloco de cobertura prometendo uma analise que nao acontece mais.
        var doMotor = CodigosQueOMotorConhece();

        foreach (var codigo in CatalogoDeRegras.Codigos)
        {
            Assert.Contains(codigo, doMotor);
        }
    }

    // ------------------------------------------------------------------
    //  ADR-0004 — a redacao passou a ser texto do produto
    // ------------------------------------------------------------------

    [Fact]
    public void Nenhuma_redacao_usa_termo_acusatorio()
    {
        foreach (var codigo in CatalogoDeRegras.Codigos)
        {
            var descricao = CatalogoDeRegras.DescricaoDe(codigo);

            Assert.False(
                VocabularioDeCompliance.ContemTermoProibido(descricao),
                $"A redação de {codigo} usa termo que o ADR-0004 proíbe: {descricao}");
        }
    }

    [Fact]
    public void Nenhuma_redacao_grita_em_caixa_alta()
    {
        // ADR-0013: caixa alta e' enfase, e enfase sobre achado descritivo e' imputacao
        // pela tipografia. Vale para o texto tanto quanto para o CSS.
        foreach (var codigo in CatalogoDeRegras.Codigos)
        {
            var descricao = CatalogoDeRegras.DescricaoDe(codigo)!;
            var letras = descricao.Where(char.IsLetter).ToArray();

            Assert.False(
                letras.Length > 0 && letras.All(char.IsUpper),
                $"A redação de {codigo} está inteira em caixa alta.");
        }
    }

    // ------------------------------------------------------------------
    //  O que o catalogo faz quando nao sabe
    // ------------------------------------------------------------------

    [Fact]
    public void Codigo_desconhecido_nao_inventa_texto()
    {
        // Devolver o proprio codigo, ou uma frase generica, faria a tela exibir algo que
        // ninguem redigiu. Nulo obriga a borda a decidir — e a decisao dela e' nao desenhar.
        Assert.Null(CatalogoDeRegras.DescricaoDe("REG_XX_999"));
        Assert.Null(CatalogoDeRegras.DescricaoDe(""));
        Assert.Null(CatalogoDeRegras.DescricaoDe(null));
    }
}
