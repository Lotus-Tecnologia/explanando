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
        int pagina,
        int itensPorPagina,
        CancellationToken cancelamento = default)
    {
        var (numero, tamanho) = Normalizar(pagina, itensPorPagina);

        var consulta =
            from score in contexto.ScoresRisco.AsNoTracking()
            join parlamentar in contexto.Parlamentares.AsNoTracking()
                on score.ParlamentarId equals parlamentar.Id
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

        var score = cabecalho.score;
        var temAmostra = score is not null && score.AmostraSuficiente;

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

        var itens = pagina
            .Select(linha => Traduzir(linha.despesa, linha.flags, carteira))
            .ToList();

        return new Pagina<ItemDeDespesa>
        {
            Itens = itens,
            Total = total,
            NumeroDaPagina = numero,
            ItensPorPagina = tamanho,
        };
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

        return new DespesaDetalhada
        {
            Despesa = Traduzir(despesa, flags, autor.NumeroCarteira),
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
        int? numeroCarteira) =>
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
        };

    private static CoberturaDeRegra Traduzir(CoberturaDoParlamentar cobertura) =>
        new()
        {
            Codigo = cobertura.CodigoRegra,
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
