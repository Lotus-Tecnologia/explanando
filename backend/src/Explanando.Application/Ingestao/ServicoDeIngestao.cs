using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Ingestao;

/// <summary>Porta de persistencia da ingestao. Implementada na infraestrutura.</summary>
public interface IRepositorioDeIngestao
{
    Task<Parlamentar?> ObterParlamentarAsync(int idExterno, CancellationToken cancelamento = default);

    Task<Fornecedor?> ObterFornecedorAsync(CpfCnpj documento, CancellationToken cancelamento = default);

    /// <summary>Hashes ja gravados para o parlamentar — a base da idempotencia da coleta.</summary>
    Task<HashSet<byte[]>> ObterHashesDeDespesasAsync(
        Guid parlamentarId,
        CancellationToken cancelamento = default);

    void Adicionar(Parlamentar parlamentar);

    void Adicionar(Fornecedor fornecedor);

    void Adicionar(Sociedade sociedade);

    void Adicionar(Despesa despesa);

    Task SalvarAsync(CancellationToken cancelamento = default);
}

/// <summary>Uma despesa que a fonte trouxe mas que nao pode ser gravada, e por que.</summary>
public sealed record DespesaRejeitada(string CodigoDocumento, string Motivo);

/// <summary>
/// O que a coleta produziu. Serve tanto de log quanto de alarme: numero e' o unico jeito
/// de distinguir "o deputado nao gastou" de "a coleta quebrou em silencio".
/// </summary>
public sealed record ResultadoDaIngestao
{
    public int DeputadosProcessados { get; init; }
    public int DespesasNovas { get; init; }
    public int DespesasJaConhecidas { get; init; }
    public int FornecedoresNovos { get; init; }

    /// <summary>
    /// Fornecedores que ficaram sem enriquecimento porque a fonte de cadastro falhou.
    ///
    /// Precisa ser visivel: sem cadastro, as regras de KYB se abstem sobre aquelas despesas.
    /// Um numero alto aqui nao significa "nada encontrado" — significa "nao deu para olhar",
    /// e as duas coisas parecem iguais no resultado final se ninguem contar.
    /// </summary>
    public int FornecedoresNaoEnriquecidos { get; init; }

    public IReadOnlyList<DespesaRejeitada> Rejeitadas { get; init; } = [];
    public IReadOnlyList<string> AlertasDeSanidade { get; init; } = [];

    public bool TemAlerta => AlertasDeSanidade.Count > 0;
}

