using Explanando.Domain.Comum;

namespace Explanando.Domain.Ingestao;

/// <summary>
/// Marca que a coleta de um parlamentar numa legislatura chegou ao fim.
///
/// A correcao da ingestao ja esta garantida pelo <c>HashUnico</c>: reprocessar tudo nao
/// duplica nada. O checkpoint existe por <b>tempo</b>, nao por correcao — a carga completa
/// sao ~6.000 requisicoes, e uma queda no deputado 400 nao pode custar os 399 anteriores.
///
/// Guarda tambem o que a coleta produziu, porque numero e' o unico jeito de distinguir
/// "este deputado nao gastou" de "a coleta quebrou em silencio".
/// </summary>
public sealed class CheckpointDeIngestao : Entidade
{
    private CheckpointDeIngestao()
    {
        // EF Core.
    }

    public int IdExternoParlamentar { get; private set; }

    public int Legislatura { get; private set; }

    public DateTimeOffset ConcluidoEm { get; private set; }

    public int DespesasImportadas { get; private set; }

    public int DespesasRejeitadas { get; private set; }

    /// <summary>Verdadeiro quando a coleta terminou com alarme de sanidade.</summary>
    public bool ComAlerta { get; private set; }

    public static CheckpointDeIngestao Criar(
        int idExternoParlamentar,
        int legislatura,
        DateTimeOffset concluidoEm,
        int despesasImportadas,
        int despesasRejeitadas,
        bool comAlerta) =>
        new()
        {
            IdExternoParlamentar = idExternoParlamentar,
            Legislatura = legislatura,
            ConcluidoEm = concluidoEm,
            DespesasImportadas = despesasImportadas,
            DespesasRejeitadas = despesasRejeitadas,
            ComAlerta = comAlerta,
        };

    public void Atualizar(
        DateTimeOffset concluidoEm,
        int despesasImportadas,
        int despesasRejeitadas,
        bool comAlerta)
    {
        ConcluidoEm = concluidoEm;
        DespesasImportadas = despesasImportadas;
        DespesasRejeitadas = despesasRejeitadas;
        ComAlerta = comAlerta;
    }

    /// <summary>
    /// Se este checkpoint ainda vale, dado o quanto se aceita de defasagem.
    ///
    /// Checkpoint <b>com alarme</b> nunca vale: ele registra uma coleta que provavelmente
    /// falhou, e pula-la de novo perpetuaria o buraco no banco.
    /// </summary>
    public bool AindaVale(DateTimeOffset agora, TimeSpan validade) =>
        !ComAlerta && agora - ConcluidoEm < validade;
}
