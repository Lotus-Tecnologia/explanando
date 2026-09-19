using System.Globalization;
using System.Text.Json;
using Explanando.Domain.Estado;

namespace Explanando.Infrastructure.Integracoes.Siorg;

/// <summary>O que o SIORG respondeu, já interpretado: as unidades ou o motivo da recusa.</summary>
public sealed record RespostaDoSiorg
{
    public IReadOnlyList<NovaUnidadeDoEstado> Unidades { get; init; } = [];

    /// <summary>Nulo quando <c>codigoErro</c> veio zero.</summary>
    public string? Falha { get; init; }

    /// <summary>
    /// A recusa é temporária e insistir resolve.
    ///
    /// <para>É o caso do limite de <b>uma requisição simultânea por IP</b>, que foi medido
    /// do pior jeito possível: seis threads derrubaram 329 das 335 chamadas, e todas
    /// voltaram <c>HTTP 200</c>.</para>
    /// </summary>
    public bool VaiPassar { get; init; }
}

/// <summary>
/// Traduz o JSON do SIORG para o vocabulário do domínio.
///
/// <para><b>Os códigos chegam como URI, e em duas grafias diferentes para o mesmo campo.</b>
/// Medido: <c>/unidade-organizacional/26</c> devolve
/// <c>.../id/poder/executivo</c>, enquanto <c>/estrutura-organizacional</c> devolve
/// <c>.../id/poder/1</c> para exatamente a mesma unidade. Um conversor que soubesse só uma
/// das grafias funcionaria em metade das chamadas e devolveria nulo silencioso na outra
/// metade — o tipo de defeito que só aparece depois de publicado.</para>
/// </summary>
public static class ConversorDoSiorg
{
    /// <summary>
    /// Códigos de erro que <b>não</b> adianta repetir.
    ///
    /// <para><c>100</c>: campo obrigatório ausente. <c>104</c>: "unidade organizacional deve
    /// ser um órgão ou entidade" — é o que o serviço responde ao pedir a árvore de um
    /// <c>ente</c>, e é esperado, não defeito.</para>
    ///
    /// <para>Qualquer outro código é tratado como temporário. A escolha é deliberada: errar
    /// para o lado de insistir custa uma chamada a mais; errar para o lado de desistir
    /// deixa uma subárvore inteira de fora e faz a coleta parecer completa quando não é.</para>
    /// </summary>
    private static readonly int[] ErrosPermanentes = [100, 104];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RespostaDoSiorg ConverterLista(string corpo)
    {
        ListaDeUnidadesJson? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<ListaDeUnidadesJson>(corpo, Json);
        }
        catch (JsonException erro)
        {
            return new RespostaDoSiorg { Falha = $"resposta ilegivel: {erro.Message}" };
        }

        if (Recusa(envelope?.Servico) is { } recusa)
        {
            return recusa;
        }

