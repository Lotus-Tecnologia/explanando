using Explanando.Domain.Auditoria;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Contestacoes;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Estado;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Ingestao;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Retencao;
using Explanando.Domain.Usuarios;
using Explanando.Domain.Varreduras;
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

    public DbSet<HistoricoScore> HistoricoScores => Set<HistoricoScore>();

    public DbSet<ContestacaoDespesa> Contestacoes => Set<ContestacaoDespesa>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Favorito> Favoritos => Set<Favorito>();

    public DbSet<SolicitacaoVarredura> SolicitacoesVarredura => Set<SolicitacaoVarredura>();

    public DbSet<ConsolidadoMensal> ConsolidadosMensais => Set<ConsolidadoMensal>();

    public DbSet<CoberturaDoParlamentar> CoberturasDeRegra => Set<CoberturaDoParlamentar>();

    /// <summary>
    /// O grafo do Estado. Contexto novo, tabelas proprias: nao ha chave estrangeira entre
    /// unidade e parlamentar, porque a ligacao entre o Senado como orgao e os 118 senadores
    /// e' semantica, nao referencial — sao dois cadastros federais que nao se conhecem.
    /// </summary>
    public DbSet<UnidadeDoEstado> UnidadesDoEstado => Set<UnidadeDoEstado>();

    public DbSet<MudancaEstrutural> MudancasEstruturais => Set<MudancaEstrutural>();

    /// <summary>
    /// O quadro de pessoal, em contagens. Nao ha' tabela de servidor: a agregacao acontece
    /// na ingestao, e nenhum nome chega ao banco.
    /// </summary>
    public DbSet<LotacaoDoOrgao> LotacoesDoOrgao => Set<LotacaoDoOrgao>();

    public DbSet<ContagemPorFuncao> ContagensPorFuncao => Set<ContagemPorFuncao>();

    /// <summary>
    /// As cadeiras de comando — a unica tabela com nome de pessoa, e a excecao declarada
    /// a' regra de <see cref="LotacoesDoOrgao"/>.
    ///
    /// <para>Sao ~100 cargos cujo ocupante o proprio governo publica com nome e sobrenome
    /// (portal da Presidencia, APIs do Congresso). Cada linha carrega a URL de onde saiu e
    /// a data em que foi vista; sem as duas, a linha nem e' criada. Ver a nota em
    /// <c>CadeiraDeComando</c> e o ADR das cadeiras.</para>
    /// </summary>
    public DbSet<CadeiraDeComando> CadeirasDeComando => Set<CadeiraDeComando>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExplanandoDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
