using Explanando.Domain.Auditoria;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;

namespace Explanando.Domain.Regras;

/// <summary>
/// O que uma regra de rede precisa: a base inteira, e não um parlamentar.
///
/// <para>Existe separado do <see cref="ContextoDeAvaliacao"/> porque a pergunta é de outra
/// natureza. Fracionamento, pico e padrão mensal só fazem sentido contra o histórico da
/// mesma pessoa; concentração só existe <i>entre</i> pessoas. Espremer as duas no mesmo
/// contexto obrigaria toda regra por parlamentar a carregar a base toda em memória.</para>
/// </summary>
public sealed class ContextoDeRede
{
    private readonly Dictionary<Guid, Parlamentar> _parlamentaresPorId;
    private readonly Dictionary<string, Fornecedor> _fornecedoresPorDocumento;

    public ContextoDeRede(
        IEnumerable<Parlamentar> parlamentares,
        IReadOnlyList<Despesa> despesas,
        IEnumerable<Fornecedor> fornecedores,
        ParametrosDoMotor? parametros = null)
    {
        ArgumentNullException.ThrowIfNull(parlamentares);
        ArgumentNullException.ThrowIfNull(despesas);
        ArgumentNullException.ThrowIfNull(fornecedores);

        Despesas = despesas;
        Parametros = parametros ?? ParametrosDoMotor.Padrao;

        _parlamentaresPorId = parlamentares.ToDictionary(parlamentar => parlamentar.Id);

        _fornecedoresPorDocumento = fornecedores.ToDictionary(
            fornecedor => fornecedor.Documento.Numero,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<Despesa> Despesas { get; }

    public ParametrosDoMotor Parametros { get; }

    public int TotalDeParlamentares => _parlamentaresPorId.Count;

    public Parlamentar? ParlamentarDe(Despesa despesa)
    {
        ArgumentNullException.ThrowIfNull(despesa);

        return _parlamentaresPorId.GetValueOrDefault(despesa.ParlamentarId);
    }

    public Fornecedor? FornecedorDe(Despesa despesa)
    {
        ArgumentNullException.ThrowIfNull(despesa);

        return _fornecedoresPorDocumento.GetValueOrDefault(despesa.DocumentoFornecedor.Numero);
    }
}

/// <summary>
/// Uma regra que cruza parlamentares. Roda ao fim da ingestão, sobre a base inteira, e não
/// na varredura de um perfil.
/// </summary>
public interface IRegraDeRede
{
    string Codigo { get; }

    string Descricao { get; }

    IEnumerable<FlagAnomalia> Avaliar(ContextoDeRede contexto);
}

/// <summary>
/// REG_NET_004 — um mesmo fornecedor recém-aberto atende vários parlamentares, todos do
/// mesmo partido.
///
/// <para><b>Nenhuma das três condições isolada significa coisa alguma.</b> Empresa nova é
/// rotina; fornecedor que atende vários gabinetes é rotina; deputados de um partido
/// contratarem perto uns dos outros é rotina. A regra descreve a <i>coincidência</i> das
/// três, e ainda assim descreve — não conclui.</para>
///
/// <para>Exige exclusividade partidária literal, como está no texto da especificação: um
/// único parlamentar de outro partido na lista desliga a regra. É a postura permissiva do
/// <c>ADR-0007</c> aplicada ao caso em que o erro seria mais caro — aqui a flag não fala de
/// uma nota, fala de um arranjo entre pessoas.</para>
///
/// <para>Marca <b>uma</b> despesa por parlamentar: a de maior valor com aquele fornecedor.
/// Marcar todas transformaria um único achado em centenas de linhas idênticas e inflaria o
/// peso do parlamentar no índice por um fato só.</para>
/// </summary>
public sealed class RegraConcentracaoPartidaria : IRegraDeRede
{
    public string Codigo => "REG_NET_004";

    public string Descricao =>
        "Fornecedor recém-aberto concentrado em parlamentares de um mesmo partido.";

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeRede contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var parametros = contexto.Parametros;

        var porFornecedor = contexto.Despesas
            .Where(despesa => !despesa.EhEstorno)
            .GroupBy(despesa => despesa.DocumentoFornecedor.Numero, StringComparer.Ordinal);

        foreach (var grupo in porFornecedor)
        {
            if (contexto.FornecedorDe(grupo.First()) is not { } fornecedor)
            {
                continue;
            }

            if (fornecedor.DataInicioAtividade is not { } abertura)
            {
                continue;
            }

            // Relacoes que passam do piso de valor: uma nota de estacionamento nao faz
            // do fornecedor um prestador daquele gabinete.
            var relacoes = grupo
                .GroupBy(despesa => despesa.ParlamentarId)
                .Select(porParlamentar => new
                {
                    ParlamentarId = porParlamentar.Key,
                    Total = porParlamentar.Sum(despesa => despesa.ValorLiquido),
                    MaiorDespesa = porParlamentar
                        .OrderByDescending(despesa => despesa.ValorLiquido)
                        .First(),
                    PrimeiraNota = porParlamentar.Min(despesa => despesa.DataDocumento),
                })
                .Where(relacao =>
                    relacao.Total >= parametros.ValorMinimoPorParlamentarNaConcentracao)
                .ToList();

            if (relacoes.Count < parametros.MinimoDeParlamentaresParaConcentracao)
            {
                continue;
            }

            var partidos = new HashSet<string>(StringComparer.Ordinal);
            var conhecoTodos = true;

            foreach (var relacao in relacoes)
            {
                if (contexto.ParlamentarDe(relacao.MaiorDespesa) is not { } parlamentar
                    || string.IsNullOrWhiteSpace(parlamentar.SiglaPartido))
                {
                    // Um parlamentar sem partido conhecido impede afirmar exclusividade:
                    // ele pode ser justamente o de fora.
                    conhecoTodos = false;
                    break;
                }

                partidos.Add(parlamentar.SiglaPartido);
            }

            if (!conhecoTodos || partidos.Count != 1)
            {
                continue;
            }

            // "Recem-criada" medido contra a primeira nota emitida a qualquer um deles: e'
            // o instante em que a relacao comecou, e nao a data de hoje, que muda sozinha.
            var primeiraNota = relacoes.Min(relacao => relacao.PrimeiraNota);
            var idadeNaPrimeiraNota =
                DateOnly.FromDateTime(primeiraNota.UtcDateTime).DayNumber - abertura.DayNumber;

            if (idadeNaPrimeiraNota < 0
                || idadeNaPrimeiraNota > parametros.DiasParaFornecedorRecemCriadoNaRede)
            {
                continue;
            }

            var partido = partidos.Single();

            foreach (var relacao in relacoes)
            {
                yield return FlagAnomalia.Criar(
                    relacao.MaiorDespesa.Id,
                    Codigo,
                    Gravidade.Media,
                    $"Padrão de contratação: '{fornecedor.RazaoSocial}', aberta em "
                    + $"{abertura:dd/MM/yyyy}, presta serviço a {relacoes.Count} "
                    + $"parlamentares nesta base, todos do partido {partido}. "
                    + "A coincidência é descritiva e não caracteriza, por si, qualquer "
                    + "situação irregular.",
                    grauDeConfianca: 0.7m);
            }
        }
    }
}
