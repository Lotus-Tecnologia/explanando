using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class DespesaConfiguracao : IEntityTypeConfiguration<Despesa>
{
    public void Configure(EntityTypeBuilder<Despesa> builder)
    {
        builder.ToTable("despesas");
        builder.HasKey(despesa => despesa.Id);

        builder.Property(despesa => despesa.ParlamentarId).IsRequired();

        // Nullable de proposito: 2 em 100 despesas reais tem o CNPJ sentinela
        // "00000000000001", que nao resolve para fornecedor nenhum. A despesa e'
        // persistida assim mesmo — descarta-la esconderia gasto publico.
        builder.Property(despesa => despesa.FornecedorId);

        builder
            .Property(despesa => despesa.DocumentoFornecedor)
            .HasConversion(Conversores.CpfCnpj)
            .HasColumnName("documento_fornecedor")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(despesa => despesa.NomeFornecedor).HasMaxLength(200);

        builder
            .Property(despesa => despesa.Competencia)
            .HasConversion(Conversores.Competencia)
            .HasColumnName("competencia")
            .IsRequired();

        builder.Property(despesa => despesa.TipoDespesa).HasMaxLength(100).IsRequired();
        builder.Property(despesa => despesa.CodigoDocumento).HasMaxLength(50).IsRequired();

        // Base de REG_KYB_005 (nota inicial). Ausente da especificacao original.
        builder.Property(despesa => despesa.NumeroDocumento).HasMaxLength(50).IsRequired();
        builder.Property(despesa => despesa.CodigoTipoDocumento).IsRequired();

        builder
            .Property(despesa => despesa.DataDocumento)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Tres valores, nao um: o pedido, o pago e o cortado.
        builder.Property(despesa => despesa.ValorDocumento).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(despesa => despesa.ValorLiquido).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(despesa => despesa.ValorGlosa).HasColumnType("numeric(18,2)").IsRequired();

        builder.Property(despesa => despesa.UrlDocumento).HasMaxLength(500);
        builder.Property(despesa => despesa.CodigoLote);
        builder.Property(despesa => despesa.Parcela);

        builder.Property(despesa => despesa.HashUnico).HasColumnType("char(64)").IsRequired();

        builder.Ignore(despesa => despesa.TemGlosa);
        builder.Ignore(despesa => despesa.EhDocumentoFiscal);
        builder.Ignore(despesa => despesa.ValorParaScore);

        // A garantia de idempotencia da ingestao. Sem este indice, reprocessar um mes
        // duplicaria todas as despesas dele.
        builder
            .HasIndex(despesa => despesa.HashUnico)
            .IsUnique()
            .HasDatabaseName("ix_despesas_hash_unico");

        builder
            .HasIndex(despesa => despesa.ParlamentarId)
            .HasDatabaseName("ix_despesas_parlamentar_id");

        builder
            .HasIndex(despesa => despesa.FornecedorId)
            .HasDatabaseName("ix_despesas_fornecedor_id");

        builder
            .HasIndex(despesa => despesa.DataDocumento)
            .HasDatabaseName("ix_despesas_data_documento");

        // REG_TX_001 (fracionamento) e REG_TX_003 (padrao mensal identico) varrem
        // "mesmo parlamentar, mesmo fornecedor, mesmo mes".
        builder
            .HasIndex(despesa => new
            {
                despesa.ParlamentarId,
                despesa.Competencia,
                despesa.DocumentoFornecedor,
            })
            .HasDatabaseName("ix_despesas_parlamentar_competencia_fornecedor");

        builder
            .HasOne<Fornecedor>()
            .WithMany()
            .HasForeignKey(despesa => despesa.FornecedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FlagAnomaliaConfiguracao : IEntityTypeConfiguration<FlagAnomalia>
{
    public void Configure(EntityTypeBuilder<FlagAnomalia> builder)
    {
        builder.ToTable("flags_anomalias");
        builder.HasKey(flag => flag.Id);

        builder.Property(flag => flag.CodigoRegra).HasMaxLength(50).IsRequired();
        builder.Property(flag => flag.Gravidade).HasConversion<int>().IsRequired();
        builder.Property(flag => flag.Descricao).HasColumnType("text").IsRequired();

        // Exigido por REG_NET_001: o cruzamento por 6 digitos de CPF produz indicio,
        // e a incerteza precisa viajar junto do achado ate a tela.
        builder
            .Property(flag => flag.GrauDeConfianca)
            .HasColumnType("numeric(4,3)")
            .IsRequired();

        builder
            .Property(flag => flag.DataIdentificacao)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Ignore(flag => flag.EhVermelha);
        builder.Ignore(flag => flag.EhDeterministica);
        builder.Ignore(flag => flag.Peso);

        builder
            .HasIndex(flag => flag.DespesaId)
            .HasDatabaseName("ix_flags_anomalias_despesa_id");

        builder
            .HasIndex(flag => flag.CodigoRegra)
            .HasDatabaseName("ix_flags_anomalias_codigo_regra");

        // A mesma regra nao pode marcar a mesma despesa duas vezes numa reexecucao.
        builder
            .HasIndex(flag => new { flag.DespesaId, flag.CodigoRegra })
            .IsUnique()
            .HasDatabaseName("ix_flags_anomalias_despesa_regra");

        builder
            .HasOne<Despesa>()
            .WithMany()
            .HasForeignKey(flag => flag.DespesaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ScoreRiscoParlamentarConfiguracao
    : IEntityTypeConfiguration<ScoreRiscoParlamentar>
{
    public void Configure(EntityTypeBuilder<ScoreRiscoParlamentar> builder)
    {
        builder.ToTable("scores_risco");
        builder.HasKey(score => score.Id);

        // O numero do B2C: soma em reais, com estornos abatidos.
        builder
            .Property(score => score.VolumeTotalGasto)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(score => score.DespesasAvaliadas).IsRequired();

        builder
            .Property(score => score.PesoDasFlags)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder.Property(score => score.TotalFlagsVermelhas).IsRequired();
        builder.Property(score => score.TotalFlagsMedias).IsRequired();
        builder.Property(score => score.TotalFlagsBaixas).IsRequired();

        // Composicao do indice por gravidade (ADR-0009): a API B2B expoe o total sempre
        // acompanhado das partes, para nao sugerir gravidade que nao existe.
        builder.Property(score => score.PesoFlagsAltas).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(score => score.PesoFlagsMedias).HasColumnType("numeric(10,2)").IsRequired();
        builder.Property(score => score.PesoFlagsBaixas).HasColumnType("numeric(10,2)").IsRequired();

        builder.Ignore(score => score.IndiceDeAltas);
        builder.Ignore(score => score.IndiceDeMedias);
        builder.Ignore(score => score.IndiceDeBaixas);
        builder.Ignore(score => score.IndiceSemGravidadeAlta);

        // O numero do B2B: pontos por 100 despesas. Quatro casas porque a densidade e'
        // pequena — arredondar para duas empataria parlamentares distintos.
        builder
            .Property(score => score.IndiceDeRisco)
            .HasColumnType("numeric(10,4)")
            .IsRequired();

        builder.Property(score => score.AmostraSuficiente).IsRequired();

        builder
            .Property(score => score.DataUltimaVarredura)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Relacao 1:1 com parlamentar.
        builder
            .HasIndex(score => score.ParlamentarId)
            .IsUnique()
            .HasDatabaseName("ix_scores_risco_parlamentar_id");

        // Sao dois rankings, entao sao dois indices (ADR-0009).
        builder
            .HasIndex(score => score.VolumeTotalGasto)
            .IsDescending()
            .HasDatabaseName("ix_scores_risco_volume_total_gasto");

        // Filtrado: quem nao tem amostra suficiente nunca aparece neste ranking, entao
        // nao precisa ocupar espaco no indice.
        builder
            .HasIndex(score => score.IndiceDeRisco)
            .IsDescending()
            .HasFilter("amostra_suficiente")
            .HasDatabaseName("ix_scores_risco_indice_de_risco");

        builder
            .HasOne<Parlamentar>()
            .WithOne()
            .HasForeignKey<ScoreRiscoParlamentar>(score => score.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LimiteCotaConfiguracao : IEntityTypeConfiguration<LimiteCota>
{
    public void Configure(EntityTypeBuilder<LimiteCota> builder)
    {
        builder.ToTable("limites_cota");
        builder.HasKey(limite => limite.Id);

        builder.Property(limite => limite.Uf).HasColumnType("char(2)").IsRequired();
        builder.Property(limite => limite.ValorMensal).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(limite => limite.VigenciaInicio).HasColumnType("date").IsRequired();
        builder.Property(limite => limite.VigenciaFim).HasColumnType("date");

        builder
            .HasIndex(limite => new { limite.Uf, limite.VigenciaInicio })
            .IsUnique()
            .HasDatabaseName("ix_limites_cota_uf_vigencia");
    }
}
