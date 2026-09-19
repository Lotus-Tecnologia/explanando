using Explanando.Application.Ingestao;
using Explanando.Domain.Estado;

namespace Explanando.Application.Estado;

/// <summary>Resumo de uma passada pela estrutura do Estado.</summary>
public sealed record ResultadoDaColetaDoEstado
{
    public int OrgaosVisitados { get; init; }
    public int OrgaosComFalha { get; init; }

    /// <summary>Órgãos de outra esfera de governo, deixados de fora por escolha.</summary>
    public int OrgaosForaDoEscopo { get; init; }

    public int UnidadesVistas { get; init; }
    public int UnidadesNovas { get; init; }
    public int UnidadesAtualizadas { get; init; }
    public int UnidadesDesaparecidas { get; init; }
    public int UnidadesReaparecidas { get; init; }

    /// <summary>Unidades que o domínio recusou — ver <see cref="UnidadeDoEstado"/>.</summary>
    public int UnidadesRecusadas { get; init; }

    /// <summary>
    /// Arestas que apontam para uma unidade que a fonte não soube descrever. Ficam como
    /// estão: apagar o pai reparentaria o nó para a raiz e mentiria sobre a hierarquia.
    /// </summary>
    public int AncestraisNaoResolvidos { get; init; }

    /// <summary>Quantas linhas de história esta coleta produziu.</summary>
    public int Mudancas { get; init; }

    /// <summary>
    /// Nenhum órgão falhou. <b>Só uma coleta completa pode concluir desaparecimento</b>.
    /// </summary>
    public bool Completa { get; init; }

    /// <summary>
    /// O banco estava vazio: esta coleta é a fotografia inicial, e não notícia.
    /// </summary>
    public bool EhLinhaDeBase { get; init; }

    public IReadOnlyList<string> Falhas { get; init; } = [];
}

