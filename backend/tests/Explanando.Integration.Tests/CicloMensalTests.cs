using Explanando.Application.Ciclo;
using Explanando.Application.Ingestao;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Repositorios;

namespace Explanando.Integration.Tests;

/// <summary>
/// A agenda da passada mensal contra Postgres real.
///
/// <para>O que precisa de prova aqui não é a aritmética de datas — essa está coberta por
/// teste unitário em <c>CicloDeAtualizacaoTests</c>. É a <b>consulta</b>: ela tem de trazer o
/// parlamentar mais atrasado, e trazer o mais recente por engano faria o ciclo global nunca
/// vencer enquanto houvesse uma varredura paga por mês.</para>
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class CicloMensalTests(BancoDeTestes banco)
{
    private const int PisoAmostral = 100;

    private static int _sequencia;

    private sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora { get; } = agora;
    }

    private static readonly DateTimeOffset Hoje =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A faixa de <c>IdExterno</c> desta classe.
    ///
    /// <para>O banco e' compartilhado pela colecao, e o indice em (id_externo, tipo) e'
    /// unico. Cada classe tem a sua faixa, e as tomadas sao 400k, 500k, 600k, 700k, 800k,
    /// 900k, 10M, 20M e 30M — a de 800k mora em <c>Integracoes/IngestaoPontaAPontaTests</c>,
    /// numa subpasta, que e' como ela passou despercebida na primeira busca.</para>
    ///
    /// <para>Escolher faixa em uso derruba a suite com 23505 <b>so' quando os testes rodam
    /// juntos</b> — isolados, passam. E' o mesmo tropeco registrado na Etapa 04 §5.6, e a
    /// forma de nao repeti-lo e' conferir com
    /// <c>grep -rn "_sequencia) + " tests/Explanando.Integration.Tests/</c>, recursivo.</para>
    /// </summary>
    private const int FaixaDeIdExterno = 1_100_000;

    private static Parlamentar Deputado(string nome) =>
        Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = Interlocked.Increment(ref _sequencia) + FaixaDeIdExterno,
            NomeCivil = $"{nome} da Silva",
            NomeParlamentar = nome,
            SiglaPartido = "XYZ",
            Uf = "SP",
            Tipo = TipoParlamentar.DeputadoFederal,
        });

    /// <summary>Semeia parlamentares com varreduras nas datas informadas.</summary>
    private async Task<AgendaDoCicloMensal> SemearAsync(params DateTimeOffset[] varreduras)
    {
        await using var contexto = banco.CriarContexto();

        // A base e' compartilhada pela colecao: cada teste limpa o que os outros deixaram,
        // senao o "mais antigo" viria de outro cenario.
        contexto.ScoresRisco.RemoveRange(contexto.ScoresRisco);
        await contexto.SaveChangesAsync();

        foreach (var quando in varreduras)
        {
            var parlamentar = Deputado($"Deputado {quando:yyyyMMddHHmmss}");
            contexto.Parlamentares.Add(parlamentar);

            var score = ScoreRiscoParlamentar.Criar(parlamentar.Id);
            score.Recalcular([], [], quando, PisoAmostral);

            contexto.ScoresRisco.Add(score);
        }

        await contexto.SaveChangesAsync();

        return new AgendaDoCicloMensal(
            new RepositorioDoCiclo(banco.CriarContexto()),
            new RelogioFixo(Hoje));
    }

    [Fact]
    public async Task Olha_para_o_parlamentar_mais_atrasado_e_nao_para_o_mais_recente()
    {
        // O caso que a regra existe para pegar: uma varredura paga hoje, e todo o resto
        // parado há um ano. Se a consulta trouxesse a MAIS RECENTE, a base pareceria
        // atualizada e o ciclo global nunca rodaria — que é exatamente a distorção de
        // frescor que a passada mensal existe para corrigir.
        var agenda = await SemearAsync(Hoje, Hoje.AddYears(-1), Hoje.AddYears(-1));

        (await agenda.EstaVencidoAsync(CicloDeAtualizacao.IntervaloPadrao)).ShouldBeTrue();
    }

    [Fact]
    public async Task Nao_vence_quando_a_base_inteira_esta_fresca()
    {
        var agenda = await SemearAsync(Hoje.AddDays(-2), Hoje.AddDays(-3), Hoje);

        (await agenda.EstaVencidoAsync(CicloDeAtualizacao.IntervaloPadrao)).ShouldBeFalse();
    }

    [Fact]
    public async Task Base_sem_varredura_nenhuma_vence()
    {
        var agenda = await SemearAsync();

        (await agenda.EstaVencidoAsync(CicloDeAtualizacao.IntervaloPadrao)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_data_mais_antiga_e_a_que_a_consulta_devolve()
    {
        var maisAntiga = Hoje.AddDays(-90);
        await SemearAsync(Hoje, maisAntiga, Hoje.AddDays(-5));

        var repositorio = new RepositorioDoCiclo(banco.CriarContexto());

        var encontrada = await repositorio.VarreduraMaisAntigaAsync();

        encontrada.ShouldNotBeNull();
        encontrada.Value.ShouldBe(maisAntiga, TimeSpan.FromSeconds(1));
    }
}
