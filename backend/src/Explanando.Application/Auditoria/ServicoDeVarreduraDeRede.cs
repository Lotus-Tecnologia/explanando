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
    /// Apaga as flags de rede da passada anterior, por código de regra.
    ///
    /// A passada por parlamentar não pode fazer isso: ela apaga por despesa, e apagaria
    /// achados de rede que nada têm a ver com aquele deputado. Por isso o recorte aqui é
    /// pelo código.
    /// </summary>
    Task RemoverFlagsDeRedeAsync(
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
/// <para>Não recalcula o score de ninguém. As flags de rede entram no perfil na próxima
/// varredura individual daquele parlamentar; fazê-las mexer no score aqui significaria
/// escrever em 513 linhas de score numa transação só.</para>
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
        await repositorio.RemoverFlagsDeRedeAsync(codigos, cancelamento).ConfigureAwait(false);

        foreach (var flag in resultado.Flags)
        {
            repositorio.Adicionar(flag);
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return resultado;
    }
}