/// <summary>
/// Percorre o SIORG e mantém o grafo do Estado — os nós, as arestas e o que mudou.
///
/// <para><b>Por que a comparação vive aqui.</b> A fonte publica um retrato do agora: os
/// campos de histórico chegam vazios e os parâmetros de versão são ignorados
/// (<c>docs/contratos/siorg.md</c>). "O que mudou na estrutura do Estado" só existe se
/// alguém guardar o retrato anterior e comparar — e é isto.</para>
///
/// <para><b>Duas políticas mandam no resultado, e as duas são o ADR-0007 num grafo:</b></para>
///
/// <list type="number">
/// <item><b>A primeira coleta é linha de base.</b> Gravar vinte mil "passou a constar na
/// estrutura oficial" no dia um seria verdade e seria inútil: afogaria a única tela em que
/// cada linha deveria significar algo que aconteceu.</item>
/// <item><b>Coleta incompleta não faz ninguém desaparecer.</b> Quando uma subárvore falha,
/// as unidades dela somem da resposta exatamente como sumiriam se tivessem sido extintas.
/// Publicar "300 órgãos deixaram de constar" por causa de um tempo esgotado seria uma
/// manchete falsa produzida por nós.</item>
/// </list>
/// </summary>
public sealed class ServicoDeColetaDoEstado(
    IFonteDaEstruturaDoEstado fonte,
    IRepositorioDoEstado repositorio,
    IRelogio relogio)
{
    /// <summary>
    /// Quantos saltos para cima o resolvedor de ancestrais aceita.
    ///
    /// <para>Medido, a cadeia mais longa tem três degraus acima do órgão: Poder Executivo →
    /// União → República Federativa do Brasil. Oito é folga com teto — sem teto, um ciclo
    /// no cadastro (pai A → B → A) faria o laço girar para sempre contra uma API pública.</para>
    /// </summary>
    private const int SaltosMaximosDeAncestral = 8;

    /// <summary>
    /// As esferas que entram no grafo.
    ///
    /// <para>Só a federal, por padrão e <b>por escolha explícita</b>. Medido: a lista de
    /// órgãos traz o Governo do Distrito Federal e mais quatro unidades estaduais. Não é
    /// erro da fonte — outros entes também usam o SIORG —, mas são cinco nós de um ente que
    /// ninguém decidiu mapear, e um mapa do Estado brasileiro com metade de um estado dentro
    /// confunde mais do que informa.</para>
    /// </summary>
    public static IReadOnlySet<EsferaDeGoverno> EsferasPadrao { get; } =
        new HashSet<EsferaDeGoverno> { EsferaDeGoverno.Federal };

    public async Task<ResultadoDaColetaDoEstado> ColetarAsync(
        IReadOnlySet<EsferaDeGoverno>? esferas = null,
        CancellationToken cancelamento = default)
    {
        var aceitas = esferas ?? EsferasPadrao;
        var agora = relogio.Agora;

        var conhecidas = (await repositorio.CarregarUnidadesAsync(cancelamento).ConfigureAwait(false))
            .ToDictionary(unidade => unidade.Codigo);

        var ehLinhaDeBase = conhecidas.Count == 0;

        var orgaos = await fonte.ListarOrgaosAsync(cancelamento).ConfigureAwait(false);

        var noEscopo = orgaos
            .Where(orgao => orgao.Esfera is null || aceitas.Contains(orgao.Esfera.Value))
            .ToList();

        var falhas = new List<string>();
        var vistas = new Dictionary<int, NovaUnidadeDoEstado>();
        var visitados = 0;

        foreach (var orgao in noEscopo)
        {
            var subarvore = await fonte.ObterSubarvoreAsync(orgao.Codigo, cancelamento)
                .ConfigureAwait(false);

            visitados++;

            if (!subarvore.DeuCerto)
            {
                falhas.Add($"orgao {orgao.Codigo} ({orgao.Nome}): {subarvore.Falha}");
                continue;
            }

            foreach (var unidade in subarvore.Unidades)
            {
                // A mesma unidade chega por mais de uma subarvore — a do Ministerio da
                // Defesa e a dos comandos militares se sobrepoem, por exemplo. Dedupe por
                // codigo, que e' a identidade do no'.
                vistas[unidade.Codigo] = unidade;
            }

            // O orgao em si pode nao vir na propria arvore se a chamada devolver so' os
            // filhos; garantir aqui e' mais barato que descobrir o buraco no desenho.
            vistas.TryAdd(orgao.Codigo, orgao);
        }

        var ancestraisNaoResolvidos = await ResolverAncestraisAsync(vistas, cancelamento)
            .ConfigureAwait(false);

        var completa = falhas.Count == 0;

        var contagem = Aplicar(vistas, conhecidas, agora, ehLinhaDeBase, completa);

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return new ResultadoDaColetaDoEstado
        {
            OrgaosVisitados = visitados,
            OrgaosComFalha = falhas.Count,
            OrgaosForaDoEscopo = orgaos.Count - noEscopo.Count,
            UnidadesVistas = vistas.Count,
            UnidadesNovas = contagem.Novas,
            UnidadesAtualizadas = contagem.Atualizadas,
            UnidadesDesaparecidas = contagem.Desaparecidas,
            UnidadesReaparecidas = contagem.Reaparecidas,
            UnidadesRecusadas = contagem.Recusadas,
            AncestraisNaoResolvidos = ancestraisNaoResolvidos,
            Mudancas = contagem.Mudancas,
            Completa = completa,
            EhLinhaDeBase = ehLinhaDeBase,
            Falhas = falhas,
        };
    }

    /// <summary>
    /// Sobe a hierarquia até a raiz, buscando unidade a unidade o que a varredura por
    /// órgão não alcança.
    ///
    /// <para>É necessário porque <c>/estrutura-organizacional</c> recusa um <c>ente</c>:
    /// os nós de topo — os quatro poderes, a União, a República Federativa do Brasil — não
    /// aparecem em nenhuma subárvore, e sem eles o grafo começa no segundo nível e as
    /// arestas do topo apontam para o vazio.</para>
    /// </summary>
    private async Task<int> ResolverAncestraisAsync(
        Dictionary<int, NovaUnidadeDoEstado> vistas,
        CancellationToken cancelamento)
    {
        // Guarda quem ja' foi pedido. Sem isto, um pai que a fonte nao conhece seria
        // pedido de novo a cada salto e contado varias vezes como nao resolvido.
        var pedidos = new HashSet<int>();
        var naoResolvidos = 0;

        for (var salto = 0; salto < SaltosMaximosDeAncestral; salto++)
        {
            var faltantes = vistas.Values
                .Select(unidade => unidade.CodigoPai)
                .OfType<int>()
                .Where(pai => !vistas.ContainsKey(pai) && !pedidos.Contains(pai))
                .Distinct()
                .ToList();

            if (faltantes.Count == 0)
            {
                break;
            }

            foreach (var codigo in faltantes)
            {
                pedidos.Add(codigo);

                var pai = await fonte.ObterUnidadeAsync(codigo, cancelamento).ConfigureAwait(false);

                if (pai is null)
                {
                    naoResolvidos++;
                    continue;
                }

                vistas[pai.Codigo] = pai;
            }
        }

        return naoResolvidos;
    }

    private Contagem Aplicar(
        Dictionary<int, NovaUnidadeDoEstado> vistas,
        Dictionary<int, UnidadeDoEstado> conhecidas,
        DateTimeOffset agora,
        bool ehLinhaDeBase,
        bool completa)
    {
        var contagem = new Contagem();

        foreach (var (codigo, nova) in vistas)
        {
            if (conhecidas.TryGetValue(codigo, out var existente))
            {
                Atualizar(existente, nova, agora, ehLinhaDeBase, ref contagem);
                continue;
            }

            UnidadeDoEstado criada;

            try
            {
                criada = UnidadeDoEstado.Criar(nova, agora);
            }
            catch (ArgumentException)
            {
                // Cadastro publico erra. Uma unidade malformada nao pode derrubar as outras
                // dezenove mil — o desfecho certo e' recusar a linha e seguir.
                contagem.Recusadas++;
                continue;
            }

            repositorio.Adicionar(criada);
            contagem.Novas++;

            if (!ehLinhaDeBase)
            {
                repositorio.Registrar(MudancaEstrutural.Criacao(codigo, criada.Nome, agora));
                contagem.Mudancas++;
            }
        }

        if (!completa)
        {
            return contagem;
        }

        foreach (var (codigo, existente) in conhecidas)
        {
            if (vistas.ContainsKey(codigo) || !existente.MarcarDesaparecida(agora))
            {
                continue;
            }

            contagem.Desaparecidas++;
            repositorio.Registrar(MudancaEstrutural.Desaparecimento(codigo, existente.Nome, agora));
            contagem.Mudancas++;
        }

        return contagem;
    }

    private void Atualizar(
        UnidadeDoEstado existente,
        NovaUnidadeDoEstado nova,
        DateTimeOffset agora,
        bool ehLinhaDeBase,
        ref Contagem contagem)
    {
        // O nome de ANTES: a mudanca precisa ser legivel com o rotulo que a unidade tinha
        // quando ela aconteceu.
        var nomeNaEpoca = existente.Nome;

        if (existente.MarcarReaparecida(agora))
        {
            contagem.Reaparecidas++;
            repositorio.Registrar(MudancaEstrutural.Reaparecimento(existente.Codigo, nomeNaEpoca, agora));
            contagem.Mudancas++;
        }

        IReadOnlyList<MudancaDeCampo> mudancas;

        try
        {
            mudancas = existente.Atualizar(nova, agora);
        }
        catch (ArgumentException)
        {
            contagem.Recusadas++;
            return;
        }

        if (mudancas.Count == 0)
        {
            return;
        }

        contagem.Atualizadas++;

        if (ehLinhaDeBase)
        {
            return;
        }

        foreach (var mudanca in mudancas)
        {
            repositorio.Registrar(
                MudancaEstrutural.Alteracao(existente.Codigo, nomeNaEpoca, mudanca, agora));

            contagem.Mudancas++;
        }
    }

    private struct Contagem
    {
        public int Novas;
        public int Atualizadas;
        public int Desaparecidas;
        public int Reaparecidas;
        public int Recusadas;
        public int Mudancas;
    }
}