        return new RespostaDoSiorg
        {
            Unidades = [.. (envelope?.Unidades ?? []).Select(Converter).OfType<NovaUnidadeDoEstado>()],
        };
    }

    public static RespostaDoSiorg ConverterUnidade(string corpo)
    {
        UnidadeUnicaJson? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<UnidadeUnicaJson>(corpo, Json);
        }
        catch (JsonException erro)
        {
            return new RespostaDoSiorg { Falha = $"resposta ilegivel: {erro.Message}" };
        }

        if (Recusa(envelope?.Servico) is { } recusa)
        {
            return recusa;
        }

        var unidade = envelope?.Unidade is null ? null : Converter(envelope.Unidade);

        return new RespostaDoSiorg { Unidades = unidade is null ? [] : [unidade] };
    }

    private static RespostaDoSiorg? Recusa(ServicoJson? servico)
    {
        if (servico is null)
        {
            // Sem envelope nao da' para distinguir sucesso vazio de recusa. Tratar como
            // falha temporaria e' o unico desfecho que nao inventa informacao.
            return new RespostaDoSiorg { Falha = "resposta sem envelope de servico", VaiPassar = true };
        }

        if (servico.CodigoErro == 0)
        {
            return null;
        }

        return new RespostaDoSiorg
        {
            Falha = $"codigoErro {servico.CodigoErro}: {servico.Mensagem}",
            VaiPassar = !ErrosPermanentes.Contains(servico.CodigoErro),
        };
    }

    /// <summary>Nulo quando nem o código da unidade dá para ler — sem ele não há nó.</summary>
    private static NovaUnidadeDoEstado? Converter(UnidadeJson bruta)
    {
        if (Numero(bruta.CodigoUnidade) is not { } codigo)
        {
            return null;
        }

        return new NovaUnidadeDoEstado
        {
            Codigo = codigo,
            CodigoPai = Numero(bruta.CodigoUnidadePai),
            CodigoOrgao = Numero(bruta.CodigoOrgaoEntidade),
            Nome = Texto(bruta.Nome) ?? $"Unidade {codigo}",
            Sigla = Texto(bruta.Sigla),
            Poder = ConverterPoder(Cauda(bruta.CodigoPoder)),
            Esfera = ConverterEsfera(Cauda(bruta.CodigoEsfera)),
            Tipo = ConverterTipo(Cauda(bruta.CodigoTipoUnidade)),
            NaturezaJuridica = ConverterNatureza(Cauda(bruta.CodigoNaturezaJuridica)),
            Normatizacao = ConverterNormatizacao(bruta.NivelNormatizacao),
        };
    }

    /// <summary>O último segmento da URI. Vazio e nulo são a mesma ausência.</summary>
    private static string? Cauda(string? uri)
    {
        var texto = Texto(uri);

        if (texto is null)
        {
            return null;
        }

        var barra = texto.LastIndexOf('/');

        return barra < 0 ? texto : Texto(texto[(barra + 1)..]);
    }

    private static int? Numero(string? uri) =>
        Cauda(uri) is { } cauda
        && int.TryParse(cauda, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    private static string? Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // ------------------------------------------------------------------
    //  As duas grafias de cada tabela de referencia
    // ------------------------------------------------------------------

    private static Poder? ConverterPoder(string? cauda) => cauda switch
    {
        "1" or "executivo" => Poder.Executivo,
        "2" or "legislativo" => Poder.Legislativo,
        "3" or "judiciario" => Poder.Judiciario,
        "4" or "funcoes-essenciais-a-justica" => Poder.FuncoesEssenciaisAJustica,
        _ => null,
    };

    private static EsferaDeGoverno? ConverterEsfera(string? cauda) => cauda switch
    {
        "1" or "federal" => EsferaDeGoverno.Federal,
        "2" or "estadual-distrital" => EsferaDeGoverno.EstadualOuDistrital,
        "3" or "municipal" => EsferaDeGoverno.Municipal,
        _ => null,
    };

    private static NaturezaJuridica? ConverterNatureza(string? cauda) => cauda switch
    {
        "1" or "empresa-publica" => NaturezaJuridica.EmpresaPublica,
        "2" or "fundacao-publica" => NaturezaJuridica.FundacaoPublica,
        "3" or "administracao-direta" => NaturezaJuridica.AdministracaoDireta,
        "4" or "autarquia" => NaturezaJuridica.Autarquia,
        "6" or "sociedade-de-economia-mista" => NaturezaJuridica.SociedadeDeEconomiaMista,
        "7" or "orgao-publico-autonomo-federal" => NaturezaJuridica.OrgaoPublicoAutonomoFederal,
        _ => null,
    };

    /// <summary>
    /// Três grafias aqui, não duas: as URIs trazem o nome por extenso, e a tabela de
    /// referência (<c>/tipo-unidade</c>) devolve a sigla de duas letras.
    /// </summary>
    private static TipoDeUnidade ConverterTipo(string? cauda) => cauda switch
    {
        "ente" or "ET" or "1" => TipoDeUnidade.Ente,
        "entidade" or "EN" or "2" => TipoDeUnidade.Entidade,
        "orgao" or "OR" or "3" => TipoDeUnidade.Orgao,
        "unidade-administrativa" or "UA" or "4" => TipoDeUnidade.UnidadeAdministrativa,
        "unidade-colegiada" or "UC" or "5" => TipoDeUnidade.UnidadeColegiada,

        // Tipo desconhecido vira unidade administrativa, que e' o caso geral e o mais
        // numeroso (1.967 de 2.334 na medicao). Recusar a unidade por causa de um rotulo
        // novo apagaria do mapa um orgao que existe.
        _ => TipoDeUnidade.UnidadeAdministrativa,
    };

    private static NivelDeNormatizacao? ConverterNormatizacao(string? valor) => Texto(valor) switch
    {
        "LEI_DECRETO" => NivelDeNormatizacao.LeiOuDecreto,
        "ATO_INTERNO" => NivelDeNormatizacao.AtoInterno,
        _ => null,
    };
}
