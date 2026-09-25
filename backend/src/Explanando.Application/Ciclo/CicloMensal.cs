using Explanando.Application.Auditoria;
using Explanando.Application.Ingestao;
using Explanando.Application.Retencao;
using Explanando.Domain.Regras;
using Explanando.Domain.Retencao;

namespace Explanando.Application.Ciclo;

/// <summary>O que o banco precisa responder para a passada mensal se agendar sozinha.</summary>
public interface IRepositorioDoCiclo
{
    /// <summary>
    /// A varredura <b>mais antiga</b> da base, ou nulo se ninguém foi varrido ainda.
    ///
    /// <para>Mais antiga, e não mais recente: ver <see cref="CicloDeAtualizacao.EstaVencido"/>.
    /// Trocar as duas faria a passada global nunca vencer enquanto houvesse uma varredura
    /// paga por mês.</para>
    /// </summary>
    Task<DateTimeOffset?> VarreduraMaisAntigaAsync(CancellationToken cancelamento = default);

    /// <summary>Todos os parlamentares da base, na ordem em que serão varridos.</summary>
    Task<IReadOnlyList<Guid>> IdsDosParlamentaresAsync(CancellationToken cancelamento = default);
}

/// <summary>
/// Decide <b>se</b> a passada mensal deve rodar.
///
/// <para>Separada da execução de propósito: a decisão é barata, precisa de duas dependências
/// e é onde mora a regra que importa; a execução é cara e é uma sequência de serviços que já
/// têm testes próprios. Juntas, a decisão só poderia ser testada montando o pipeline
/// inteiro.</para>
/// </summary>
public sealed class AgendaDoCicloMensal(IRepositorioDoCiclo repositorio, IRelogio relogio)
{
    public async Task<bool> EstaVencidoAsync(
        TimeSpan intervalo,
        CancellationToken cancelamento = default)
    {
        var maisAntiga = await repositorio
            .VarreduraMaisAntigaAsync(cancelamento)
            .ConfigureAwait(false);

        return CicloDeAtualizacao.EstaVencido(maisAntiga, relogio.Agora, intervalo);
    }
}

/// <summary>O que a passada mensal produziu.</summary>
public sealed record ResultadoDoCiclo
{
    /// <summary>Falso quando a base ainda estava fresca e nada foi feito.</summary>
    public bool Executou { get; init; }

    public ResultadoDaColeta? Coleta { get; init; }
    public int ParlamentaresVarridos { get; init; }
    public int FlagsEmitidas { get; init; }
    public IReadOnlyList<FalhaDeRegra> Falhas { get; init; } = [];
    public ResultadoDeRede? Rede { get; init; }
    public ResultadoDaRetencao? Retencao { get; init; }

    /// <summary>
    /// Verdadeiro quando a coleta parou no meio — fonte muda, listagem incompleta ou falha.
    ///
    /// <para>A varredura roda mesmo assim, e isso é deliberado: ela é idempotente e produz
    /// análise válida sobre o que já está no banco. Mas o ciclo <b>não</b> pode ser relatado
    /// como limpo, porque o dado ficou mais velho do que o resultado sugere.</para>
    /// </summary>
    public bool ColetaIncompleta { get; init; }
}

/// <summary>
/// A passada completa e gratuita de toda a base, que o <c>ADR-0010</c> §4 exige uma vez por
/// mês: coleta, varredura por parlamentar, passada de rede e consolidação.
///
/// <para>A ordem não é arbitrária. A coleta traz despesa nova; a varredura precisa dela para
/// avaliar; a passada de rede cruza parlamentares e só faz sentido depois que todos foram
/// varridos; e a consolidação soma o que existe no fim. Invertida, cada etapa trabalharia
/// sobre o estado anterior à que deveria alimentá-la.</para>
/// </summary>
public sealed class ServicoDeCicloMensal(
    AgendaDoCicloMensal agenda,
    ServicoDeColeta coleta,
    ServicoDeVarredura varredura,
    ServicoDeVarreduraDeRede rede,
    ServicoDeRetencao retencao,
    IRepositorioDoCiclo repositorio)
{
    public async Task<ResultadoDoCiclo> ExecutarSeVencidoAsync(
        int legislatura,
        TimeSpan intervalo,
        CancellationToken cancelamento = default)
    {
        var vencido = await agenda.EstaVencidoAsync(intervalo, cancelamento).ConfigureAwait(false);

        return vencido
            ? await ExecutarAsync(legislatura, cancelamento).ConfigureAwait(false)
            : new ResultadoDoCiclo { Executou = false };
    }

    public async Task<ResultadoDoCiclo> ExecutarAsync(
        int legislatura,
        CancellationToken cancelamento = default)
    {
        var resultadoDaColeta = await coleta
            .ColetarTudoAsync(legislatura, maxDeputados: null, cancelamento)
            .ConfigureAwait(false);

        var ids = await repositorio.IdsDosParlamentaresAsync(cancelamento).ConfigureAwait(false);

        var flags = 0;
        var falhas = new List<FalhaDeRegra>();

        foreach (var id in ids)
        {
            var resultado = await varredura.VarrerAsync(id, cancelamento).ConfigureAwait(false);

            flags += resultado.FlagsEmitidas;
            falhas.AddRange(resultado.Falhas);
        }

        // Fechamento: as regras que cruzam parlamentares não podem rodar com um só.
        var resultadoDeRede = await rede.VarrerAsync(cancelamento).ConfigureAwait(false);
        falhas.AddRange(resultadoDeRede.Falhas);

        // O score conta as flags de rede, que acabaram de mudar. Só quem ganhou ou perdeu
        // um achado de rede precisa ser recalculado — na base real, uma ou duas dezenas.
        foreach (var id in resultadoDeRede.ParlamentaresAfetados)
        {
            await varredura.VarrerAsync(id, cancelamento).ConfigureAwait(false);
        }

        // Consolida sem podar. A poda é destrutiva e nunca acontece por omissão — apagar
        // gasto público tem de ser um ato explícito, nunca efeito colateral de uma rotina.
        var resultadoDaRetencao = await retencao
            .AplicarAsync(JanelaDeRetencao.Ilimitada, cancelamento)
            .ConfigureAwait(false);

        return new ResultadoDoCiclo
        {
            Executou = true,
            Coleta = resultadoDaColeta,
            ParlamentaresVarridos = ids.Count,
            FlagsEmitidas = flags,
            Falhas = falhas,
            Rede = resultadoDeRede,
            Retencao = resultadoDaRetencao,
            ColetaIncompleta =
                resultadoDaColeta.ListagemIncompleta
                || resultadoDaColeta.InterrompidaPorFonteSuspeita
                || resultadoDaColeta.Falhas.Count > 0,
        };
    }
}
