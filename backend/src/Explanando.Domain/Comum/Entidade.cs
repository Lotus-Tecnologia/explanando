namespace Explanando.Domain.Comum;

/// <summary>
/// Raiz de toda entidade persistida. Identidade por <see cref="Id"/>, nao por valor —
/// dois parlamentares homonimos continuam sendo duas pessoas.
///
/// O construtor sem parametros e' protegido para uso do EF Core na materializacao.
/// </summary>
public abstract class Entidade
{
    protected Entidade() => Id = Guid.CreateVersion7();

    /// <summary>
    /// UUID v7: ordenavel por tempo de criacao, o que mantem a insercao em indice B-tree
    /// sequencial em vez de espalhada. Importa numa tabela que recebe centenas de milhares
    /// de despesas.
    /// </summary>
    public Guid Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is Entidade outra && outra.GetType() == GetType() && outra.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
