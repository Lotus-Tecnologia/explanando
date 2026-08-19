using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Implementacao EF Core da porta de persistencia da ingestao.
/// </summary>
public sealed class RepositorioDeIngestao(ExplanandoDbContext contexto) : IRepositorioDeIngestao
{
    public Task<Parlamentar?> ObterParlamentarAsync(
        int idExterno,
        CancellationToken cancelamento = default) =>
        contexto.Parlamentares
            .FirstOrDefaultAsync(
                parlamentar => parlamentar.IdExterno == idExterno
                    && parlamentar.Tipo == TipoParlamentar.DeputadoFederal,
                cancelamento);

    /// <summary>
    /// Compara o objeto de valor inteiro, e nao <c>documento.Numero</c>.
    ///
    /// Com value converter o EF Core mapeia <see cref="CpfCnpj"/> para uma coluna so' e nao
    /// consegue traduzir acesso a propriedade dentro do tipo — a consulta lancaria
    /// InvalidOperationException em tempo de execucao. Descoberto na Etapa 02.
    /// </summary>
    public Task<Fornecedor?> ObterFornecedorAsync(
        CpfCnpj documento,
        CancellationToken cancelamento = default) =>
        contexto.Fornecedores
            .FirstOrDefaultAsync(fornecedor => fornecedor.Documento == documento, cancelamento);

    /// <summary>
    /// Carrega so' a coluna de hash, nao as despesas inteiras. Um deputado tem ~1.200
    /// despesas por legislatura; materializar as entidades a cada coleta seria desperdicio
    /// de memoria e de banda para uma comparacao de strings.
    /// </summary>
    public async Task<HashSet<string>> ObterHashesDeDespesasAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        var hashes = await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .Select(despesa => despesa.HashUnico)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return hashes.ToHashSet(StringComparer.Ordinal);
    }

    public void Adicionar(Parlamentar parlamentar) => contexto.Parlamentares.Add(parlamentar);

    public void Adicionar(Fornecedor fornecedor) => contexto.Fornecedores.Add(fornecedor);

    public void Adicionar(Sociedade sociedade) => contexto.Sociedades.Add(sociedade);

    public void Adicionar(Despesa despesa) => contexto.Despesas.Add(despesa);

    public Task SalvarAsync(CancellationToken cancelamento = default) =>
        contexto.SaveChangesAsync(cancelamento);
}
