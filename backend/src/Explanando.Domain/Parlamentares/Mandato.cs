using Explanando.Domain.Comum;

namespace Explanando.Domain.Parlamentares;

/// <summary>
/// Exercicio de um parlamentar numa legislatura. A legislatura corrente e' a 57 (2023-2027),
/// e ela e' parametro <b>obrigatorio</b> do endpoint de despesas da Camara — sem ela a API
/// devolve 200 com lista vazia (ver docs/registro/etapa-01-contratos.md).
/// </summary>
public sealed class Mandato : Entidade
{
    private Mandato()
    {
        // EF Core.
    }

    public Guid ParlamentarId { get; private set; }

    public int Legislatura { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly? DataFim { get; private set; }

    /// <summary>Mandato sem data de fim esta em curso.</summary>
    public bool Ativo => DataFim is null;

    internal static Mandato Criar(Guid parlamentarId, int legislatura, DateOnly inicio, DateOnly? fim)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(legislatura, 0);

        if (fim is not null && fim < inicio)
        {
            throw new ArgumentException(
                $"Fim do mandato ({fim}) e' anterior ao inicio ({inicio}).", nameof(fim));
        }

        return new Mandato
        {
            ParlamentarId = parlamentarId,
            Legislatura = legislatura,
            DataInicio = inicio,
            DataFim = fim,
        };
    }

    internal void Atualizar(DateOnly inicio, DateOnly? fim)
    {
        DataInicio = inicio;
        DataFim = fim;
    }
}
