using Explanando.Domain.Eleicoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

/// <summary>A ligação parlamentar ↔ candidatura no TSE.</summary>
internal sealed class CandidaturaConfiguracao : IEntityTypeConfiguration<CandidaturaDoParlamentar>
{
    public void Configure(EntityTypeBuilder<CandidaturaDoParlamentar> builder)
    {
        builder.ToTable("candidaturas");
        builder.HasKey(candidatura => candidatura.Id);

        builder.Property(candidatura => candidatura.NomeDeUrna).HasMaxLength(120).IsRequired();
        builder.Property(candidatura => candidatura.Cargo).HasMaxLength(60).IsRequired();
        builder.Property(candidatura => candidatura.Uf).HasMaxLength(2).IsRequired();
        builder.Property(candidatura => candidatura.SiglaPartido).HasMaxLength(40).IsRequired();
        builder.Property(candidatura => candidatura.Situacao).HasMaxLength(60).IsRequired();

        builder
            .HasOne<Domain.Parlamentares.Parlamentar>()
            .WithMany()
            .HasForeignKey(candidatura => candidatura.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma candidatura por parlamentar por eleição, e um sequencial do TSE por ano.
        builder
            .HasIndex(candidatura => new { candidatura.ParlamentarId, candidatura.AnoDaEleicao })
            .IsUnique()
            .HasDatabaseName("ix_candidaturas_parlamentar_ano");

        builder
            .HasIndex(candidatura => new { candidatura.AnoDaEleicao, candidatura.SequencialNoTse })
            .IsUnique()
            .HasDatabaseName("ix_candidaturas_ano_sequencial");
    }
}

/// <summary>Votos por município, só das candidaturas ligadas a parlamentares da base.</summary>
internal sealed class VotosNoMunicipioConfiguracao : IEntityTypeConfiguration<VotosNoMunicipio>
{
    public void Configure(EntityTypeBuilder<VotosNoMunicipio> builder)
    {
        builder.ToTable("votos_por_municipio");
        builder.HasKey(votos => votos.Id);

        builder.Property(votos => votos.NomeDoMunicipio).HasMaxLength(120).IsRequired();
        builder.Property(votos => votos.Uf).HasMaxLength(2).IsRequired();

        builder
            .HasOne<CandidaturaDoParlamentar>()
            .WithMany()
            .HasForeignKey(votos => votos.CandidaturaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasIndex(votos => votos.ParlamentarId)
            .HasDatabaseName("ix_votos_por_municipio_parlamentar");
    }
}

/// <summary>
/// Doações de pessoa física. <b>O CPF é a máscara da Receita</b> — o número completo que o TSE
/// publica não tem coluna onde caber, e a coluna de 11 caracteres é o limite físico disso.
/// </summary>
internal sealed class DoacaoEleitoralConfiguracao : IEntityTypeConfiguration<DoacaoEleitoral>
{
    public void Configure(EntityTypeBuilder<DoacaoEleitoral> builder)
    {
        builder.ToTable("doacoes_eleitorais");
        builder.HasKey(doacao => doacao.Id);

        builder.Property(doacao => doacao.Destino).HasConversion<int>().IsRequired();
        builder.Property(doacao => doacao.NomeDoDoador).HasMaxLength(200).IsRequired();
        builder.Property(doacao => doacao.NomeDoDoadorNormalizado).HasMaxLength(200).IsRequired();

        builder
            .Property(doacao => doacao.CpfDoDoador)
            .HasConversion(Conversores.CpfParcial)
            .HasColumnName("cpf_mascarado_do_doador")
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(doacao => doacao.NomeDoDestinatario).HasMaxLength(200).IsRequired();
        builder.Property(doacao => doacao.SiglaPartidoDoDestinatario).HasMaxLength(40).IsRequired();
        builder.Property(doacao => doacao.UfDoDestinatario).HasMaxLength(2).IsRequired();
        builder.Property(doacao => doacao.CargoDoDestinatario).HasMaxLength(60);
        builder.Property(doacao => doacao.Data).HasColumnType("date");
        builder.Property(doacao => doacao.Valor).HasPrecision(18, 2);

        // Não é único: medido, 189 receitas de pessoa física em 2022 repetem o SQ_RECEITA com
        // valores diferentes — a mesma receita declarada em partes. A carga é idempotente por
        // outro caminho: ela apaga o ano inteiro antes de gravar.
        builder
            .HasIndex(doacao => new { doacao.AnoDaEleicao, doacao.Destino, doacao.SequencialDaReceita })
            .HasDatabaseName("ix_doacoes_ano_destino_sequencial");

        builder
            .HasIndex(doacao => doacao.ParlamentarId)
            .HasDatabaseName("ix_doacoes_parlamentar");

        // O cruzamento com o sócio: nome normalizado primeiro, que é o mais seletivo.
        builder
            .HasIndex(doacao => doacao.NomeDoDoadorNormalizado)
            .HasDatabaseName("ix_doacoes_nome_do_doador");
    }
}
