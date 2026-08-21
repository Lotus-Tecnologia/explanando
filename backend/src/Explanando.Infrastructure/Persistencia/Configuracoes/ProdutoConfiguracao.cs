using Explanando.Domain.Auditoria;
using Explanando.Domain.Contestacoes;
using Explanando.Domain.Despesas;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Retencao;
using Explanando.Domain.Usuarios;
using Explanando.Domain.Varreduras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// Mapeamento das entidades de produto: contestacao, usuario, favorito, solicitacao de
/// varredura e historico de score. Separadas das de auditoria porque tem outro ciclo de vida
/// — sao escritas por acao de pessoa, nao pela varredura.
/// </summary>
internal sealed class ContestacaoDespesaConfiguracao : IEntityTypeConfiguration<ContestacaoDespesa>
{
    public void Configure(EntityTypeBuilder<ContestacaoDespesa> builder)
    {
        builder.ToTable("contestacoes");
        builder.HasKey(contestacao => contestacao.Id);

        builder.Property(contestacao => contestacao.DespesaId).IsRequired();
        builder.Property(contestacao => contestacao.ParlamentarId).IsRequired();
        builder.Property(contestacao => contestacao.FlagAnomaliaId);

        builder
            .Property(contestacao => contestacao.EmailDoGabinete)
            .HasMaxLength(320)
            .IsRequired();

        builder
            .Property(contestacao => contestacao.Texto)
            .HasMaxLength(ContestacaoDespesa.TamanhoMaximoDoTexto)
            .IsRequired();

        builder.Property(contestacao => contestacao.UrlDoAnexo).HasMaxLength(500);

        builder.Property(contestacao => contestacao.Situacao).HasConversion<int>().IsRequired();

        builder
            .Property(contestacao => contestacao.EnviadaEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(contestacao => contestacao.RetiradaEm)
            .HasColumnType("timestamp with time zone");

        builder.Ignore(contestacao => contestacao.EstaVisivel);

        // A pagina da despesa carrega a resposta do gabinete junto do achado.
        builder
            .HasIndex(contestacao => contestacao.DespesaId)
            .HasDatabaseName("ix_contestacoes_despesa_id");

        builder
            .HasIndex(contestacao => contestacao.ParlamentarId)
            .HasDatabaseName("ix_contestacoes_parlamentar_id");

        builder
            .HasOne<Despesa>()
            .WithMany()
            .HasForeignKey(contestacao => contestacao.DespesaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, nao Cascade: apagar um parlamentar nao pode apagar em silencio a defesa
        // que o gabinete dele publicou.
        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(contestacao => contestacao.ParlamentarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<FlagAnomalia>()
            .WithMany()
            .HasForeignKey(contestacao => contestacao.FlagAnomaliaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(usuario => usuario.Id);

        builder.Property(usuario => usuario.Email).HasMaxLength(320).IsRequired();

        // Hash, nunca senha. O tamanho cobre Argon2id codificado.
        builder.Property(usuario => usuario.HashSenha).HasMaxLength(255).IsRequired();

        builder.Property(usuario => usuario.Perfil).HasConversion<int>().IsRequired();
        builder.Property(usuario => usuario.ParlamentarVinculadoId);
        builder.Property(usuario => usuario.Ativo).IsRequired();

        builder
            .Property(usuario => usuario.DataCriacao)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .HasIndex(usuario => usuario.Email)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_email");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(usuario => usuario.ParlamentarVinculadoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FavoritoConfiguracao : IEntityTypeConfiguration<Favorito>
{
    public void Configure(EntityTypeBuilder<Favorito> builder)
    {
        builder.ToTable("favoritos");
        builder.HasKey(favorito => favorito.Id);

        builder.Property(favorito => favorito.UsuarioId).IsRequired();
        builder.Property(favorito => favorito.ParlamentarId).IsRequired();

        builder
            .Property(favorito => favorito.DataCriacao)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder
            .Property(favorito => favorito.UltimaNotificacaoEm)
            .HasColumnType("timestamp with time zone");

        // A unica garantia de idempotencia que existe: duas linhas iguais significariam
        // dois alertas por achado, e duas requisicoes simultaneas passariam por qualquer
        // checagem feita em memoria sem se enxergar.
        builder
            .HasIndex(favorito => new { favorito.UsuarioId, favorito.ParlamentarId })
            .IsUnique()
            .HasDatabaseName("ix_favoritos_usuario_parlamentar");

        // O ciclo de retorno le por parlamentar: "quem precisa saber deste achado novo?"
        builder
            .HasIndex(favorito => favorito.ParlamentarId)
            .HasDatabaseName("ix_favoritos_parlamentar_id");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(favorito => favorito.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(favorito => favorito.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SolicitacaoVarreduraConfiguracao
    : IEntityTypeConfiguration<SolicitacaoVarredura>
{
    public void Configure(EntityTypeBuilder<SolicitacaoVarredura> builder)
    {
        builder.ToTable("solicitacoes_varredura");
        builder.HasKey(solicitacao => solicitacao.Id);

        builder.Property(solicitacao => solicitacao.ParlamentarId).IsRequired();
        builder.Property(solicitacao => solicitacao.UsuarioId);

        builder
            .Property(solicitacao => solicitacao.DataSolicitacao)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(solicitacao => solicitacao.Situacao).HasConversion<int>().IsRequired();

        builder.Property(solicitacao => solicitacao.PaymentId).HasMaxLength(100);

        builder
            .Property(solicitacao => solicitacao.ValorPago)
            .HasColumnType("numeric(10,2)")
            .IsRequired();

        builder
            .Property(solicitacao => solicitacao.PagaEm)
            .HasColumnType("timestamp with time zone");

        builder
            .Property(solicitacao => solicitacao.ConcluidaEm)
            .HasColumnType("timestamp with time zone");

        builder.Property(solicitacao => solicitacao.MotivoDaFalha).HasColumnType("text");

        // A segunda metade da idempotencia do webhook: a primeira esta no dominio, e esta
        // aqui cobre duas requisicoes simultaneas, que o dominio sozinho nao veria.
        // Filtrado porque PaymentId so' existe depois da confirmacao.
        builder
            .HasIndex(solicitacao => solicitacao.PaymentId)
            .IsUnique()
            .HasFilter("payment_id IS NOT NULL")
            .HasDatabaseName("ix_solicitacoes_varredura_payment_id");

        // "Qual foi a ultima varredura concluida deste parlamentar?" — a pergunta da
        // carencia de 24 horas, feita antes de emitir qualquer cobranca.
        builder
            .HasIndex(solicitacao => new { solicitacao.ParlamentarId, solicitacao.ConcluidaEm })
            .IsDescending(false, true)
            .HasDatabaseName("ix_solicitacoes_varredura_parlamentar_concluida");

        builder
            .HasIndex(solicitacao => solicitacao.Situacao)
            .HasDatabaseName("ix_solicitacoes_varredura_situacao");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(solicitacao => solicitacao.ParlamentarId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull: o usuario pode apagar a conta (LGPD art. 18, VI) sem que o registro
        // contabil do pagamento desapareca junto.
        builder
            .HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(solicitacao => solicitacao.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class HistoricoScoreConfiguracao : IEntityTypeConfiguration<HistoricoScore>
{
    public void Configure(EntityTypeBuilder<HistoricoScore> builder)
    {
        builder.ToTable("historico_scores");
        builder.HasKey(ponto => ponto.Id);

        builder.Property(ponto => ponto.ParlamentarId).IsRequired();
        builder.Property(ponto => ponto.Referencia).HasColumnType("date").IsRequired();

        // Os mesmos tipos das colunas de origem em scores_risco: a curva nao pode ter
        // precisao diferente do numero que ela historia.
        builder
            .Property(ponto => ponto.VolumeTotalGasto)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder
            .Property(ponto => ponto.IndiceDeRisco)
            .HasColumnType("numeric(10,4)")
            .IsRequired();

        builder.Property(ponto => ponto.DespesasAvaliadas).IsRequired();
        builder.Property(ponto => ponto.TotalFlagsVermelhas).IsRequired();

        // Sem esta coluna um degrau no grafico e' ambiguo entre "o parlamentar mudou" e
        // "as regras mudaram". Ver HistoricoScore.EhComparavelCom.
        builder.Property(ponto => ponto.VersaoDoMotor).IsRequired();

        builder
            .Property(ponto => ponto.CapturadoEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Um ponto por parlamentar por dia. Varredura repetida no mesmo dia atualiza a
        // linha existente em vez de empilhar pontos sobre a mesma data.
        builder
            .HasIndex(ponto => new { ponto.ParlamentarId, ponto.Referencia })
            .IsUnique()
            .HasDatabaseName("ix_historico_scores_parlamentar_referencia");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(ponto => ponto.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ConsolidadoMensalConfiguracao : IEntityTypeConfiguration<ConsolidadoMensal>
{
    public void Configure(EntityTypeBuilder<ConsolidadoMensal> builder)
    {
        builder.ToTable("consolidados_mensais");
        builder.HasKey(consolidado => consolidado.Id);

        builder.Property(consolidado => consolidado.ParlamentarId).IsRequired();

        builder
            .Property(consolidado => consolidado.Competencia)
            .HasConversion(Conversores.Competencia)
            .HasColumnName("competencia")
            .IsRequired();

        // Os mesmos tipos das colunas de origem em despesas: o total nao pode arredondar
        // diferente das notas que o formaram.
        builder.Property(c => c.TotalLiquido).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.TotalDocumento).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(c => c.TotalGlosa).HasColumnType("numeric(18,2)");

        builder.Property(c => c.QuantidadeDeDespesas).IsRequired();
        builder.Property(c => c.QuantidadeDeEstornos).IsRequired();
        builder.Property(c => c.DetalheDisponivel).IsRequired();

        builder
            .Property(consolidado => consolidado.AtualizadoEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        // Um consolidado por parlamentar e mes. Dois seriam gasto contado em dobro.
        builder
            .HasIndex(consolidado => new { consolidado.ParlamentarId, consolidado.Competencia })
            .IsUnique()
            .HasDatabaseName("ix_consolidados_mensais_parlamentar_competencia");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(consolidado => consolidado.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CoberturaDoParlamentarConfiguracao
    : IEntityTypeConfiguration<CoberturaDoParlamentar>
{
    public void Configure(EntityTypeBuilder<CoberturaDoParlamentar> builder)
    {
        builder.ToTable("coberturas_regra");
        builder.HasKey(cobertura => cobertura.Id);

        builder.Property(cobertura => cobertura.ParlamentarId).IsRequired();
        builder.Property(cobertura => cobertura.CodigoRegra).HasMaxLength(50).IsRequired();
        builder.Property(cobertura => cobertura.Situacao).HasConversion<int>().IsRequired();
        builder.Property(cobertura => cobertura.DespesasComDado).IsRequired();
        builder.Property(cobertura => cobertura.DespesasSemDado).IsRequired();
        builder.Property(cobertura => cobertura.FlagsEmitidas).IsRequired();
        builder.Property(cobertura => cobertura.DadoAusente).HasConversion<int>().IsRequired();
        builder.Property(cobertura => cobertura.MotivoDaFalha).HasColumnType("text");
        builder.Property(cobertura => cobertura.VersaoDoMotor).IsRequired();

        builder
            .Property(cobertura => cobertura.AtualizadoEm)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Ignore(cobertura => cobertura.DespesasVistas);
        builder.Ignore(cobertura => cobertura.SilencioEhConclusivo);

        // Uma medicao por regra por parlamentar. Duas linhas seriam duas verdades sobre a
        // mesma pergunta.
        builder
            .HasIndex(cobertura => new { cobertura.ParlamentarId, cobertura.CodigoRegra })
            .IsUnique()
            .HasDatabaseName("ix_coberturas_regra_parlamentar_regra");

        builder
            .HasOne<Parlamentar>()
            .WithMany()
            .HasForeignKey(cobertura => cobertura.ParlamentarId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
