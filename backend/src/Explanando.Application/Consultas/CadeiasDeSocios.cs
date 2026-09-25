using Explanando.Domain.Rede;

namespace Explanando.Application.Consultas;

/// <summary>Quanto um parlamentar pagou a um fornecedor, somado.</summary>
public sealed record PagamentoAgregado(Guid ParlamentarId, Guid FornecedorId, decimal Valor);

/// <summary>Um sócio de uma empresa, como o quadro societário da Receita o publica.</summary>
public sealed record VinculoDeSocio(Guid FornecedorId, string Nome, string CpfMascarado, string? Qualificacao);

/// <summary>Uma empresa, com o CNPJ em dígitos (para a raiz) e formatado (para a tela).</summary>
public sealed record EmpresaDaCadeia(Guid Id, string Digitos, string Documento, string RazaoSocial);

/// <summary>Uma empresa numa cadeia, e quanto o parlamentar daquela ponta pagou a ela.</summary>
public sealed record EmpresaNaCadeia
{
    public required string Documento { get; init; }

    public required string RazaoSocial { get; init; }

    public decimal Valor { get; init; }
}

/// <summary>O parlamentar da outra ponta, e quanto ele pagou à outra empresa.</summary>
public sealed record OutroParlamentar
{
    public required ParlamentarDaDespesa Parlamentar { get; init; }

    public decimal Valor { get; init; }
}

/// <summary>Uma pessoa sócia das duas empresas, e o papel dela em cada uma.</summary>
public sealed record SocioEmComum
{
    /// <summary>
    /// O nome como o quadro societário da Receita o publica.
    ///
    /// <para><b>Publicado por decisão do Gustavo em 2026-09-25</b> (plano de execução, Fase 5).
    /// É dado público no QSA, e é o que deixa a ligação verificável num clique. O CPF — mesmo
    /// mascarado — continua de fora: ele serve para casar, não para mostrar.</para>
    /// </summary>
    public required string Nome { get; init; }

    /// <summary>"Sócio-Administrador nas duas", ou uma qualificação em cada.</summary>
    public required string Qualificacao { get; init; }
}

/// <summary>
/// Parlamentar → fornecedor → sócio em comum → outra empresa → outro parlamentar.
/// </summary>
public sealed record CadeiaDeSocio
{
    public required EmpresaNaCadeia Fornecedor { get; init; }

    public required EmpresaNaCadeia OutraEmpresa { get; init; }

    /// <summary>Quantas pessoas são sócias das duas empresas ao mesmo tempo.</summary>
    public int SociosEmComum { get; init; }

    /// <summary>Quem é sócio das duas, e em que papel.</summary>
    public IReadOnlyList<SocioEmComum> Socios { get; init; } = [];

    public IReadOnlyList<OutroParlamentar> OutrosParlamentares { get; init; } = [];
}

/// <summary>A resposta da Fase 5 para um parlamentar.</summary>
public sealed record QuemBeneficiaQuem
{
    public IReadOnlyList<CadeiaDeSocio> Cadeias { get; init; } = [];

    /// <summary>Quantas cadeias passaram pelos cortes, além das listadas.</summary>
    public int TotalDeCadeias { get; init; }

    public int ParlamentaresMaximosPorSocio { get; } = CriteriosDaRede.ParlamentaresMaximos;

    public decimal ValorMinimoPorPonta { get; } = CriteriosDaRede.ValorMinimoDaLigacao;

    public string Ressalva { get; } = CadeiasDeSocios.Ressalva;
}

