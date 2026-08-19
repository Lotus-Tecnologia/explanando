using Explanando.Domain.Auditoria;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Ingestao;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia;

/// <summary>
/// Contexto de persistencia do V1.
///
/// A nomenclatura PascalCase do C# vira snake_case no banco via EFCore.NamingConventions,
/// configurado em <c>ServicosDePersistencia</c>. Os mapeamentos ficam em classes
/// <c>IEntityTypeConfiguration</c> separadas — as entidades de dominio nao carregam
/// Data Annotations nem sabem que o EF Core existe.
/// </summary>
public sealed class ExplanandoDbContext(DbContextOptions<ExplanandoDbContext> options)
    : DbContext(options)
{
    public DbSet<Parlamentar> Parlamentares => Set<Parlamentar>();

    public DbSet<Mandato> Mandatos => Set<Mandato>();

    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();

    public DbSet<Sociedade> Sociedades => Set<Sociedade>();

    public DbSet<Despesa> Despesas => Set<Despesa>();

    public DbSet<FlagAnomalia> FlagsAnomalias => Set<FlagAnomalia>();

    public DbSet<ScoreRiscoParlamentar> ScoresRisco => Set<ScoreRiscoParlamentar>();

    public DbSet<LimiteCota> LimitesCota => Set<LimiteCota>();

    public DbSet<CheckpointDeIngestao> CheckpointsDeIngestao => Set<CheckpointDeIngestao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExplanandoDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
