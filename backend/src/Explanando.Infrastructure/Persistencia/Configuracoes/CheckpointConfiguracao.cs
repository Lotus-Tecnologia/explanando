using Explanando.Domain.Ingestao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class CheckpointDeIngestaoConfiguracao : IEntityTypeConfiguration<CheckpointDeIngestao>
{
    public void Configure(EntityTypeBuilder<CheckpointDeIngestao> builder)
    {
        builder.ToTable("checkpoints_ingestao");
        builder.HasKey(checkpoint => checkpoint.Id);

        builder.Property(checkpoint => checkpoint.IdExternoParlamentar).IsRequired();
        builder.Property(checkpoint => checkpoint.Legislatura).IsRequired();

        builder
            .Property(checkpoint => checkpoint.ConcluidoEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(checkpoint => checkpoint.DespesasImportadas).IsRequired();
        builder.Property(checkpoint => checkpoint.DespesasRejeitadas).IsRequired();
        builder.Property(checkpoint => checkpoint.ComAlerta).IsRequired();

        // Um checkpoint por parlamentar e legislatura.
        builder
            .HasIndex(checkpoint => new { checkpoint.IdExternoParlamentar, checkpoint.Legislatura })
            .IsUnique()
            .HasDatabaseName("ix_checkpoints_ingestao_parlamentar_legislatura");

        // Consulta operacional: "quais coletas terminaram com alarme?"
        builder
            .HasIndex(checkpoint => checkpoint.ComAlerta)
            .HasDatabaseName("ix_checkpoints_ingestao_com_alerta");
    }
}