/// <summary>
/// Monta as cadeias de sócio em comum — a Fase 5 do plano de execução.
///
/// <para>Quatro cortes, e cada um existe por uma medição na base (2026-09-24):</para>
/// <list type="number">
/// <item><b>Casamento literal</b> de nome + CPF mascarado. Os nomes que aparecem com mais de um
/// CPF — 75 na base — ficam de fora inteiros: não há como saber se são a mesma pessoa, e
/// ligar pelo nome seria afirmar que são.</item>
/// <item><b>Sócio de alcance alto não liga ninguém.</b> Quem alcança mais de
/// <see cref="CriteriosDaRede.ParlamentaresMaximos"/> parlamentares por suas empresas é
/// infraestrutura (1.117 alcançam 20 ou mais), como a companhia aérea do mapa de fornecedores.</item>
/// <item><b>R$ 50 mil em cada ponta</b>, o piso do ADR-0017. Sem ele, um deputado tinha 271
/// ligações — a maioria notas isoladas, que não são relação.</item>
/// <item><b>Filial não é outra empresa.</b> Mesma raiz de CNPJ é a mesma pessoa jurídica.</item>
/// </list>
/// </summary>
public static class CadeiasDeSocios
{
    public const int CadeiasPadrao = 10;

    public const string Ressalva =
        "Ter sócio em comum não é irregularidade. Empresas com os mesmos donos atendem clientes "
        + "diferentes o tempo todo, e contratar uma delas é lícito. A ligação mostra uma "
        + "coincidência que pode ser conferida no quadro societário de cada CNPJ, na Receita "
        + "Federal; o que ela significa, se significa algo, cabe a quem lê.";

