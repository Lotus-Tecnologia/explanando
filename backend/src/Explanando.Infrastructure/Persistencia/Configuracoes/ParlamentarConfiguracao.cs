using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class ParlamentarConfiguracao : IEntityTypeConfiguration<Parlamentar>
{
    public void Configure(EntityTypeBuilder<Parlamentar> builder)
    {
        builder.ToTable("parlamentares");
        builder.HasKey(parlamentar => parlamentar.Id);

        // So' existe dentro da URL do documento, que nao e' persistida. Ver a nota da
        // propriedade — sem esta coluna, nao ha como linkar documento nenhum.
        builder.Property(parlamentar => parlamentar.NumeroCarteira);

        builder.Property(parlamentar => parlamentar.IdExterno).IsRequired();
        builder.Property(parlamentar => parlamentar.NomeCivil).HasMaxLength(200).IsRequired();
        builder.Property(parlamentar => parlamentar.NomeParlamentar).HasMaxLength(100).IsRequired();
        builder.Property(parlamentar => parlamentar.SiglaPartido).HasMaxLength(20).IsRequired();
        builder.Property(parlamentar => parlamentar.Uf).HasColumnType("char(2)").IsRequired();
        builder.Property(parlamentar => parlamentar.Tipo).HasConversion<int>().IsRequired();
        builder.Property(parlamentar => parlamentar.FotoUrl).HasMaxLength(500);

        // O id 204379 da Camara e o id 204379 do Senado seriam pessoas diferentes:
        // a unicidade e' do par, nao do numero.
        builder
            .HasIndex(parlamentar => new { parlamentar.IdExterno, parlamentar.Tipo })
            .IsUnique()
            .HasDatabaseName("ix_parlamentares_id_externo_tipo");

        // Busca por nome na home publica.
        builder
            .HasIndex(parlamentar => parlamentar.NomeParlamentar)
            .HasDatabaseName("ix_parlamentares_nome_parlamentar");

        builder
            .HasMany(parlamentar => parlamentar.Mandatos)
            .WithOne()
            .HasForeignKey(mandato => mandato.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(parlamentar => parlamentar.Mandatos)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class MandatoConfiguracao : IEntityTypeConfiguration<Mandato>
{
    public void Configure(EntityTypeBuilder<Mandato> builder)
    {
        builder.ToTable("mandatos");
        builder.HasKey(mandato => mandato.Id);

        builder.Property(mandato => mandato.Legislatura).IsRequired();
        builder.Property(mandato => mandato.DataInicio).HasColumnType("date").IsRequired();
        builder.Property(mandato => mandato.DataFim).HasColumnType("date");

        // `Ativo` e' derivado de DataFim: nao existe coluna para ele.
        builder.Ignore(mandato => mandato.Ativo);

        builder
            .HasIndex(mandato => new { mandato.ParlamentarId, mandato.Legislatura })
            .IsUnique()
            .HasDatabaseName("ix_mandatos_parlamentar_legislatura");
    }
}
