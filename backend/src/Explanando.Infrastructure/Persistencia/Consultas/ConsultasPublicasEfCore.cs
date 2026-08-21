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
            Casa = cabecalho.parlamentar.Tipo == TipoParlamentar.Senador
                ? "Senado Federal"
                : "Câmara dos Deputados",
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

        // O filtro por tipo fica em memoria de proposito: TipoDespesa e' derivado do
        // catalogo e nao existe como coluna. Filtrar no banco exigiria traduzir o nome
        // para codigo aqui, o que duplicaria o catalogo em SQL.
        var carteira = await contexto.Parlamentares
            .AsNoTracking()
            .Where(p => p.Id == parlamentarId)
            .Select(p => p.NumeroCarteira)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        var itens = pagina
            .Where(linha => filtro.TipoDespesa is null
                || string.Equals(linha.despesa.TipoDespesa, filtro.TipoDespesa,
                    StringComparison.OrdinalIgnoreCase))
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

    public async Task<ItemDeDespesa?> ObterDespesaAsync(
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

        var carteira = await contexto.Parlamentares
            .AsNoTracking()
            .Where(p => p.Id == despesa.ParlamentarId)
            .Select(p => p.NumeroCarteira)
            .FirstOrDefaultAsync(cancelamento)
            .ConfigureAwait(false);

        return Traduzir(despesa, flags, carteira);
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
