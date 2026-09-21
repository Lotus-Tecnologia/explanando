using Explanando.Application.Consultas;
using Explanando.Domain.Cadeiras;
using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// O mapa de poder sobre EF Core.
///
/// <para>Uma consulta só, sem paginação e sem cache: são ~100 linhas. O custo está todo
/// na montagem das camadas, que acontece em memória porque o vocabulário de cada camada
/// depende do <b>par</b> poder-camada e isso não é coisa que se traduza em SQL.</para>
/// </summary>
public sealed class ConsultasDeCadeirasEfCore(ExplanandoDbContext contexto) : IConsultasDeCadeiras
{
    /// <summary>
    /// A ordem em que os poderes aparecem.
    ///
    /// <para>A mesma do grafo — ver <c>ORDEM_DOS_PODERES</c> no desenho. As duas vistas
    /// trocam entre si no clique, e um poder que muda de lugar ao alternar a vista faria o
    /// leitor perder de vista o que estava olhando.</para>
    /// </summary>
    private static readonly Poder[] Ordem =
        [Poder.Executivo, Poder.Judiciario, Poder.FuncoesEssenciaisAJustica, Poder.Legislativo];

    public async Task<MapaDePoderCompleto> ObterMapaAsync(CancellationToken cancelamento = default)
    {
        var todas = await contexto.CadeirasDeComando
            .AsNoTracking()
            .OrderBy(cadeira => cadeira.Camada)
            .ThenBy(cadeira => cadeira.Cargo)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porPoder = todas
            .GroupBy(cadeira => cadeira.Poder)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.ToList());

        return new MapaDePoderCompleto
        {
            Poderes =
            [
                .. Ordem.Select(poder =>
                    Montar(poder, porPoder.GetValueOrDefault(poder) ?? [])),
            ],
        };
    }

    private static MapaDePoder Montar(Poder poder, List<CadeiraDeComando> cadeiras)
    {
        var camadas = cadeiras
            .GroupBy(cadeira => cadeira.Camada)
            .OrderBy(grupo => grupo.Key)
            .Select(grupo => new CamadaDoMapa
            {
                Chave = VocabularioDasCadeiras.ChaveDe(grupo.Key),
                Rotulo = VocabularioDasCadeiras.De(poder, grupo.Key),
                Ordem = (int)grupo.Key,
                Cadeiras =
                [
                    .. grupo.Select(cadeira => new CadeiraDoMapa
                    {
                        Cargo = cadeira.Cargo,
                        Ocupante = cadeira.Ocupante,
                        Orgao = cadeira.Orgao,
                        Partido = cadeira.Partido,
                        Uf = cadeira.Uf,
                        FotoUrl = cadeira.FotoUrl,
                        CodigoSiorg = cadeira.CodigoSiorg,
                        Fonte = cadeira.Fonte,
                        VerificadoEm = cadeira.VerificadoEm,
                        Vaga = cadeira.Vaga,
                        Curada = cadeira.Origem == OrigemDaCadeira.Curada,
                    }),
                ],
            })
            .ToList();

        return new MapaDePoder
        {
            ChaveDoPoder = VocabularioDoEstado.ChaveDe(poder),
            Poder = VocabularioDoEstado.De(poder),
            Camadas = camadas,
            TotalDeCadeiras = cadeiras.Count,
            Vagas = cadeiras.Count(cadeira => cadeira.Vaga),

            // A verificacao mais ANTIGA: e' o pior caso da tela, e e' o que o leitor
            // precisa saber. A mais nova mascararia uma camada parada ha' meses.
            VerificadoDesde = cadeiras.Count == 0
                ? null
                : cadeiras.Min(cadeira => cadeira.VerificadoEm),

            PorQueFalta = cadeiras.Count == 0
                ? VocabularioDasCadeiras.PorQueFaltaOcupante
                : null,
        };
    }
}