/// <summary>
/// Orquestra a coleta: busca na fonte, traduz, resolve fornecedor e persiste.
///
/// Tres decisoes centrais, todas herdadas de descobertas da Etapa 01:
///
/// 1. <b>Coleta vazia para deputado em exercicio e' alarme, nao resultado.</b> Sem
///    idLegislatura a API da Camara responde 200 com lista vazia; um ingestor que aceite
///    isso em silencio grava zero despesas e reporta sucesso.
/// 2. <b>Despesa sem fornecedor resolvivel ainda e' gravada.</b> O CNPJ sentinela aparece
///    em 2% das notas; descartar essas despesas esconderia gasto publico, que e' o oposto
///    do proposito do produto.
/// 3. <b>Registro que o dominio recusa vira rejeicao contada, nao excecao.</b> Uma nota
///    com valores inconsistentes nao pode derrubar a coleta das outras 1.199.
/// </summary>
public sealed class ServicoDeIngestao(
    IFonteDeParlamentares fonteDeParlamentares,
    IFonteDeCadastroEmpresarial fonteDeCadastro,
    IRepositorioDeIngestao repositorio)
{
    public async Task<ResultadoDaIngestao> IngerirDeputadoAsync(
        DeputadoDaFonte deputado,
        int legislatura,
        CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(deputado);

        var rejeitadas = new List<DespesaRejeitada>();
        var alertas = new List<string>();

        var parlamentar = await GarantirParlamentarAsync(deputado, cancelamento).ConfigureAwait(false);
        var conhecidos = await repositorio.ObterHashesDeDespesasAsync(parlamentar.Id, cancelamento)
            .ConfigureAwait(false);

        // Cache por documento dentro da execucao: um deputado costuma repetir os mesmos
        // fornecedores dezenas de vezes, e cada consulta e' uma chamada a um servico gratuito.
        var fornecedoresResolvidos = new Dictionary<string, Guid?>(StringComparer.Ordinal);

        var novas = 0;
        var repetidas = 0;
        var fornecedoresNovos = 0;
        var naoEnriquecidos = 0;
        var vindasDaFonte = 0;

        await foreach (var bruta in fonteDeParlamentares
            .ListarDespesasAsync(deputado.IdExterno, legislatura, cancelamento)
            .ConfigureAwait(false))
        {
            vindasDaFonte++;

            Despesa despesa;

            try
            {
                despesa = Despesa.Criar(Tradutor.ParaNovaDespesa(bruta, parlamentar.Id));
            }
            catch (Exception erro) when (erro is ArgumentException or ArgumentOutOfRangeException)
            {
                // Uma nota malformada nao pode derrubar a coleta das outras.
                rejeitadas.Add(new DespesaRejeitada(bruta.CodigoDocumento, erro.Message));
                continue;
            }

            if (!conhecidos.Add(despesa.HashUnico))
            {
                repetidas++;
                continue;
            }

            // A carteira parlamentar so' existe dentro da URL do documento, que nao e'
            // persistida. Extrai-la aqui e' a unica chance: depois desta passada, o
            // endereco original nao existe mais em lugar nenhum.
            if (parlamentar.NumeroCarteira is null
                && UrlDeDocumento.Analisar(bruta.UrlDocumento).NumeroCarteira is { } carteira)
            {
                parlamentar.RegistrarNumeroDeCarteira(carteira);
            }

            var (fornecedorId, criouFornecedor, falhouEnriquecimento) = await ResolverFornecedorAsync(
                despesa.DocumentoFornecedor, fornecedoresResolvidos, cancelamento).ConfigureAwait(false);

            if (criouFornecedor)
            {
                fornecedoresNovos++;
            }

            if (falhouEnriquecimento)
            {
                naoEnriquecidos++;
            }

            if (fornecedorId is { } id)
            {
                despesa.AssociarFornecedor(id);
            }

            repositorio.Adicionar(despesa);
            novas++;
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        if (vindasDaFonte == 0 && deputado.EmExercicio)
        {
            alertas.Add(
                $"Deputado {deputado.IdExterno} ({deputado.NomeParlamentar}) esta em exercicio "
                + $"mas a fonte devolveu zero despesas na legislatura {legislatura}. Isso e' quase "
                + "sempre falha de coleta, nao ausencia de gasto — conferir se idLegislatura foi enviado.");
        }

        return new ResultadoDaIngestao
        {
            DeputadosProcessados = 1,
            DespesasNovas = novas,
            DespesasJaConhecidas = repetidas,
            FornecedoresNovos = fornecedoresNovos,
            FornecedoresNaoEnriquecidos = naoEnriquecidos,
            Rejeitadas = rejeitadas,
            AlertasDeSanidade = alertas,
        };
    }

    private async Task<Parlamentar> GarantirParlamentarAsync(
        DeputadoDaFonte deputado,
        CancellationToken cancelamento)
    {
        var existente = await repositorio.ObterParlamentarAsync(deputado.IdExterno, cancelamento)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            return existente;
        }

        var novo = Parlamentar.Criar(Tradutor.ParaNovoParlamentar(deputado));
        repositorio.Adicionar(novo);
        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        return novo;
    }

    /// <summary>
    /// Devolve o id do fornecedor, ou nulo quando o documento nao identifica ninguem
    /// (sentinela, vazio ou invalido). Nulo aqui nao impede a despesa de ser gravada.
    ///
    /// <para><b>Falha da fonte de cadastro nao derruba o deputado.</b> Na primeira coleta
    /// real, uma consulta de CNPJ que esgotou as tentativas por rate-limit propagava a
    /// excecao e abortava a ingestao inteira daquele parlamentar — mil despesas ja gravadas
    /// ficavam sem checkpoint, e a proxima passada recomecava do zero. Uma falha transitoria
    /// ao enriquecer <b>um</b> fornecedor nao pode custar as outras 1.199 despesas.</para>
    ///
    /// <para>O caso degradado e' o mesmo de "CNPJ desconhecido pela fonte": a despesa e'
    /// gravada sem vinculo e as regras de KYB se abstem sobre ela. A diferenca e' que este
    /// caso e' <b>contado a parte</b>, porque "nao deu para olhar" nao pode se confundir com
    /// "olhei e nao achei nada".</para>
    /// </summary>
    private async Task<(Guid? Id, bool Criou, bool FalhouEnriquecimento)> ResolverFornecedorAsync(
        CpfCnpj documento,
        Dictionary<string, Guid?> cache,
        CancellationToken cancelamento)
    {
        if (!documento.PodeSerEnriquecido)
        {
            return (null, false, false);
        }

        if (cache.TryGetValue(documento.Numero, out var doCache))
        {
            return (doCache, false, false);
        }

        var existente = await repositorio.ObterFornecedorAsync(documento, cancelamento)
            .ConfigureAwait(false);

        if (existente is not null)
        {
            cache[documento.Numero] = existente.Id;
            return (existente.Id, false, false);
        }

        EmpresaDaFonte? empresa;

        try
        {
            empresa = await fonteDeCadastro.ConsultarAsync(documento.Numero, cancelamento)
                .ConfigureAwait(false);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nao cacheia: a proxima passada tenta de novo este CNPJ.
            return (null, false, true);
        }

        if (empresa is null)
        {
            // Fonte nao conhece o CNPJ. A despesa continua valendo, so' fica sem
            // enriquecimento — e sem enriquecimento as regras de KYB simplesmente se abstem.
            cache[documento.Numero] = null;
            return (null, false, false);
        }

        var fornecedor = Fornecedor.Criar(Tradutor.ParaNovoFornecedor(empresa));
        repositorio.Adicionar(fornecedor);

        foreach (var socio in empresa.QuadroSocietario)
        {
            repositorio.Adicionar(Sociedade.Criar(
                fornecedor.Id,
                socio.NomeSocio,
                socio.CpfMascarado,
                socio.Qualificacao,
                socio.DataEntradaSociedade));
        }

        await repositorio.SalvarAsync(cancelamento).ConfigureAwait(false);

        cache[documento.Numero] = fornecedor.Id;
        return (fornecedor.Id, true, false);
    }
}
