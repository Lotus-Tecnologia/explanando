using Explanando.Application.Consultas;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Despesas;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Infrastructure.Persistencia.Consultas;

/// <summary>
/// As consultas de leitura, sobre EF Core.
///
/// <para>Tudo aqui é <c>AsNoTracking</c> e projetado campo a campo. Nada devolve entidade de
/// domínio: o que a API publica é decidido explicitamente, linha a linha, e não por
/// serialização de um objeto que pode ganhar campos depois.</para>
/// </summary>
public sealed class ConsultasPublicasEfCore(ExplanandoDbContext contexto) : IConsultasPublicas
{
    /// <summary>Teto de página. Sem ele, um <c>?itens=100000</c> derruba a API de fora.</summary>
    public const int MaximoDeItensPorPagina = 200;

    public async Task<Pagina<ItemDoRanking>> RanquearAsync(
        OrdenacaoDoRanking ordenacao,
        TipoParlamentar casa,
        int pagina,
        int itensPorPagina,
        CancellationToken cancelamento = default)
    {
        var (numero, tamanho) = Normalizar(pagina, itensPorPagina);

        // Resolvido AQUI, e nao dentro da projecao: `NomeDaCasa` e' metodo C# e o EF nao o
        // traduz para SQL — dentro do Select ele quebraria em tempo de execucao, nao de
        // compilacao. Como a consulta ja esta filtrada por uma casa so', o nome e' constante
        // para todas as linhas e entra como parametro.
        var nomeDaCasa = NomeDaCasa(casa);

        // O filtro por casa vem ANTES de tudo, inclusive do piso amostral: cada ranking e'
        // uma competicao fechada. Um senador com 200 despesas disputa com os outros 117
        // senadores, nao com 513 deputados cuja cota tem outra regra e outro valor.
        var consulta =
            from score in contexto.ScoresRisco.AsNoTracking()
            join parlamentar in contexto.Parlamentares.AsNoTracking()
                on score.ParlamentarId equals parlamentar.Id
            where parlamentar.Tipo == casa
            select new { score, parlamentar };

        // Quem esta abaixo do piso sai das ordenacoes por densidade, e so' delas. O volume
        // nao depende de amostra: um deputado com 20 despesas gastou o que gastou.
        if (ordenacao is OrdenacaoDoRanking.Indice or OrdenacaoDoRanking.Gravidade)
        {
            consulta = consulta.Where(linha => linha.score.AmostraSuficiente);
        }

        consulta = ordenacao switch
        {
            OrdenacaoDoRanking.Indice =>
                consulta.OrderByDescending(linha => linha.score.IndiceDeRisco)
                        .ThenBy(linha => linha.parlamentar.NomeParlamentar),

            // Densidade so' das altas. Ordenar pelo total colocaria quem tem 14 achados
            // graves abaixo de quem tem zero, porque medias e baixas somam.
            OrdenacaoDoRanking.Gravidade =>
                consulta.OrderByDescending(linha =>
                            linha.score.DespesasAvaliadas == 0
                                ? 0m
                                : linha.score.PesoFlagsAltas / linha.score.DespesasAvaliadas)
                        .ThenByDescending(linha => linha.score.TotalFlagsVermelhas)
                        .ThenBy(linha => linha.parlamentar.NomeParlamentar),

            _ => consulta.OrderByDescending(linha => linha.score.VolumeTotalGasto)
                         .ThenBy(linha => linha.parlamentar.NomeParlamentar),
        };

        var total = await consulta.CountAsync(cancelamento).ConfigureAwait(false);

        var itens = await consulta
            .Skip((numero - 1) * tamanho)
            .Take(tamanho)
            .Select(linha => new ItemDoRanking
            {
                Id = linha.parlamentar.Id,
                IdExterno = linha.parlamentar.IdExterno,
                NomeParlamentar = linha.parlamentar.NomeParlamentar,
                SiglaPartido = linha.parlamentar.SiglaPartido,
                Uf = linha.parlamentar.Uf,
                FotoUrl = linha.parlamentar.FotoUrl,
                Casa = nomeDaCasa,
                VolumeTotalGasto = linha.score.VolumeTotalGasto,
                DespesasAvaliadas = linha.score.DespesasAvaliadas,

                // Nulo, nao zero: zero diria que a densidade foi medida e deu nada.
                IndiceDeRisco = linha.score.AmostraSuficiente
                    ? linha.score.IndiceDeRisco
                    : (decimal?)null,
                IndiceDeAltas = linha.score.AmostraSuficiente && linha.score.DespesasAvaliadas > 0
                    ? linha.score.PesoFlagsAltas / linha.score.DespesasAvaliadas * 100m
                    : (decimal?)null,

                FlagsAltas = linha.score.TotalFlagsVermelhas,
                FlagsMedias = linha.score.TotalFlagsMedias,
                FlagsBaixas = linha.score.TotalFlagsBaixas,
                AmostraSuficiente = linha.score.AmostraSuficiente,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return new Pagina<ItemDoRanking>
        {
            Itens = itens,
            Total = total,
            NumeroDaPagina = numero,
            ItensPorPagina = tamanho,
        };
    }

    public async Task<PerfilDoParlamentar?> ObterPerfilAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        var cabecalho = await (
            from parlamentar in contexto.Parlamentares.AsNoTracking()
            where parlamentar.Id == parlamentarId
            join s in contexto.ScoresRisco.AsNoTracking()
                on parlamentar.Id equals s.ParlamentarId into scores
            from encontrado in scores.DefaultIfEmpty()
            select new { parlamentar, score = encontrado })
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        if (cabecalho is null)
        {
            return null;
        }

        var serie = await contexto.ConsolidadosMensais
            .AsNoTracking()
            .Where(mes => mes.ParlamentarId == parlamentarId)
            .OrderBy(mes => mes.Competencia)
            .Select(mes => new MesDoParlamentar
            {
                Ano = mes.Competencia.Ano,
                Mes = mes.Competencia.Mes,
                TotalLiquido = mes.TotalLiquido,
                TotalGlosa = mes.TotalGlosa,
                QuantidadeDeDespesas = mes.QuantidadeDeDespesas,
                DetalheDisponivel = mes.DetalheDisponivel,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var cobertura = await contexto.CoberturasDeRegra
            .AsNoTracking()
            .Where(regra => regra.ParlamentarId == parlamentarId)
            .OrderBy(regra => regra.CodigoRegra)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        // DISTINCT sobre o codigo (smallint) e a coluna de excecao, e nao sobre o nome:
        // o nome nao existe no banco, e' derivado do catalogo. Sai barato porque o indice
        // por parlamentar ja recorta a varredura.
        var tiposBrutos = await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .Select(despesa => new
            {
                despesa.CodigoTipoDespesa,
                despesa.TipoDespesaNaoCatalogado,
            })
            .Distinct()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var tipos = tiposBrutos
            .Select(bruto =>
                CatalogoDeTiposDeDespesa.NomeDe(bruto.CodigoTipoDespesa)
                ?? bruto.TipoDespesaNaoCatalogado)
            .Where(nome => !string.IsNullOrWhiteSpace(nome))
            .Select(nome => nome!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(nome => nome, StringComparer.Ordinal)
            .ToList();

        // Agrupado no banco: o parlamentar de maior volume tem milhares de despesas e
        // dezenas de achados, e só interessam as somas por regra.
        var achados = await (
            from flag in contexto.FlagsAnomalias.AsNoTracking()
            join despesa in contexto.Despesas.AsNoTracking()
                on flag.DespesaId equals despesa.Id
            where despesa.ParlamentarId == parlamentarId
            group flag by new { flag.CodigoRegra, flag.Gravidade } into grupo
            select new
            {
                grupo.Key.CodigoRegra,
                grupo.Key.Gravidade,
                Quantidade = grupo.Count(),
                Peso = grupo.Sum(flag => (int)flag.Gravidade * flag.GrauDeConfianca),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var fornecedoresDistintos = await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .Select(despesa => despesa.DocumentoFornecedor)
            .Distinct()
            .CountAsync(cancelamento)
            .ConfigureAwait(false);

        var score = cabecalho.score;
        var temAmostra = score is not null && score.AmostraSuficiente;

        var composicao = ComposicaoDoIndice.Montar(
            achados.Select(linha => new AchadosAgrupados(
                linha.CodigoRegra, linha.Gravidade, linha.Quantidade, linha.Peso)),
            score?.DespesasAvaliadas ?? 0);

        return new PerfilDoParlamentar
        {
            Id = cabecalho.parlamentar.Id,
            IdExterno = cabecalho.parlamentar.IdExterno,
            NomeParlamentar = cabecalho.parlamentar.NomeParlamentar,
            NomeCivil = cabecalho.parlamentar.NomeCivil,
            SiglaPartido = cabecalho.parlamentar.SiglaPartido,
            Uf = cabecalho.parlamentar.Uf,
            Casa = NomeDaCasa(cabecalho.parlamentar.Tipo),
            FotoUrl = cabecalho.parlamentar.FotoUrl,

            VolumeTotalGasto = score?.VolumeTotalGasto ?? 0m,
            DespesasAvaliadas = score?.DespesasAvaliadas ?? 0,
            IndiceDeRisco = temAmostra ? score!.IndiceDeRisco : null,
            IndiceDeAltas = temAmostra ? score!.IndiceDeAltas : null,
            AmostraSuficiente = temAmostra,
            FlagsAltas = score?.TotalFlagsVermelhas ?? 0,
            FlagsMedias = score?.TotalFlagsMedias ?? 0,
            FlagsBaixas = score?.TotalFlagsBaixas ?? 0,
            DataUltimaVarredura = score?.DataUltimaVarredura,
            VersaoDoMotor = cobertura.Count > 0 ? cobertura[0].VersaoDoMotor : 0,

            SerieMensal = serie,
            Cobertura = [.. cobertura.Select(Traduzir)],
            TiposDeDespesa = tipos,
            Composicao = composicao,
            FornecedoresDistintos = fornecedoresDistintos,
        };
    }

    public async Task<Pagina<ItemDeDespesa>> ListarDespesasAsync(
        Guid parlamentarId,
        FiltroDeDespesas filtro,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var (numero, tamanho) = Normalizar(filtro.Pagina, filtro.ItensPorPagina);

        var consulta = contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId);

        // Competencia e' objeto de valor com conversor para o inteiro AAAAMM; o LINQ nao
        // enxerga Ano e Mes separadamente, entao o filtro compara a competencia inteira.
        if (filtro.Ano is { } ano)
        {
            consulta = filtro.Mes is { } mes
                ? consulta.Where(d => d.Competencia == Domain.Comum.Competencia.Criar(ano, mes))
                : consulta.Where(d =>
                    d.Competencia >= Domain.Comum.Competencia.Criar(ano, 1)
                    && d.Competencia <= Domain.Comum.Competencia.Criar(ano, 12));
        }

        // O tipo e' filtrado NO BANCO, e nao em memoria depois da paginacao. Filtrar depois
        // do Take deixaria o Total contando o que o filtro descartou — a tela diria "1 de
        // 121" — e as paginas do fim viriam vazias, o que o leitor lê como "parou de gastar".
        // E' a mesma classe de mentira que faz o mes sem ano devolver 400 em vez de ignorar.
        if (!string.IsNullOrWhiteSpace(filtro.TipoDespesa))
        {
            var nome = filtro.TipoDespesa.Trim();
            var codigo = ResolverCodigoDoTipo(nome);

            // Fora do catalogo, a comparacao vai na coluna de excecao — onde a ingestao
            // preserva a grafia original de um tipo que a Camara criou depois.
            consulta = codigo == CatalogoDeTiposDeDespesa.NaoCatalogado
                ? consulta.Where(d => d.TipoDespesaNaoCatalogado == nome)
                : consulta.Where(d => d.CodigoTipoDespesa == codigo);
        }

        if (filtro.ComFlag is { } comFlag)
        {
            consulta = comFlag
                ? consulta.Where(d => contexto.FlagsAnomalias.Any(f => f.DespesaId == d.Id))
                : consulta.Where(d => !contexto.FlagsAnomalias.Any(f => f.DespesaId == d.Id));
        }

        var total = await consulta.CountAsync(cancelamento).ConfigureAwait(false);

        var pagina = await consulta
            .OrderByDescending(despesa => despesa.DataDocumento)
            .ThenBy(despesa => despesa.Id)
            .Skip((numero - 1) * tamanho)
            .Take(tamanho)
            .Select(despesa => new
            {
                despesa,
                flags = contexto.FlagsAnomalias
                    .Where(f => f.DespesaId == despesa.Id)
                    .OrderByDescending(f => f.Gravidade)
                    .ToList(),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var carteira = await contexto.Parlamentares
            .AsNoTracking()
            .Where(p => p.Id == parlamentarId)
            .Select(p => p.NumeroCarteira)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        var respostas = await RespostasDoGabineteAsync(
            [.. pagina.Select(linha => linha.despesa.Id)], cancelamento).ConfigureAwait(false);

        var itens = pagina
            .Select(linha => Traduzir(
                linha.despesa, linha.flags, carteira, respostas[linha.despesa.Id]))
            .ToList();

        return new Pagina<ItemDeDespesa>
        {
            Itens = itens,
            Total = total,
            NumeroDaPagina = numero,
            ItensPorPagina = tamanho,
        };
    }

    public async Task<PagamentosPorFornecedor?> ObterPagamentosPorFornecedorAsync(
        Guid parlamentarId,
        int quantos,
        CancellationToken cancelamento = default)
    {
        if (!await contexto.Parlamentares.AnyAsync(p => p.Id == parlamentarId, cancelamento)
                .ConfigureAwait(false))
        {
            return null;
        }

        // Em memória, e não agrupado no banco: a competência é objeto de valor com conversor
        // para AAAAMM, e o LINQ não enxerga o ano dentro dela. São alguns milhares de linhas
        // por parlamentar, recortadas pelo índice dele.
        var linhas = await contexto.Despesas
            .AsNoTracking()
            .Where(despesa => despesa.ParlamentarId == parlamentarId)
            .Select(despesa => new
            {
                despesa.DocumentoFornecedor,
                despesa.NomeFornecedor,
                despesa.Competencia,
                despesa.ValorLiquido,
                despesa.DataDocumento,
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var pagamentos = linhas
            .Select(linha => new PagamentoBruto(
                linha.DocumentoFornecedor.Formatado,
                linha.NomeFornecedor,
                linha.Competencia.Ano,
                linha.Competencia.Mes,
                linha.ValorLiquido,
                linha.DataDocumento))
            .ToList();

        return EscadaDePagamentos.Montar(pagamentos, Math.Clamp(quantos, 1, 30));
    }

    public async Task<QuemBeneficiaQuem?> ObterSociosEmComumAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default)
    {
        if (!await contexto.Parlamentares.AnyAsync(p => p.Id == parlamentarId, cancelamento)
                .ConfigureAwait(false))
        {
            return null;
        }

        // 1. Os fornecedores dele que passam do piso. Só eles podem abrir uma cadeia.
        var fornecedoresDoParlamentar = await contexto.Despesas
            .AsNoTracking()
            .Where(d => d.ParlamentarId == parlamentarId && d.FornecedorId != null)
            .GroupBy(d => d.FornecedorId!.Value)
            .Select(g => new { FornecedorId = g.Key, Valor = g.Sum(d => d.ValorLiquido) })
            .Where(g => g.Valor >= Domain.Rede.CriteriosDaRede.ValorMinimoDaLigacao)
            .Select(g => g.FornecedorId)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        if (fornecedoresDoParlamentar.Count == 0)
        {
            return new QuemBeneficiaQuem();
        }

        // 2. Os sócios deles, e TODAS as empresas de quem tem o mesmo nome — inclusive os
        //    homônimos, que é como a ambiguidade aparece e é descartada.
        var nomes = await contexto.Sociedades
            .AsNoTracking()
            .Where(s => fornecedoresDoParlamentar.Contains(s.FornecedorId))
            .Select(s => s.NomeSocio)
            .Distinct()
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var sociedades = await contexto.Sociedades
            .AsNoTracking()
            .Where(s => nomes.Contains(s.NomeSocio))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var vinculos = sociedades
            .Select(s => new VinculoDeSocio(s.FornecedorId, s.NomeSocio, s.Cpf.Mascarado, s.Qualificacao))
            .Distinct()
            .ToList();

        var ambiguos = vinculos
            .GroupBy(v => v.Nome, StringComparer.Ordinal)
            .Where(g => g.Select(v => v.CpfMascarado).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var empresaIds = vinculos.Select(v => v.FornecedorId).Distinct().ToList();

        // 3. Quem pagou a cada uma dessas empresas. Com todos os valores, e não só os acima do
        //    piso: o alcance do sócio conta qualquer parlamentar que as contratou.
        var pagamentos = await contexto.Despesas
            .AsNoTracking()
            .Where(d => d.FornecedorId != null && empresaIds.Contains(d.FornecedorId!.Value))
            .GroupBy(d => new { d.ParlamentarId, FornecedorId = d.FornecedorId!.Value })
            .Select(g => new PagamentoAgregado(g.Key.ParlamentarId, g.Key.FornecedorId, g.Sum(d => d.ValorLiquido)))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var empresas = (await contexto.Fornecedores
            .AsNoTracking()
            .Where(f => empresaIds.Contains(f.Id))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false))
            .ToDictionary(
                f => f.Id,
                f => new EmpresaDaCadeia(f.Id, f.Documento.Numero, f.Documento.Formatado, f.RazaoSocial));

        var parlamentarIds = pagamentos.Select(p => p.ParlamentarId).Distinct().ToList();

        var parlamentares = (await contexto.Parlamentares
            .AsNoTracking()
            .Where(p => parlamentarIds.Contains(p.Id))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false))
            .ToDictionary(
                p => p.Id,
                p => new ParlamentarDaDespesa
                {
                    Id = p.Id,
                    NomeParlamentar = p.NomeParlamentar,
                    SiglaPartido = p.SiglaPartido,
                    Uf = p.Uf,
                    Casa = NomeDaCasa(p.Tipo),
                    FotoUrl = p.FotoUrl,
                });

        return CadeiasDeSocios.Montar(
            parlamentarId, pagamentos, vinculos, ambiguos, empresas, parlamentares);
    }

    /// <summary>Teto do feed. Leitor de RSS guarda o que já viu; cem itens cobrem meses.</summary>
    public const int MaximoDeAchadosRecentes = 100;

    public async Task<IReadOnlyList<AchadoRecente>?> ListarAchadosRecentesAsync(
        Guid? parlamentarId,
        int itens,
        CancellationToken cancelamento = default)
    {
        var tamanho = Math.Clamp(itens, 1, MaximoDeAchadosRecentes);

        if (parlamentarId is { } pedido
            && !await contexto.Parlamentares.AnyAsync(p => p.Id == pedido, cancelamento)
                .ConfigureAwait(false))
        {
            return null;
        }

        var consulta = contexto.Despesas
            .AsNoTracking()
            .Where(despesa => contexto.FlagsAnomalias.Any(flag => flag.DespesaId == despesa.Id));

        if (parlamentarId is { } id)
        {
            consulta = consulta.Where(despesa => despesa.ParlamentarId == id);
        }

        // Pelo id, e não pela data do documento: o id é UUID v7, ordenado pelo momento em que
        // a despesa entrou na base — que é o que "recente" quer dizer num alerta. Pela data do
        // documento, uma nota antiga que a Câmara só publicou agora ficaria lá embaixo, e ela é
        // tão novidade quanto a de ontem.
        var despesas = await consulta
            .OrderByDescending(despesa => despesa.Id)
            .Take(tamanho)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var ids = despesas.Select(despesa => despesa.Id).ToList();
        var donos = despesas.Select(despesa => despesa.ParlamentarId).Distinct().ToList();

        var flags = (await contexto.FlagsAnomalias
            .AsNoTracking()
            .Where(flag => ids.Contains(flag.DespesaId))
            .ToListAsync(cancelamento)
            .ConfigureAwait(false))
            .ToLookup(flag => flag.DespesaId);

        var parlamentares = await contexto.Parlamentares
            .AsNoTracking()
            .Where(parlamentar => donos.Contains(parlamentar.Id))
            .ToDictionaryAsync(parlamentar => parlamentar.Id, cancelamento)
            .ConfigureAwait(false);

        return [.. despesas.Select(despesa =>
        {
            var autor = parlamentares[despesa.ParlamentarId];

            return new AchadoRecente
            {
                DespesaId = despesa.Id,
                RegistradaEm = Domain.Comum.Entidade.MomentoDaCriacao(despesa.Id),
                Parlamentar = new ParlamentarDaDespesa
                {
                    Id = autor.Id,
                    NomeParlamentar = autor.NomeParlamentar,
                    SiglaPartido = autor.SiglaPartido,
                    Uf = autor.Uf,
                    Casa = NomeDaCasa(autor.Tipo),
                    FotoUrl = autor.FotoUrl,
                },
                TipoDespesa = despesa.TipoDespesa,
                NomeFornecedor = despesa.NomeFornecedor,
                DocumentoFornecedor = despesa.DocumentoFornecedor.Formatado,
                ValorLiquido = despesa.ValorLiquido,
                DataDocumento = despesa.DataDocumento,
                Ano = despesa.Competencia.Ano,
                Mes = despesa.Competencia.Mes,
                Regras = [.. flags[despesa.Id]
                    .OrderByDescending(flag => flag.Gravidade)
                    .ThenBy(flag => flag.CodigoRegra, StringComparer.Ordinal)
                    .Select(flag => new RegraDoAchado
                    {
                        Codigo = flag.CodigoRegra,
                        Gravidade = flag.Gravidade,
                        DescricaoDaRegra = CatalogoDeRegras.DescricaoDe(flag.CodigoRegra),
                        Detalhe = flag.Descricao,
                    })],
            };
        })];
    }

    public async Task<DespesaDetalhada?> ObterDespesaAsync(
        Guid despesaId,
        CancellationToken cancelamento = default)
    {
        var despesa = await contexto.Despesas
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == despesaId, cancelamento)
            .ConfigureAwait(false);

        if (despesa is null)
        {
            return null;
        }

        var flags = await contexto.FlagsAnomalias
            .AsNoTracking()
            .Where(f => f.DespesaId == despesaId)
            .OrderByDescending(f => f.Gravidade)
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        var autor = await contexto.Parlamentares
            .AsNoTracking()
            .Where(p => p.Id == despesa.ParlamentarId)
            .Select(p => new
            {
                p.Id,
                p.NomeParlamentar,
                p.SiglaPartido,
                p.Uf,
                p.Tipo,
                p.FotoUrl,
                p.NumeroCarteira,
            })
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        // Despesa sem parlamentar nao existe: a FK e' obrigatoria. Se acontecesse, seria
        // melhor devolver 404 do que publicar um achado sem dono.
        if (autor is null)
        {
            return null;
        }

        var respostas = await RespostasDoGabineteAsync([despesaId], cancelamento)
            .ConfigureAwait(false);

        return new DespesaDetalhada
        {
            Despesa = Traduzir(despesa, flags, autor.NumeroCarteira, respostas[despesaId]),
            Parlamentar = new ParlamentarDaDespesa
            {
                Id = autor.Id,
                NomeParlamentar = autor.NomeParlamentar,
                SiglaPartido = autor.SiglaPartido,
                Uf = autor.Uf,
                Casa = NomeDaCasa(autor.Tipo),
                FotoUrl = autor.FotoUrl,
            },
        };
    }

    /// <summary>
    /// As respostas publicadas do gabinete, por despesa. A retirada não sai: o gabinete
    /// retirou, e a página respeita — o registro fica no banco.
    /// </summary>
    private async Task<ILookup<Guid, RespostaDoGabinete>> RespostasDoGabineteAsync(
        IReadOnlyCollection<Guid> despesaIds,
        CancellationToken cancelamento)
    {
        if (despesaIds.Count == 0)
        {
            return Array.Empty<RespostaDoGabinete>().ToLookup(_ => Guid.Empty);
        }

        var linhas = await contexto.Contestacoes
            .AsNoTracking()
            .Where(contestacao => despesaIds.Contains(contestacao.DespesaId)
                && contestacao.Situacao == Domain.Contestacoes.SituacaoDaContestacao.Publicada)
            .OrderBy(contestacao => contestacao.EnviadaEm)
            .Select(contestacao => new
            {
                contestacao.DespesaId,
                Resposta = new RespostaDoGabinete
                {
                    Texto = contestacao.Texto,
                    UrlDoAnexo = contestacao.UrlDoAnexo,
                    EnviadaEm = contestacao.EnviadaEm,
                },
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return linhas.ToLookup(linha => linha.DespesaId, linha => linha.Resposta);
    }

    /// <summary>O nome por extenso da casa, escrito num lugar só.</summary>
    private static string NomeDaCasa(TipoParlamentar tipo) =>
        tipo == TipoParlamentar.Senador ? "Senado Federal" : "Câmara dos Deputados";

    /// <summary>
    /// O codigo do tipo, tolerando diferenca de caixa no nome.
    ///
    /// <para>O catalogo casa por <see cref="StringComparer.Ordinal"/>, que e' o certo para a
    /// ingestao — grafia exata, inclusive o ponto final que uns tipos tem e outros nao. Mas
    /// aqui o nome chega de um consumidor da API, que pode te-lo reescrito. Resolver a caixa
    /// em C#, antes da consulta, evita depender do collation do banco (que e' <c>C</c>,
    /// ordinal) e mantem a comparacao em SQL sobre um <c>smallint</c>.</para>
    /// </summary>
    private static short ResolverCodigoDoTipo(string nome)
    {
        var codigo = CatalogoDeTiposDeDespesa.CodigoDe(nome);

        if (codigo != CatalogoDeTiposDeDespesa.NaoCatalogado)
        {
            return codigo;
        }

        var equivalente = CatalogoDeTiposDeDespesa.Nomes
            .FirstOrDefault(catalogado =>
                string.Equals(catalogado, nome, StringComparison.OrdinalIgnoreCase));

        return equivalente is null
            ? CatalogoDeTiposDeDespesa.NaoCatalogado
            : CatalogoDeTiposDeDespesa.CodigoDe(equivalente);
    }

    private static ItemDeDespesa Traduzir(
        Despesa despesa,
        IReadOnlyList<FlagAnomalia> flags,
        int? numeroCarteira,
        IEnumerable<RespostaDoGabinete> respostas) =>
        new()
        {
            Id = despesa.Id,
            Ano = despesa.Competencia.Ano,
            Mes = despesa.Competencia.Mes,
            TipoDespesa = despesa.TipoDespesa,
            DataDocumento = despesa.DataDocumento,
            NomeFornecedor = despesa.NomeFornecedor,

            // Formatado, nunca cru: CNPJ sai inteiro porque e' dado de pessoa juridica e
            // publico; CPF sai sempre mascarado. A borda nao decide isso — o objeto de
            // valor decide, e a borda nao tem como contornar.
            DocumentoFornecedor = despesa.DocumentoFornecedor.Formatado,

            ValorLiquido = despesa.ValorLiquido,
            ValorDocumento = despesa.ValorDocumento,
            ValorGlosa = despesa.ValorGlosa,
            TemDocumentoPublicado = despesa.TemDocumentoPublicado,
            UrlDocumento = despesa.UrlPublica(numeroCarteira),
            EhEstorno = despesa.EhEstorno,

            Flags = [.. flags.Select(flag => new FlagDaDespesa
            {
                CodigoRegra = flag.CodigoRegra,
                Gravidade = flag.Gravidade,
                Descricao = flag.Descricao,
                GrauDeConfianca = flag.GrauDeConfianca,
                VersaoDoMotor = flag.VersaoDoMotor,
            })],

            Respostas = [.. respostas],
        };

    private static CoberturaDeRegra Traduzir(CoberturaDoParlamentar cobertura) =>
        new()
        {
            Codigo = cobertura.CodigoRegra,

            // A redacao de leitor vem do dominio, e nao da persistencia, porque melhorar o
            // texto da tela nao pode exigir reprocessar 513 parlamentares. O que o banco
            // guarda e' o codigo; o que a pagina mostra e' resolvido na leitura.
            Descricao = CatalogoDeRegras.DescricaoDe(cobertura.CodigoRegra),

            Situacao = cobertura.Situacao,
            DespesasComDado = cobertura.DespesasComDado,
            DespesasSemDado = cobertura.DespesasSemDado,
            FlagsEmitidas = cobertura.FlagsEmitidas,
            SilencioEhConclusivo = cobertura.SilencioEhConclusivo,
            MotivoDaAbstencao = MotivoDe(cobertura),
        };

    /// <summary>
    /// Traduz a ausência de dado numa frase que a tela pode exibir.
    ///
    /// Sem isto a interface teria de conhecer o enum <see cref="DadoExigido"/> e redigir a
    /// explicação por conta própria — e cada consumidor da API escreveria a sua, que é
    /// exatamente como "nada irregular encontrado" acabaria aparecendo em algum lugar.
    /// </summary>
    private static string? MotivoDe(CoberturaDoParlamentar cobertura)
    {
        if (cobertura.Situacao == SituacaoDaRegra.Falhou)
        {
            return "A regra falhou nesta passada; o resultado está incompleto.";
        }

        if (cobertura.SilencioEhConclusivo && cobertura.DespesasSemDado == 0)
        {
            return null;
        }

        var faltando = new List<string>();

        if (cobertura.DadoAusente.HasFlag(DadoExigido.CadastroDoFornecedor))
        {
            faltando.Add("o CNPJ não resolveu para um fornecedor conhecido");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.OpcaoPeloMei))
        {
            faltando.Add("a opção pelo MEI não é informada pela fonte de cadastro");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.TetoDaCota))
        {
            faltando.Add("não há teto de cota publicado para o período");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.TipoNaTabelaCurada))
        {
            faltando.Add("o tipo de despesa está fora da tabela de referência");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.AberturaDoFornecedor))
        {
            faltando.Add("a data de abertura do fornecedor é desconhecida");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.EnderecoDoFornecedor))
        {
            faltando.Add("o endereço do fornecedor é desconhecido");
        }

        if (cobertura.DadoAusente.HasFlag(DadoExigido.SituacaoDoFornecedor))
        {
            faltando.Add("a situação cadastral do fornecedor é desconhecida");
        }

        if (faltando.Count == 0)
        {
            return cobertura.DespesasSemDado > 0
                ? $"{cobertura.DespesasSemDado} despesas não puderam ser avaliadas."
                : null;
        }

        var alcance = cobertura.Situacao == SituacaoDaRegra.SemDado
            ? "Nenhuma despesa pôde ser avaliada"
            : $"{cobertura.DespesasSemDado} de {cobertura.DespesasVistas} despesas não puderam ser avaliadas";

        return $"{alcance}: {string.Join("; ", faltando)}.";
    }

    /// <summary>
    /// Página e tamanho válidos. Teto porque a paginação é a superfície mais fácil de
    /// abusar numa API pública sem autenticação.
    /// </summary>
    private static (int Numero, int Tamanho) Normalizar(int pagina, int itensPorPagina) =>
        (Math.Max(1, pagina), Math.Clamp(itensPorPagina, 1, MaximoDeItensPorPagina));
}
