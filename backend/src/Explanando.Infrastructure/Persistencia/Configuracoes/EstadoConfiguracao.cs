using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class UnidadeDoEstadoConfiguracao : IEntityTypeConfiguration<UnidadeDoEstado>
{
    public void Configure(EntityTypeBuilder<UnidadeDoEstado> builder)
    {
        builder.ToTable("unidades_do_estado");
        builder.HasKey(unidade => unidade.Id);

        // O codigo do SIORG e' a identidade do no', e duas linhas com o mesmo codigo
        // significariam dois nos para o mesmo orgao — arestas duplicadas no desenho.
        builder
            .HasIndex(unidade => unidade.Codigo)
            .IsUnique()
            .HasDatabaseName("ix_unidades_do_estado_codigo");

        builder.Property(unidade => unidade.Codigo).IsRequired();
        builder.Property(unidade => unidade.Nome).HasMaxLength(500).IsRequired();
        builder.Property(unidade => unidade.Sigla).HasMaxLength(60);

        // Sem chave estrangeira para si mesma, de proposito. O SIORG entrega a arvore em
        // ordem arbitraria — o filho chega antes do pai —, e uma FK obrigaria a ordenar
        // topologicamente antes de cada gravacao para satisfazer o banco. A integridade
        // que importa aqui e' a do grafo montado na leitura, e ela e' garantida pela
        // resolucao de ancestrais na coleta.
        builder.Property(unidade => unidade.CodigoPai);
        builder.Property(unidade => unidade.CodigoOrgao);

        builder.Property(unidade => unidade.Poder).HasConversion<int?>();
        builder.Property(unidade => unidade.Esfera).HasConversion<int?>();
        builder.Property(unidade => unidade.Tipo).HasConversion<int>().IsRequired();
        builder.Property(unidade => unidade.NaturezaJuridica).HasConversion<int?>();
        builder.Property(unidade => unidade.Normatizacao).HasConversion<int?>();

        builder
            .Property(unidade => unidade.PrimeiraColeta)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(unidade => unidade.UltimaColeta)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(unidade => unidade.Ativa).IsRequired();

        // A travessia do grafo desce por aqui: "quem responde a esta unidade?".
        builder
            .HasIndex(unidade => unidade.CodigoPai)
            .HasDatabaseName("ix_unidades_do_estado_pai");

        // O desenho agrupa por poder, e o filtro padrao esconde o que sumiu.
        builder
            .HasIndex(unidade => new { unidade.Poder, unidade.Ativa })
            .HasDatabaseName("ix_unidades_do_estado_poder_ativa");
    }
}

internal sealed class MudancaEstruturalConfiguracao : IEntityTypeConfiguration<MudancaEstrutural>
{
    public void Configure(EntityTypeBuilder<MudancaEstrutural> builder)
    {
        builder.ToTable("mudancas_estruturais");
        builder.HasKey(mudanca => mudanca.Id);

        builder.Property(mudanca => mudanca.CodigoUnidade).IsRequired();
        builder.Property(mudanca => mudanca.NomeNaEpoca).HasMaxLength(500).IsRequired();
        builder.Property(mudanca => mudanca.Tipo).HasConversion<int>().IsRequired();
        builder.Property(mudanca => mudanca.Campo).HasMaxLength(60);

        // Cabem nome de orgao, sigla e codigo — o mais longo dos valores comparados.
        builder.Property(mudanca => mudanca.De).HasMaxLength(500);
        builder.Property(mudanca => mudanca.Para).HasMaxLength(500);

        builder
            .Property(mudanca => mudanca.DetectadaEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // A consulta que a tela faz: "o que mudou nos ultimos 30 dias", do mais novo para
        // o mais velho.
        builder
            .HasIndex(mudanca => mudanca.DetectadaEm)
            .IsDescending()
            .HasDatabaseName("ix_mudancas_estruturais_detectada_em");

        // E a do perfil de um orgao: "a historia desta unidade".
        builder
            .HasIndex(mudanca => mudanca.CodigoUnidade)
            .HasDatabaseName("ix_mudancas_estruturais_unidade");
    }
}
