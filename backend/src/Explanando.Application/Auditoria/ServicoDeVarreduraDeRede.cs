using Explanando.Domain.Auditoria;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;

namespace Explanando.Application.Auditoria;

/// <summary>Porta da passada de rede. Lê a base inteira, não um parlamentar.</summary>
public interface IRepositorioDeRede
{
    Task<IReadOnlyList<Parlamentar>> ObterParlamentaresAsync(
        CancellationToken cancelamento = default);

    Task<IReadOnlyList<Despesa>> ObterDespesasAsync(CancellationToken cancelamento = default);

    Task<IReadOnlyList<Fornecedor>> ObterFornecedoresAsync(
        CancellationToken cancelamento = default);

    /// <summary>
    /// Apaga as flags de rede da passada anterior, por código de regra, e devolve de quais
    /// parlamentares elas eram — o score deles muda com a remoção.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> RemoverFlagsDeRedeAsync(
        IReadOnlyCollection<string> codigosDeRegra,
        CancellationToken cancelamento = default);

    void Adicionar(FlagAnomalia flag);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>
/// Roda as regras que cruzam parlamentares sobre a base inteira.
///
/// <para>É uma passada de <b>fechamento</b>, depois da ingestão — não a varredura de um
/// perfil. Rodá-la junto com a varredura individual obrigaria cada deputado a carregar as
/// despesas de todos os outros.</para>
///
/// <para>Não recalcula o score de ninguém: devolve em
/// <see cref="ResultadoDeRede.ParlamentaresAfetados"/> quem precisa ser varrido de novo, e
/// quem chama faz isso. Sem esse segundo passo o índice publicado ficava sem o REG_NET_004
/// — a varredura individual o apagava, e a de rede o regravava depois do score calculado.
/// Medido em 2026-09-24: 17 deputados com índice abaixo do que a própria página listava.</para>
/// </summary>
public sealed class ServicoDeVarreduraDeRede(
    IRepositorioDeRede repositorio,
    MotorDeRede motor,
    ParametrosDoMotor? parametros = null)
{
    private readonly ParametrosDoMotor _parametros = parametros ?? ParametrosDoMotor.Padrao;

    public async Task<ResultadoDeRede> VarrerAsync(CancellationToken cancelamento = default)
    {
        var parlamentares = await repositorio.ObterParlamentaresAsync(cancelamento)
            .ConfigureAwait(false);

        var despesas = await repositorio.ObterDespesasAsync(cancelamento).ConfigureAwait(false);

        var fornecedores = await repositorio.ObterFornecedoresAsync(cancelamento)
            .ConfigureAwait(false);

        var contexto = new ContextoDeRede(parlamentares, despesas, fornecedores, _parametros);
        var resultado = motor.Avaliar(contexto);

        var codigos = motor.Regras.Select(regra => regra.Codigo).ToList();

        // Recalcula do zero, como a passada individual: rodar de novo não pode dobrar as
        // flags, e uma regra afrouxada tem de conseguir remover um achado anterior.
        var antes = await repositorio.RemoverFlagsDeRedeAsync(codigos, cancelamento)
            .ConfigureAwait(false);

        foreach (var flag in resultado.Flags)
        {
            repositorio.Adicionar(flag);
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        var donoDaDespesa = despesas.ToDictionary(despesa => despesa.Id, despesa => despesa.ParlamentarId);

        var depois = resultado.Flags
            .Select(flag => donoDaDespesa.GetValueOrDefault(flag.DespesaId))
            .Where(id => id != Guid.Empty);

        return resultado with { ParlamentaresAfetados = [.. antes.Union(depois)] };
    }
}
