using Explanando.Application.Consultas;
using Explanando.Domain.Comum;
using Explanando.Domain.Estado;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// As leituras do grafo do Estado sobre EF Core.
///
/// <para><b>O desafio aqui é de escala, não de SQL.</b> A base tem mais de cem mil
/// unidades, e a pergunta que a tela faz — "quantas unidades pendem deste órgão?" — é
/// recursiva. Duas saídas seriam possíveis: uma CTE recursiva por nó, ou carregar as
/// arestas uma vez e contar em memória. Esta classe faz a segunda: são ~100 mil pares de
/// inteiros, algo como 1 MB, contra uma consulta recursiva por linha do mapa.</para>
/// </summary>
public sealed class ConsultasDoEstadoEfCore(ExplanandoDbContext contexto) : IConsultasDoEstado
{
    /// <summary>
    /// Os tipos que entram no mapa inicial.
    ///
    /// <para>Entes, órgãos e entidades — algo em torno de 340 nós. As unidades
    /// administrativas e os colegiados ficam para o detalhe de cada órgão: medido, são mais
    /// de cem mil, e desenhá-las juntas produz uma nuvem onde o Executivo inteiro
    /// desaparece atrás dos conselhos.</para>
    /// </summary>
    private static readonly TipoDeUnidade[] TiposDoMapa =
        [TipoDeUnidade.Ente, TipoDeUnidade.Orgao, TipoDeUnidade.Entidade];

    /// <summary>
    /// O quadro de pessoal da competência mais recente, por nó do grafo.
    ///
    /// <para>São ~350 linhas — barato de carregar inteiro, e é o que permite somar os ramos
    /// em memória junto com a contagem de unidades, sem uma segunda travessia recursiva.</para>
    /// </summary>
    private async Task<Pessoal> CarregarPessoalAsync(CancellationToken cancelamento)
    {
        var competencia = await contexto.LotacoesDoOrgao
            .AsNoTracking()
            .OrderByDescending(lotacao => lotacao.Competencia)
            .Select(lotacao => (Competencia?)lotacao.Competencia)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        if (competencia is null)
        {
            return new Pessoal();
        }

        var lotacoes = await contexto.LotacoesDoOrgao
            .AsNoTracking()
            .Where(lotacao => lotacao.Competencia == competencia)
            .Select(lotacao => new
            {
                lotacao.CodigoSiorg,
                lotacao.Vinculos,
                lotacao.ComFuncaoDeConfianca,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porNo = new Dictionary<int, (int Vinculos, int Funcao)>();

        foreach (var lotacao in lotacoes.Where(l => l.CodigoSiorg is not null))
        {
            var no = lotacao.CodigoSiorg!.Value;
            var antes = porNo.GetValueOrDefault(no);

            // Dois codigos SIAFI podem apontar para o mesmo no': e' o caso de um orgao que
            // se dividiu no SIAPE mas continua um so' no SIORG. Somar e' o certo.
            porNo[no] = (antes.Vinculos + lotacao.Vinculos, antes.Funcao + lotacao.ComFuncaoDeConfianca);
        }

        var total = lotacoes.Sum(l => l.Vinculos);
        var ligados = lotacoes.Where(l => l.CodigoSiorg is not null).Sum(l => l.Vinculos);

        return new Pessoal
        {
            Competencia = competencia.Value.ToString(),
            Total = total,
            PorNo = porNo,
            Cobertura = total == 0 ? null : (double)ligados / total,
        };
    }

    public async Task<MapaDoEstado> ObterMapaAsync(CancellationToken cancelamento = default)
    {
        var arestas = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .Select(unidade => new { unidade.Codigo, unidade.CodigoPai })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var descendentes = ContarDescendentes(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)));

        var nos = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa && TiposDoMapa.Contains(unidade.Tipo))
            .OrderBy(unidade => unidade.Nome)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porPoder = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .GroupBy(unidade => unidade.Poder)
            .Select(grupo => new { grupo.Key, Unidades = grupo.Count() })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porTipo = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .GroupBy(unidade => unidade.Tipo)
            .Select(grupo => new { grupo.Key, Unidades = grupo.Count() })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var porBaseLegal = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .GroupBy(unidade => unidade.Normatizacao)
            .Select(grupo => new { grupo.Key, Unidades = grupo.Count() })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var ultimaColeta = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .MaxAsync(unidade => (DateTimeOffset?)unidade.UltimaColeta, cancelamento)
            .ConfigureAwait(false);

