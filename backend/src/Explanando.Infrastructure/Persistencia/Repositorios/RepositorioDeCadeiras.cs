using Explanando.Application.Cadeiras;
using Explanando.Domain.Cadeiras;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// A escrita das cadeiras, um coletor por vez.
///
/// <para><b>Apaga e grava, dentro de uma transação.</b> Substituir é o certo porque a
/// fonte é a verdade sobre si mesma — um ministério extinto some da página do Planalto, e
/// mesclar o deixaria no nosso mapa para sempre. A transação é o que impede a janela em
/// que o apagar já aconteceu e o gravar ainda não: sem ela, uma queda no meio deixaria o
/// mapa do Executivo vazio até a próxima coleta.</para>
///
/// <para><b>A transação roda dentro da estratégia de execução, e não em volta dela.</b> A
/// conexão usa <c>EnableRetryOnFailure</c>, e o Npgsql recusa transação iniciada pelo
/// usuário fora da estratégia — com razão: uma tentativa repetida no meio de uma
/// transação aberta refaria só parte do trabalho. Isto aqui custou um <c>coletar-cadeiras</c>
/// que baixou as duas páginas do Planalto e estourou na hora de gravar.</para>
/// </summary>
public sealed class RepositorioDeCadeiras(ExplanandoDbContext contexto) : IRepositorioDeCadeiras
{
    public async Task SubstituirAsync(
        string fonte,
        IReadOnlyList<CadeiraDeComando> cadeiras,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(cadeiras);

        var estrategia = contexto.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            await using var transacao = await contexto.Database
                .BeginTransactionAsync(cancelamento)
                .ConfigureAwait(false);

            // O recorte e' pelo COLETOR, que a propria linha carrega. Recortar por URL
            // exigiria uma tabela de prefixos aqui dentro, e o Planalto grava de duas
            // paginas diferentes na mesma coleta.
            await contexto.CadeirasDeComando
                .Where(cadeira => cadeira.Coletor == fonte)
                .ExecuteDeleteAsync(cancelamento)
                .ConfigureAwait(false);

            contexto.CadeirasDeComando.AddRange(cadeiras);

            await contexto.SaveChangesAsync(cancelamento).ConfigureAwait(false);
            await transacao.CommitAsync(cancelamento).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
