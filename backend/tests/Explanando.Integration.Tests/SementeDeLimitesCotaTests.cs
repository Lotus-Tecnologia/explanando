using Explanando.Domain.Cotas;
using Explanando.Infrastructure.Persistencia.Semente;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// Estes valores alimentam REG_TX_002, que sinaliza "gasto cravado no teto da cota" com
/// nome e foto numa pagina publica. Um numero errado aqui vira acusacao indevida — por isso
/// a tabela e' testada como dado, nao so' como mecanismo.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class SementeDeLimitesCotaTests(BancoDeTestes banco)
{
    private static readonly string[] TodasAsUfs =
    [
        "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT",
        "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
    ];

    // ------------------------------------------------------------------
    //  A tabela em si
    // ------------------------------------------------------------------

    [Fact]
    public void Cobre_as_27_unidades_da_federacao_sem_faltar_nem_sobrar()
    {
        var naSemente = SementeDeLimitesCota.Valores.Select(v => v.Uf).ToList();

        naSemente.Count.ShouldBe(27);
        naSemente.Distinct(StringComparer.Ordinal).Count().ShouldBe(27, "nenhuma UF repetida");
        naSemente.OrderBy(uf => uf, StringComparer.Ordinal)
            .ShouldBe(TodasAsUfs.OrderBy(uf => uf, StringComparer.Ordinal));
    }

    [Fact]
    public void Valores_estao_na_faixa_plausivel_publicada()
    {
        // Guarda contra erro de digitacao de ordem de grandeza: um valor com um zero a
        // mais faria a regra nunca disparar; com um a menos, dispararia sempre.
        foreach (var (uf, valor) in SementeDeLimitesCota.Valores)
        {
            valor.ShouldBeInRange(40_000m, 60_000m, $"valor de {uf} fora da faixa publicada");
        }
    }

    [Fact]
    public void Distrito_federal_tem_o_menor_teto_e_roraima_o_maior()
    {
        // O calculo da cota leva em conta a passagem aerea ate Brasilia. O DF nao precisa
        // de voo; RR e' o estado mais distante. Se esta ordem se inverter, a tabela foi
        // preenchida errado.
        var menor = SementeDeLimitesCota.Valores.MinBy(v => v.ValorMensal);
        var maior = SementeDeLimitesCota.Valores.MaxBy(v => v.ValorMensal);

        menor.Uf.ShouldBe("DF");
        menor.ValorMensal.ShouldBe(41_612.55m);
        maior.Uf.ShouldBe("RR");
        maior.ValorMensal.ShouldBe(58_474.70m);
    }

    [Fact]
    public void Estados_do_norte_tem_teto_maior_que_os_do_sudeste()
    {
        // Coerencia geografica: distancia de Brasilia manda no valor.
        decimal Valor(string uf) => SementeDeLimitesCota.Valores.First(v => v.Uf == uf).ValorMensal;

        Valor("RR").ShouldBeGreaterThan(Valor("SP"));
        Valor("AC").ShouldBeGreaterThan(Valor("MG"));
        Valor("AM").ShouldBeGreaterThan(Valor("RJ"));
        Valor("GO").ShouldBeGreaterThan(Valor("DF"), "GO é vizinho, mas ainda exige deslocamento");
    }

    // ------------------------------------------------------------------
    //  O mecanismo
    // ------------------------------------------------------------------

    [Fact]
    public async Task Grava_os_27_limites_no_banco()
    {
        await using var contexto = banco.CriarContexto();

        await SementeDeLimitesCota.AplicarAsync(contexto);

        await using var leitura = banco.CriarContexto();
        var gravados = await leitura.LimitesCota
            .Where(limite => limite.VigenciaInicio == SementeDeLimitesCota.VigenciaAssumida)
            .ToListAsync();

        gravados.Count.ShouldBe(27);
        gravados.Single(l => l.Uf == "AP").ValorMensal.ShouldBe(55_929.26m);
    }

    [Fact]
    public async Task Aplicar_de_novo_nao_duplica_nem_sobrescreve()
    {
        // A semente roda a cada partida da aplicação; precisa ser inofensiva.
        await using var contexto = banco.CriarContexto();

        await SementeDeLimitesCota.AplicarAsync(contexto);
        var inseridosNaSegunda = await SementeDeLimitesCota.AplicarAsync(contexto);

        inseridosNaSegunda.ShouldBe(0);

        await using var leitura = banco.CriarContexto();
        (await leitura.LimitesCota
            .CountAsync(l => l.VigenciaInicio == SementeDeLimitesCota.VigenciaAssumida))
            .ShouldBe(27);
    }

    // ------------------------------------------------------------------
    //  Abstencao fora da vigencia coberta
    // ------------------------------------------------------------------

    [Fact]
    public async Task Despesa_anterior_a_vigencia_fica_sem_teto_configurado()
    {
        // Os valores de 2023 a 2025 ainda nao foram levantados. REG_TX_002 tem de se
        // abster sobre essas despesas, e nao compara-las ao teto de 2026.
        await using var contexto = banco.CriarContexto();
        await SementeDeLimitesCota.AplicarAsync(contexto);

        await using var leitura = banco.CriarContexto();
        var limitesDeSp = await leitura.LimitesCota.Where(l => l.Uf == "SP").ToListAsync();

        limitesDeSp.ShouldNotBeEmpty();
        limitesDeSp.ShouldAllBe(l => !l.VigenteEm(new DateOnly(2024, 6, 15)));
        limitesDeSp.ShouldContain(l => l.VigenteEm(new DateOnly(2026, 7, 6)));
    }
}