        // Historia existe a partir da SEGUNDA coleta: a primeira e' linha de base e nao
        // anuncia nada. Sem esta distincao, "nenhuma mudanca" seria lido como "nada mudou"
        // quando na verdade significa "ainda nao comparamos" — zeros diferentes, de novo.
        var temHistorico = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .AnyAsync(unidade => unidade.UltimaColeta > unidade.PrimeiraColeta, cancelamento)
            .ConfigureAwait(false);

        var pessoal = await CarregarPessoalAsync(cancelamento).ConfigureAwait(false);

        var pessoalDoRamo = SomarPorRamo(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)),
            pessoal.PorNo);

        var noMapa = nos.Select(unidade => unidade.Codigo).ToHashSet();

        var paisNoMapa = AcharAncestralNoMapa(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)),
            noMapa);

        return new MapaDoEstado
        {
            Nos =
            [
                .. nos.Select(unidade => Projetar(unidade, descendentes, pessoal, pessoalDoRamo) with
                {
                    CodigoPaiNoMapa = paisNoMapa.GetValueOrDefault(unidade.Codigo),
                }),
            ],
            TotalDeUnidades = arestas.Count,
            PorPoder =
            [
                .. porPoder
                    .Select(item => new ContagemDoEstado
                    {
                        Rotulo = item.Key is { } poder
                            ? VocabularioDoEstado.De(poder)
                            : VocabularioDoEstado.NaoClassificado,
                        Unidades = item.Unidades,
                    })
                    .OrderByDescending(item => item.Unidades),
            ],
            PorTipo =
            [
                .. porTipo
                    .Select(item => new ContagemDoEstado
                    {
                        Rotulo = VocabularioDoEstado.De(item.Key),
                        Unidades = item.Unidades,
                    })
                    .OrderByDescending(item => item.Unidades),
            ],
            PorBaseLegal =
            [
                .. porBaseLegal
                    .Select(item => new ContagemDoEstado
                    {
                        Rotulo = item.Key is { } norma
                            ? VocabularioDoEstado.De(norma)
                            : "Base legal não informada",
                        Unidades = item.Unidades,
                    })
                    .OrderByDescending(item => item.Unidades),
            ],
            UltimaColeta = ultimaColeta,
            TemHistorico = temHistorico,
            CompetenciaDoPessoal = pessoal.Competencia,
            TotalDeServidores = pessoal.Competencia is null ? null : pessoal.Total,
            CoberturaDoPessoal = pessoal.Cobertura,
        };
    }

    public async Task<IReadOnlyList<NoDoEstado>> ObterSubarvoreAsync(
        int codigo,
        CancellationToken cancelamento = default)
    {
        // Os filhos DIRETOS, e nao a arvore inteira: alguns orgaos tem milhares de
        // unidades abaixo, e devolver tudo de uma vez traria de volta o mesmo problema que
        // o mapa inicial resolve. A navegacao desce um nivel por vez.
        var filhos = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.CodigoPai == codigo && unidade.Ativa)
            .OrderBy(unidade => unidade.Nome)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        if (filhos.Count == 0)
        {
            return [];
        }

        var arestas = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .Select(unidade => new { unidade.Codigo, unidade.CodigoPai })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var descendentes = ContarDescendentes(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)));

        var pessoal = await CarregarPessoalAsync(cancelamento).ConfigureAwait(false);

        var pessoalDoRamo = SomarPorRamo(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)),
            pessoal.PorNo);

        return [.. filhos.Select(unidade => Projetar(unidade, descendentes, pessoal, pessoalDoRamo))];
    }

    public async Task<NoDoEstado?> ObterUnidadeAsync(
        int codigo,
        CancellationToken cancelamento = default)
    {
        var unidade = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Codigo == codigo, cancelamento)
            .ConfigureAwait(false);

        if (unidade is null)
        {
            return null;
        }

        var arestas = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(item => item.Ativa)
            .Select(item => new { item.Codigo, item.CodigoPai })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var descendentes = ContarDescendentes(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)));

        var pessoal = await CarregarPessoalAsync(cancelamento).ConfigureAwait(false);

        var pessoalDoRamo = SomarPorRamo(
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)),
            pessoal.PorNo);

        return Projetar(unidade, descendentes, pessoal, pessoalDoRamo);
    }

    public async Task<IReadOnlyList<MudancaDoEstado>> ListarMudancasAsync(
        int quantidade,
        CancellationToken cancelamento = default)
    {
        var mudancas = await contexto.MudancasEstruturais
            .AsNoTracking()
            .OrderByDescending(mudanca => mudanca.DetectadaEm)
            .Take(Math.Clamp(quantidade, 1, 200))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return
        [
            .. mudancas.Select(mudanca => new MudancaDoEstado
            {
                CodigoUnidade = mudanca.CodigoUnidade,
                Unidade = mudanca.NomeNaEpoca,

                // A frase vem do dominio, e nao de um switch aqui: e' a mesma regra do
                // CatalogoDeRegras — vocabulario duplicado em cada consumidor diverge.
                Descricao = mudanca.Descrever(),
                De = mudanca.De,
                Para = mudanca.Para,
                DetectadaEm = mudanca.DetectadaEm,
            }),
        ];
    }

    public async Task<IReadOnlyList<PontoDaSerie>> ObterSerieDePessoalAsync(
        int? codigoUnidade = null,
        CancellationToken cancelamento = default)
    {
        if (codigoUnidade is null)
        {
            // O Estado inteiro: soma tudo, inclusive o que a ponte nao alcanca. A pergunta
            // "o Executivo cresceu?" e' sobre o Executivo, nao sobre a parte dele que
            // coube no nosso grafo.
            var total = await contexto.LotacoesDoOrgao
                .AsNoTracking()
                .GroupBy(lotacao => lotacao.Competencia)
                .Select(grupo => new
                {
                    Competencia = grupo.Key,
                    Vinculos = grupo.Sum(l => l.Vinculos),
                    ComFuncao = grupo.Sum(l => l.ComFuncaoDeConfianca),
                })
                .ToListAsync(cancelamento)
                .ConfigureAwait(false);

            return
            [
                .. total
                    .OrderBy(ponto => ponto.Competencia)
                    .Select(ponto => new PontoDaSerie
                    {
                        Competencia = ponto.Competencia.ToString(),
                        Vinculos = ponto.Vinculos,
                        ComFuncaoDeConfianca = ponto.ComFuncao,
                    }),
            ];
        }

        // O ramo de uma unidade: preciso saber quem esta' abaixo dela antes de somar.
        var arestas = await contexto.UnidadesDoEstado
            .AsNoTracking()
            .Where(unidade => unidade.Ativa)
            .Select(unidade => new { unidade.Codigo, unidade.CodigoPai })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var noRamo = DescendentesDe(
            codigoUnidade.Value,
            arestas.Select(aresta => (aresta.Codigo, aresta.CodigoPai)));

        var lotacoes = await contexto.LotacoesDoOrgao
            .AsNoTracking()
            .Where(lotacao => lotacao.CodigoSiorg != null)
            .Select(lotacao => new
            {
                lotacao.Competencia,
                Siorg = lotacao.CodigoSiorg!.Value,
                lotacao.Vinculos,
                lotacao.ComFuncaoDeConfianca,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return
        [
            .. lotacoes
                .Where(lotacao => noRamo.Contains(lotacao.Siorg))
                .GroupBy(lotacao => lotacao.Competencia)
                .OrderBy(grupo => grupo.Key)
                .Select(grupo => new PontoDaSerie
                {
                    Competencia = grupo.Key.ToString(),
                    Vinculos = grupo.Sum(l => l.Vinculos),
                    ComFuncaoDeConfianca = grupo.Sum(l => l.ComFuncaoDeConfianca),
                }),
        ];
    }

    /// <summary>
    /// O conjunto formado por uma unidade e tudo que pende dela.
    ///
    /// <para>Desce em largura a partir do nó, com teto de profundidade pela mesma razão das
    /// outras travessias: o dado é de cadastro externo, e um ciclo travaria o laço.</para>
    /// </summary>
    private static HashSet<int> DescendentesDe(
        int raiz,
        IEnumerable<(int Codigo, int? CodigoPai)> arestas)
    {
        var filhos = new Dictionary<int, List<int>>();

        foreach (var (codigo, pai) in arestas)
        {
            if (pai is not { } valor)
            {
                continue;
            }

            if (!filhos.TryGetValue(valor, out var lista))
            {
                lista = [];
                filhos[valor] = lista;
            }

            lista.Add(codigo);
        }

        var ramo = new HashSet<int> { raiz };
        var fila = new Queue<int>([raiz]);
        var visitados = 0;

        while (fila.Count > 0 && visitados++ < 200_000)
        {
            if (!filhos.TryGetValue(fila.Dequeue(), out var lista))
            {
                continue;
            }

            foreach (var filho in lista.Where(ramo.Add))
            {
                fila.Enqueue(filho);
            }
        }

        return ramo;
    }

    private static NoDoEstado Projetar(
        UnidadeDoEstado unidade,
        IReadOnlyDictionary<int, int> descendentes,
        Pessoal pessoal,
        Dictionary<int, int> pessoalDoRamo) => new()
        {
            Codigo = unidade.Codigo,
            CodigoPai = unidade.CodigoPai,
            Nome = unidade.Nome,
            Sigla = unidade.Sigla,
            Tipo = VocabularioDoEstado.De(unidade.Tipo),
            Poder = unidade.Poder is { } poder ? VocabularioDoEstado.De(poder) : null,
            ChaveDoTipo = VocabularioDoEstado.ChaveDe(unidade.Tipo),
            ChaveDoPoder = unidade.Poder is { } chave ? VocabularioDoEstado.ChaveDe(chave) : null,
            Anel = VocabularioDoEstado.AnelDe(unidade.Tipo),
            Natureza = unidade.NaturezaJuridica is { } natureza
                ? VocabularioDoEstado.De(natureza)
                : null,
            BaseLegal = unidade.Normatizacao is { } norma ? VocabularioDoEstado.De(norma) : null,
            UnidadesSubordinadas = descendentes.GetValueOrDefault(unidade.Codigo),
            Ativa = unidade.Ativa,

            // Nulo quando a ponte nao alcanca esta unidade, e nulo NAO e' zero. Ver a nota
            // em NoDoEstado.Servidores.
            Servidores = pessoal.PorNo.TryGetValue(unidade.Codigo, out var proprio)
                ? proprio.Vinculos
                : null,
            EmFuncaoDeConfianca = pessoal.PorNo.TryGetValue(unidade.Codigo, out var funcao)
                ? funcao.Funcao
                : null,
            ServidoresNoRamo = pessoalDoRamo.TryGetValue(unidade.Codigo, out var ramo)
                ? ramo
                : null,
        };

    /// <summary>
    /// Soma o pessoal de cada unidade em todos os seus ancestrais.
    ///
    /// <para>Mesma travessia de <see cref="ContarDescendentes"/> — sobe do nó até a raiz —,
    /// e pelo mesmo motivo: a hierarquia tem menos de dez níveis, e o teto de saltos impede
    /// que um ciclo no cadastro trave o laço.</para>
    /// </summary>
    private static Dictionary<int, int> SomarPorRamo(
        IEnumerable<(int Codigo, int? CodigoPai)> arestas,
        IReadOnlyDictionary<int, (int Vinculos, int Funcao)> porNo)
    {
        var pais = new Dictionary<int, int?>();

        foreach (var (codigo, pai) in arestas)
        {
            pais[codigo] = pai;
        }

        var soma = new Dictionary<int, int>();

        foreach (var (codigo, contagem) in porNo)
        {
            // A propria unidade conta no ramo dela.
            soma[codigo] = soma.GetValueOrDefault(codigo) + contagem.Vinculos;

            var atual = pais.GetValueOrDefault(codigo);
            var saltos = 0;

            while (atual is { } pai && saltos++ < 32)
            {
                soma[pai] = soma.GetValueOrDefault(pai) + contagem.Vinculos;

                if (!pais.TryGetValue(pai, out var avo))
                {
                    break;
                }

                atual = avo;
            }
        }

        return soma;
    }

    /// <summary>O quadro de pessoal de uma competência, pronto para projetar.</summary>
    private sealed record Pessoal
    {
        public string? Competencia { get; init; }

        public int Total { get; init; }

        public IReadOnlyDictionary<int, (int Vinculos, int Funcao)> PorNo { get; init; } =
            new Dictionary<int, (int, int)>();

        public double? Cobertura { get; init; }
    }

    /// <summary>
    /// Conta, para cada unidade, quantas pendem dela em qualquer profundidade.
    ///
    /// <para>Sobe do filho para o pai em vez de descer: cada nó caminha até a raiz uma vez,
    /// somando um em cada ancestral. O caminho é curto — a hierarquia medida tem menos de
    /// dez níveis —, e assim não há recursão nem pilha própria.</para>
    ///
    /// <para><b>Com teto de saltos</b>, porque um ciclo no cadastro travaria o laço. O
    /// domínio recusa o ciclo de um nó só (pai de si mesma), mas nada impede A → B → A, e
    /// isto roda sobre dado de fonte externa.</para>
    /// </summary>
    /// <summary>
    /// Para cada unidade do mapa, o ancestral mais próximo que também está nele.
    ///
    /// <para>Sem isto o desenho perde nós: medido, oito órgãos penduram de uma unidade
    /// administrativa — os comandos do Exército, da Marinha e da Aeronáutica ficam entre o
    /// Ministério da Defesa e os órgãos abaixo deles —, e a unidade administrativa não
    /// entra no mapa. Esses oito virariam raízes soltas e sairiam do desenho em silêncio.</para>
    /// </summary>
    private static Dictionary<int, int> AcharAncestralNoMapa(
        IEnumerable<(int Codigo, int? CodigoPai)> arestas,
        HashSet<int> noMapa)
    {
        var pais = new Dictionary<int, int?>();

        foreach (var (codigo, pai) in arestas)
        {
            pais[codigo] = pai;
        }

        var resultado = new Dictionary<int, int>(noMapa.Count);

        foreach (var codigo in noMapa)
        {
            var atual = pais.GetValueOrDefault(codigo);
            var saltos = 0;

            while (atual is { } pai && saltos++ < 32)
            {
                if (noMapa.Contains(pai))
                {
                    resultado[codigo] = pai;
                    break;
                }

                atual = pais.GetValueOrDefault(pai);
            }
        }

        return resultado;
    }

    private static Dictionary<int, int> ContarDescendentes(
        IEnumerable<(int Codigo, int? CodigoPai)> arestas)
    {
        var pais = new Dictionary<int, int?>();

        foreach (var (codigo, pai) in arestas)
        {
            pais[codigo] = pai;
        }

        var contagem = new Dictionary<int, int>(pais.Count);

        foreach (var codigo in pais.Keys)
        {
            var atual = pais[codigo];
            var saltos = 0;

            while (atual is { } pai && saltos++ < 32)
            {
                contagem[pai] = contagem.GetValueOrDefault(pai) + 1;

                if (!pais.TryGetValue(pai, out var avo))
                {
                    break;
                }

                atual = avo;
            }
        }

        return contagem;
    }
}
