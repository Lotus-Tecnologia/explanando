using Explanando.Application.Consultas;
using Explanando.Infrastructure.Persistencia.Consultas;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Explanando.Integration.Tests;

public sealed class ConsultasDoEstadoComCacheTests
{
    [Fact]
    public async Task Serie_da_unidade_0_nao_envenena_a_serie_do_Estado_inteiro()
    {
        var interno = Substitute.For<IConsultasDoEstado>();
        interno.ObterSerieDePessoalAsync(0, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PontoDaSerie>());
        interno.ObterSerieDePessoalAsync(null, Arg.Any<CancellationToken>())
            .Returns([new PontoDaSerie { Competencia = "2026-07", Vinculos = 10, ComFuncaoDeConfianca = 1 }]);

        var cache = new ServiceCollection().AddHybridCache().Services
            .BuildServiceProvider().GetRequiredService<HybridCache>();
        var consultas = new ConsultasDoEstadoComCache(interno, cache);

        // Quem chama /estado/0/pessoal primeiro não pode decidir o que /estado/pessoal mostra.
        (await consultas.ObterSerieDePessoalAsync(0)).ShouldBeEmpty();
        (await consultas.ObterSerieDePessoalAsync(null)).Count.ShouldBe(1);
    }
}
