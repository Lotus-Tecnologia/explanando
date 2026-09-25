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

    /// <summary>
    /// Quando a entidade foi criada, lido do próprio <see cref="Id"/>.
    ///
    /// <para>O UUID v7 guarda nos 48 primeiros bits os milissegundos desde 1970 (RFC 9562).
    /// É o que responde "quando esta despesa entrou na base" sem coluna nova — e é essa a data
    /// que um alerta precisa: a da flag não serve, porque a varredura recria as flags a cada
    /// passada.</para>
    ///
    /// <para>Nulo para id que não é v7 — um id montado à mão num teste, por exemplo. Inventar
    /// uma data a partir dele seria publicar um horário que não aconteceu.</para>
    /// </summary>
    public static DateTimeOffset? MomentoDaCriacao(Guid id)
    {
        if (id.Version != 7)
        {
            return null;
        }

        var hexa = id.ToString("N")[..12];
        var milissegundos = long.Parse(hexa, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture);

        return DateTimeOffset.FromUnixTimeMilliseconds(milissegundos);
    }

    public override bool Equals(object? obj) =>
        obj is Entidade outra && outra.GetType() == GetType() && outra.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
