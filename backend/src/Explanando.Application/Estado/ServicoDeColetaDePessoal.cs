using Explanando.Domain.Comum;
using Explanando.Domain.Estado;

namespace Explanando.Application.Estado;

/// <summary>Resumo de uma coleta do quadro de pessoal.</summary>
public sealed record ResultadoDaColetaDePessoal
{
    public required Competencia Competencia { get; init; }

    public int VinculosLidos { get; init; }
    public int OrgaosEncontrados { get; init; }

    /// <summary>Órgãos que a ponte ligou a um nó do grafo.</summary>
    public int OrgaosLigadosAoGrafo { get; init; }

    /// <summary>Quantos desses vieram da tabela curada, e não da regra de nome.</summary>
    public int LigadosPorExcecaoCurada { get; init; }

    /// <summary>Rubricas de totalização que não são órgãos, descartadas.</summary>
    public int AgregadosDescartados { get; init; }

    public int VinculosLigadosAoGrafo { get; init; }

    /// <summary>Vínculos que pertencem a um órgão de verdade — sem as rubricas de totalização.</summary>
    public int VinculosDeOrgaos => VinculosLidos - AgregadosDescartados;

    /// <summary>
    /// A fração dos vínculos <b>de órgão</b> que chegou ao grafo.
    ///
    /// <para>É <b>o número que decide se a tela pode existir</b>. Abaixo de uma boa
    /// cobertura, "o Ministério X tem N servidores" viraria uma afirmação sobre metade dos
    /// órgãos, com a outra metade invisível e sem aviso.</para>
    ///
    /// <para>O denominador exclui os agregados de propósito: "Estados / Municípios /
    /// Empresas" carrega 16.689 vínculos e <b>nunca</b> poderia casar, porque não é um
    /// órgão. Contá-la contra a ponte mediria a fonte, não o nosso trabalho.</para>
    /// </summary>
    public double CoberturaDosVinculos =>
        VinculosDeOrgaos == 0 ? 0 : (double)VinculosLigadosAoGrafo / VinculosDeOrgaos;

    /// <summary>Os maiores órgãos que ficaram sem par, do maior para o menor.</summary>
    public IReadOnlyList<(int CodigoSiafi, string Nome, int Vinculos)> SemPar { get; init; } = [];

    public int LotacoesGravadas { get; init; }
}

