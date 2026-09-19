using Explanando.Application.Estado;
using Explanando.Domain.Comum;
using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Acesso as contagens de pessoal.
///
/// <para>A leitura das unidades e' <c>AsNoTracking</c> e projetada: a ponte precisa de
/// codigo, nome e se e' de topo — tres campos de 96 mil linhas, nao 96 mil entidades
/// rastreadas.</para>
/// </summary>
public sealed class RepositorioDePessoal(ExplanandoDbContext contexto) : IRepositorioDePessoal
{
    /// <summary>Os tipos que a ponte considera "de topo". Ver <c>UnidadeParaPonte</c>.</summary>
    private static readonly TipoDeUnidade[] DeTopo =
        [TipoDeUnidade.Ente, TipoDeUnidade.Orgao, TipoDeUnidade.Entidade];

    public async Task<IReadOnlyList<UnidadeParaPonte>> CarregarUnidadesParaPonteAsync(
        CancellationToken cancelamento = default) =>
        await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .Select(unidade => new UnidadeParaPonte
            {
                Codigo = unidade.Codigo,
                Nome = unidade.Nome,
                EhDeTopo = DeTopo.Contains(unidade.Tipo),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

    public async Task LimparCompetenciaAsync(
        Competencia competencia,
        CancellationToken cancelamento = default)
    {
        // Comparacao com o valor INTEIRO, e nao decompondo ano e mes: `Competencia` chega
        // ao banco por um ValueConverter, e o EF nao sabe traduzir `.Ano` e `.Mes` — ele
        // recusa a consulta em tempo de execucao, nao de compilacao. Descoberto assim, na
        // primeira coleta real.
        //
        // ExecuteDelete em vez de carregar e remover: sao ate' 350 lotacoes com filhos, e
        // o cascade do banco resolve as contagens por funcao numa ida so'.
        await contexto.LotacoesDoOrgao
            .Where(lotacao => lotacao.Competencia == competencia)
            .ExecuteDeleteAsync(cancelamento)
            .ConfigureAwait(false);
    }

    public void Adicionar(LotacaoDoOrgao lotacao) => contexto.LotacoesDoOrgao.Add(lotacao);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