    public static QuemBeneficiaQuem Montar(
        Guid parlamentarId,
        IReadOnlyCollection<PagamentoAgregado> pagamentos,
        IReadOnlyCollection<VinculoDeSocio> vinculos,
        IReadOnlySet<string> nomesAmbiguos,
        IReadOnlyDictionary<Guid, EmpresaDaCadeia> empresas,
        IReadOnlyDictionary<Guid, ParlamentarDaDespesa> parlamentares,
        int quantas = CadeiasPadrao)
    {
        ArgumentNullException.ThrowIfNull(pagamentos);
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(nomesAmbiguos);
        ArgumentNullException.ThrowIfNull(empresas);
        ArgumentNullException.ThrowIfNull(parlamentares);

        var porEmpresa = pagamentos.ToLookup(pagamento => pagamento.FornecedorId);

        // A chave do sócio é o par, e literal. Nome sozinho juntaria homônimos.
        var socios = vinculos
            .Where(vinculo => !nomesAmbiguos.Contains(vinculo.Nome))
            .GroupBy(vinculo => (vinculo.Nome, vinculo.CpfMascarado))
            .Select(grupo => grupo.ToList())
            .ToList();

        var ligacoes = new List<(Guid F, Guid G, (string, string) Socio, string? QualF, string? QualG, Guid Q, decimal VQ)>();

        foreach (var empresasDoSocio in socios)
        {
            var socio = (empresasDoSocio[0].Nome, empresasDoSocio[0].CpfMascarado);

            var alcance = empresasDoSocio
                .SelectMany(vinculo => porEmpresa[vinculo.FornecedorId])
                .Select(pagamento => pagamento.ParlamentarId)
                .Distinct()
                .Count();

            if (alcance is < CriteriosDaRede.ParlamentaresMinimos or > CriteriosDaRede.ParlamentaresMaximos)
            {
                continue;
            }

            foreach (var doParlamentar in empresasDoSocio)
            {
                var pagoPeloParlamentar = porEmpresa[doParlamentar.FornecedorId]
                    .Where(pagamento => pagamento.ParlamentarId == parlamentarId)
                    .Sum(pagamento => pagamento.Valor);

                if (pagoPeloParlamentar < CriteriosDaRede.ValorMinimoDaLigacao)
                {
                    continue;
                }

                foreach (var outra in empresasDoSocio.Where(v => v.FornecedorId != doParlamentar.FornecedorId))
                {
                    if (MesmaRaiz(empresas, doParlamentar.FornecedorId, outra.FornecedorId))
                    {
                        continue;
                    }

                    foreach (var pagamento in porEmpresa[outra.FornecedorId]
                        .Where(p => p.ParlamentarId != parlamentarId
                            && p.Valor >= CriteriosDaRede.ValorMinimoDaLigacao))
                    {
                        ligacoes.Add((doParlamentar.FornecedorId, outra.FornecedorId, socio,
                            doParlamentar.Qualificacao, outra.Qualificacao,
                            pagamento.ParlamentarId, pagamento.Valor));
                    }
                }
            }
        }

        var cadeias = ligacoes
            .GroupBy(ligacao => (ligacao.F, ligacao.G))
            .Where(grupo => empresas.ContainsKey(grupo.Key.F) && empresas.ContainsKey(grupo.Key.G))
            .Select(grupo =>
            {
                var fornecedor = empresas[grupo.Key.F];
                var outra = empresas[grupo.Key.G];
                // Um sócio por pessoa: o mesmo sócio aparece uma vez por parlamentar da outra ponta.
                var sociosEmComum = grupo
                    .GroupBy(l => l.Socio)
                    .Select(porSocio => porSocio.First())
                    .ToList();

                return new CadeiaDeSocio
                {
                    Fornecedor = new EmpresaNaCadeia
                    {
                        Documento = fornecedor.Documento,
                        RazaoSocial = fornecedor.RazaoSocial,
                        Valor = porEmpresa[grupo.Key.F]
                            .Where(p => p.ParlamentarId == parlamentarId)
                            .Sum(p => p.Valor),
                    },
                    OutraEmpresa = new EmpresaNaCadeia
                    {
                        Documento = outra.Documento,
                        RazaoSocial = outra.RazaoSocial,
                        Valor = grupo.GroupBy(l => l.Q).Sum(q => q.First().VQ),
                    },
                    SociosEmComum = sociosEmComum.Count,
                    Socios = [.. sociosEmComum
                        .Select(l => new SocioEmComum
                        {
                            Nome = l.Socio.Item1,
                            Qualificacao = QualificacaoLegivel(l.QualF, l.QualG),
                        })
                        .OrderBy(s => s.Nome, StringComparer.Ordinal)],
                    OutrosParlamentares = [.. grupo
                        .GroupBy(l => l.Q)
                        .Where(q => parlamentares.ContainsKey(q.Key))
                        .Select(q => new OutroParlamentar
                        {
                            Parlamentar = parlamentares[q.Key],
                            Valor = q.First().VQ,
                        })
                        .OrderByDescending(o => o.Valor)],
                };
            })
            .Where(cadeia => cadeia.OutrosParlamentares.Count > 0)
            .OrderByDescending(cadeia => cadeia.Fornecedor.Valor + cadeia.OutraEmpresa.Valor)
            .ThenBy(cadeia => cadeia.Fornecedor.Documento, StringComparer.Ordinal)
            .ThenBy(cadeia => cadeia.OutraEmpresa.Documento, StringComparer.Ordinal)
            .ToList();

        return new QuemBeneficiaQuem
        {
            Cadeias = [.. cadeias.Take(quantas)],
            TotalDeCadeias = cadeias.Count,
        };
    }

    private static bool MesmaRaiz(IReadOnlyDictionary<Guid, EmpresaDaCadeia> empresas, Guid a, Guid b) =>
        empresas.TryGetValue(a, out var primeira)
        && empresas.TryGetValue(b, out var segunda)
        && primeira.Digitos.Length == 14
        && segunda.Digitos.Length == 14
        && primeira.Digitos.AsSpan(0, 8).SequenceEqual(segunda.Digitos.AsSpan(0, 8));

    private static string QualificacaoLegivel(string? naPrimeira, string? naOutra)
    {
        var primeira = string.IsNullOrWhiteSpace(naPrimeira) ? "qualificação não informada" : naPrimeira;
        var outra = string.IsNullOrWhiteSpace(naOutra) ? "qualificação não informada" : naOutra;

        return string.Equals(primeira, outra, StringComparison.Ordinal)
            ? $"{primeira} nas duas"
            : $"{primeira} numa, {outra} na outra";
    }
}
