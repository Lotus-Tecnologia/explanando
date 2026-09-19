using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class LotacaoDoOrgaoConfiguracao : IEntityTypeConfiguration<LotacaoDoOrgao>
{
    public void Configure(EntityTypeBuilder<LotacaoDoOrgao> builder)
    {
        builder.ToTable("lotacoes_do_orgao");
        builder.HasKey(lotacao => lotacao.Id);

        // AAAAMM, como o resto do produto. Preserva a ordem cronologica no indice.
        builder.Property(lotacao => lotacao.Competencia)
            .HasConversion(Conversores.Competencia)
            .IsRequired();

        builder.Property(lotacao => lotacao.CodigoSiafi).IsRequired();
        builder.Property(lotacao => lotacao.NomeNaOrigem).HasMaxLength(200).IsRequired();
        builder.Property(lotacao => lotacao.CodigoSiorg);
        builder.Property(lotacao => lotacao.Vinculos).IsRequired();
        builder.Property(lotacao => lotacao.ComFuncaoDeConfianca).IsRequired();

        // Um registro por orgao e mes. Recoletar o mesmo mes apaga antes de gravar, mas o
        // indice e' o que transforma uma falha nessa limpeza em erro em vez de numero
        // dobrado.
        builder
            .HasIndex(lotacao => new { lotacao.Competencia, lotacao.CodigoSiafi })
            .IsUnique()
            .HasDatabaseName("ix_lotacoes_competencia_siafi");

        // A consulta da tela: "quantos servidores tem esta unidade do grafo?"
        builder
            .HasIndex(lotacao => new { lotacao.CodigoSiorg, lotacao.Competencia })
            .HasDatabaseName("ix_lotacoes_siorg_competencia");

        builder
            .HasMany(lotacao => lotacao.Funcoes)
            .WithOne()
            .HasForeignKey(funcao => funcao.LotacaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(lotacao => lotacao.Funcoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ContagemPorFuncaoConfiguracao : IEntityTypeConfiguration<ContagemPorFuncao>
{
    public void Configure(EntityTypeBuilder<ContagemPorFuncao> builder)
    {
        builder.ToTable("contagens_por_funcao");
        builder.HasKey(contagem => contagem.Id);

        // "CD", "FCE", "FG", "FEX"... A maior medida tem 4 caracteres; 20 e' folga barata.
        builder.Property(contagem => contagem.Sigla).HasMaxLength(20).IsRequired();
        builder.Property(contagem => contagem.Quantidade).IsRequired();
        builder.Property(contagem => contagem.LotacaoId).IsRequired();
    }
}