/// <summary>
/// Conta o quadro de pessoal de cada órgão e o liga ao grafo do Estado.
///
/// <para><b>Agrega enquanto lê.</b> O arquivo tem 778 mil linhas e 444 MB; o serviço
/// mantém em memória um contador por órgão — algo como 350 entradas — e esquece cada linha
/// assim que a soma. Nenhum nome é guardado, em lugar nenhum, em momento nenhum.</para>
///
/// <para><b>A ponte é conservadora por desenho.</b> Ela tenta o nome normalizado primeiro
/// entre os órgãos de topo e só então entre todas as unidades, e em ambos os casos exige
/// alvo <b>único</b>. Nome ambíguo não casa. Um par errado aqui não produziria erro visível
/// — produziria o quadro de pessoal de um órgão exibido dentro de outro.</para>
/// </summary>
public sealed class ServicoDeColetaDePessoal(
    IFonteDePessoal fonte,
    IRepositorioDePessoal repositorio)
{
    /// <summary>Quantos órgãos sem par o resumo lista. O suficiente para investigar.</summary>
    private const int SemParNoResumo = 15;

    public async Task<ResultadoDaColetaDePessoal> ColetarAsync(
        Competencia competencia,
        CancellationToken cancelamento = default)
    {
        var unidades = await repositorio.CarregarUnidadesParaPonteAsync(cancelamento)
            .ConfigureAwait(false);

        var porNomeDeTopo = Indexar(unidades.Where(u => u.EhDeTopo));
        var porNomeQualquer = Indexar(unidades);

        var acumulado = new Dictionary<int, Acumulador>();
        var lidos = 0;
        var descartados = 0;

        await foreach (var vinculo in fonte.LerVinculosAsync(competencia, cancelamento)
            .ConfigureAwait(false))
        {
            lidos++;

            if (PonteSiape.EhAgregado(vinculo.CodigoSiafi))
            {
                descartados++;
                continue;
            }

            if (!acumulado.TryGetValue(vinculo.CodigoSiafi, out var conta))
            {
                conta = new Acumulador(vinculo.NomeDoOrgao);
                acumulado[vinculo.CodigoSiafi] = conta;
            }

            conta.Vinculos++;

            if (!string.IsNullOrWhiteSpace(vinculo.SiglaDaFuncao))
            {
                var sigla = vinculo.SiglaDaFuncao.Trim();

                conta.PorFuncao[sigla] = conta.PorFuncao.GetValueOrDefault(sigla) + 1;
            }
        }

        await repositorio.LimparCompetenciaAsync(competencia, cancelamento).ConfigureAwait(false);

        var ligados = 0;
        var porExcecao = 0;
        var vinculosLigados = 0;
        var semPar = new List<(int, string, int)>();

        foreach (var (siafi, conta) in acumulado)
        {
            var siorg = Ligar(siafi, conta.Nome, porNomeDeTopo, porNomeQualquer);

            if (siorg is not null)
            {
                ligados++;
                vinculosLigados += conta.Vinculos;

                if (PonteSiape.Excecoes.ContainsKey(siafi))
                {
                    porExcecao++;
                }
            }
            else
            {
                semPar.Add((siafi, conta.Nome, conta.Vinculos));
            }

            repositorio.Adicionar(LotacaoDoOrgao.Criar(new NovaLotacao
            {
                Competencia = competencia,
                CodigoSiafi = siafi,
                NomeNaOrigem = conta.Nome,
                CodigoSiorg = siorg,
                Vinculos = conta.Vinculos,
                PorFuncao = conta.PorFuncao,
            }));
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return new ResultadoDaColetaDePessoal
        {
            Competencia = competencia,
            VinculosLidos = lidos,
            OrgaosEncontrados = acumulado.Count,
            OrgaosLigadosAoGrafo = ligados,
            LigadosPorExcecaoCurada = porExcecao,
            AgregadosDescartados = descartados,
            VinculosLigadosAoGrafo = vinculosLigados,
            SemPar = [.. semPar.OrderByDescending(item => item.Item3).Take(SemParNoResumo)],
            LotacoesGravadas = acumulado.Count,
        };
    }

    /// <summary>
    /// O nó do grafo para um órgão, ou nulo.
    ///
    /// <para>A tabela curada vence a regra de nome: ela existe justamente para os casos em
    /// que o nome mente — órgão renomeado, nome truncado pela fonte, erro de digitação na
    /// origem.</para>
    /// </summary>
    private static int? Ligar(
        int siafi,
        string nome,
        Dictionary<string, List<UnidadeParaPonte>> deTopo,
        Dictionary<string, List<UnidadeParaPonte>> qualquer)
    {
        if (PonteSiape.Excecoes.TryGetValue(siafi, out var curado))
        {
            return curado;
        }

        var chave = PonteSiape.Normalizar(nome);

        if (chave.Length == 0)
        {
            return null;
        }

        // Os de topo primeiro. "Secretaria-Executiva" existe dezenas de vezes na base, e
        // procurar entre todas de saida faria um ministerio casar com a secretaria de outro.
        if (deTopo.TryGetValue(chave, out var altos) && altos.Count == 1)
        {
            return altos[0].Codigo;
        }

        // So' entao entre todas — e ainda exigindo alvo unico em toda a base. E' assim que
        // os comandos militares, que sao unidades administrativas, encontram par.
        return qualquer.TryGetValue(chave, out var todos) && todos.Count == 1
            ? todos[0].Codigo
            : null;
    }

    private static Dictionary<string, List<UnidadeParaPonte>> Indexar(
        IEnumerable<UnidadeParaPonte> unidades)
    {
        var indice = new Dictionary<string, List<UnidadeParaPonte>>(StringComparer.Ordinal);

        foreach (var unidade in unidades)
        {
            var chave = PonteSiape.Normalizar(unidade.Nome);

            if (chave.Length == 0)
            {
                continue;
            }

            if (!indice.TryGetValue(chave, out var lista))
            {
                lista = [];
                indice[chave] = lista;
            }

            lista.Add(unidade);
        }

        return indice;
    }

    private sealed class Acumulador(string nome)
    {
        public string Nome { get; } = nome;
        public int Vinculos { get; set; }
        public Dictionary<string, int> PorFuncao { get; } = new(StringComparer.Ordinal);
    }
}
