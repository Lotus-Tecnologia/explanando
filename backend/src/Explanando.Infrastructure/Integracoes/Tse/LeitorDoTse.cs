using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Explanando.Application.Eleicoes;
using Explanando.Domain.Eleicoes;

namespace Explanando.Infrastructure.Integracoes.Tse;

/// <summary>
/// Lê os arquivos anuais do TSE de uma pasta local.
///
/// <para><b>Local, e não HTTP.</b> O CDN do TSE (Akamai) recusa curl, PowerShell, Python e
/// <c>HttpClient</c> pela impressão digital do cliente; o <c>fetch</c> do Node passa, e é por ele
/// que <c>ferramentas/baixar-tse.mjs</c> baixa os três arquivos. São publicados uma vez por
/// eleição, então baixar à mão não é gargalo. Ver <c>docs/contratos/tse.md</c>.</para>
///
/// <para>Os nomes esperados na pasta, para o ano <c>A</c>: <c>candidatos_A.zip</c>,
/// <c>orgaos_partidarios_A.zip</c> e <c>votacao_munzona_A.zip</c> — os que o script grava.</para>
/// </summary>
public sealed class LeitorDoTse(string pasta) : IFonteDoTse
{
    /// <summary>Os arquivos do TSE vêm em Latin-1. Built-in, sem depender de ICU.</summary>
    private static readonly Encoding Codificacao = Encoding.Latin1;

    private const char Separador = ';';

    public async IAsyncEnumerable<CandidatoNaVotacao> LerVotacaoAsync(
        int ano,
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        using var zip = Abrir($"votacao_munzona_{ano}.zip");

        // Um arquivo por UF. O de BRASIL repete todos, e o de BR é só presidente: os dois ficam
        // de fora, senão cada voto seria contado duas vezes.
        foreach (var entrada in zip.Entries.Where(e =>
            e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            && !e.Name.EndsWith("_BRASIL.csv", StringComparison.OrdinalIgnoreCase)
            && !e.Name.EndsWith("_BR.csv", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            // Somado por arquivo: o mesmo candidato aparece uma linha por zona, e um município
            // grande tem dezenas de zonas. A memória fica limitada ao tamanho de uma UF.
            var porCandidato = new Dictionary<long, (CandidatoNaVotacao Dados, Dictionary<int, (string Nome, long Votos)> Municipios)>();

            await using var fluxo = entrada.Open();
            using var leitor = new StreamReader(fluxo, Codificacao);

            var colunas = Indice(await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false));

            while (await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false) is { } linha)
            {
                var campos = CsvComAspas.Separar(linha, Separador);

                var cargo = Inteiro(campos[colunas["CD_CARGO"]]);

                if (cargo is not (5 or 6) || campos[colunas["NR_TURNO"]] != "1")
                {
                    continue;
                }

                var sequencial = long.Parse(campos[colunas["SQ_CANDIDATO"]], CultureInfo.InvariantCulture);

                if (!porCandidato.TryGetValue(sequencial, out var registro))
                {
                    registro = (new CandidatoNaVotacao
                    {
                        Sequencial = sequencial,
                        NomeCivil = campos[colunas["NM_CANDIDATO"]],
                        NomeDeUrna = campos[colunas["NM_URNA_CANDIDATO"]],
                        Uf = campos[colunas["SG_UF"]],
                        CodigoDoCargo = cargo,
                        Cargo = campos[colunas["DS_CARGO"]],
                        SiglaPartido = campos[colunas["SG_PARTIDO"]],
                        NumeroNaUrna = Inteiro(campos[colunas["NR_CANDIDATO"]]),
                        Situacao = campos[colunas["DS_SIT_TOT_TURNO"]],
                    }, new Dictionary<int, (string, long)>());

                    porCandidato[sequencial] = registro;
                }

                var municipio = Inteiro(campos[colunas["CD_MUNICIPIO"]]);
                var votos = long.Parse(campos[colunas["QT_VOTOS_NOMINAIS"]], CultureInfo.InvariantCulture);

                registro.Municipios[municipio] = registro.Municipios.TryGetValue(municipio, out var atual)
                    ? (atual.Nome, atual.Votos + votos)
                    : (campos[colunas["NM_MUNICIPIO"]], votos);
            }

            foreach (var (dados, municipios) in porCandidato.Values)
            {
                yield return dados with
                {
                    PorMunicipio = [.. municipios.Select(m => new VotosDaFonte(m.Key, m.Value.Nome, m.Value.Votos))],
                };
            }
        }
    }

    public async IAsyncEnumerable<ReceitaDaFonte> LerReceitasAsync(
        int ano,
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        await foreach (var receita in LerArquivoDeReceitasAsync(
            $"candidatos_{ano}.zip", $"receitas_candidatos_{ano}_BRASIL.csv", DestinoDaDoacao.Candidato, cancelamento)
            .ConfigureAwait(false))
        {
            yield return receita;
        }

        await foreach (var receita in LerArquivoDeReceitasAsync(
            $"orgaos_partidarios_{ano}.zip", $"receitas_orgaos_partidarios_{ano}_BRASIL.csv", DestinoDaDoacao.Partido, cancelamento)
            .ConfigureAwait(false))
        {
            yield return receita;
        }
    }

    /// <summary>
    /// Só o arquivo BRASIL: os por UF repetem as mesmas receitas, e ler os dois duplicaria cada
    /// doação. Só pessoa física (CPF de 11 dígitos), e sem "recursos próprios" — o candidato
    /// financiando a própria campanha não é doador no sentido que o cruzamento procura.
    /// </summary>
    private async IAsyncEnumerable<ReceitaDaFonte> LerArquivoDeReceitasAsync(
        string arquivo,
        string entradaDoZip,
        DestinoDaDoacao destino,
        [EnumeratorCancellation] CancellationToken cancelamento)
    {
        using var zip = Abrir(arquivo);

        var entrada = zip.GetEntry(entradaDoZip)
            ?? throw new InvalidOperationException(
                $"{arquivo} não tem {entradaDoZip}. O TSE mudou o nome do arquivo, ou o download veio incompleto.");

        await using var fluxo = entrada.Open();
        using var leitor = new StreamReader(fluxo, Codificacao);

        var colunas = Indice(await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false));

        while (await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false) is { } linha)
        {
            var campos = CsvComAspas.Separar(linha, Separador);

            var documento = campos[colunas["NR_CPF_CNPJ_DOADOR"]];

            if (documento.Length != 11 || !documento.All(char.IsAsciiDigit))
            {
                continue;
            }

            var origem = campos[colunas["DS_ORIGEM_RECEITA"]];

            if (origem.Contains("próprios", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nomeNaReceita = campos[colunas["NM_DOADOR_RFB"]];
            var nome = string.IsNullOrWhiteSpace(nomeNaReceita) || nomeNaReceita.StartsWith('#')
                ? campos[colunas["NM_DOADOR"]]
                : nomeNaReceita;

            if (string.IsNullOrWhiteSpace(nome) || nome.StartsWith('#'))
            {
                continue;
            }

            yield return destino == DestinoDaDoacao.Candidato
                ? new ReceitaDaFonte
                {
                    Sequencial = long.Parse(campos[colunas["SQ_RECEITA"]], CultureInfo.InvariantCulture),
                    Destino = destino,
                    SequencialDoCandidato = long.Parse(campos[colunas["SQ_CANDIDATO"]], CultureInfo.InvariantCulture),
                    NomeDoDestinatario = campos[colunas["NM_CANDIDATO"]],
                    SiglaPartidoDoDestinatario = campos[colunas["SG_PARTIDO"]],
                    UfDoDestinatario = campos[colunas["SG_UF"]],
                    CargoDoDestinatario = campos[colunas["DS_CARGO"]],
                    DocumentoDoDoador = documento,
                    NomeDoDoador = nome,
                    Data = Data(campos[colunas["DT_RECEITA"]]),
                    Valor = Valor(campos[colunas["VR_RECEITA"]]),
                }
                : new ReceitaDaFonte
                {
                    Sequencial = long.Parse(campos[colunas["SQ_RECEITA"]], CultureInfo.InvariantCulture),
                    Destino = destino,
                    NomeDoDestinatario = OrgaoPartidario(campos, colunas),
                    SiglaPartidoDoDestinatario = campos[colunas["SG_PARTIDO"]],
                    UfDoDestinatario = campos[colunas["SG_UF"]],
                    DocumentoDoDoador = documento,
                    NomeDoDoador = nome,
                    Data = Data(campos[colunas["DT_RECEITA"]]),
                    Valor = Valor(campos[colunas["VR_RECEITA"]]),
                };
        }
    }

    /// <summary>"PT — Direção Estadual/Distrital", com o município quando a esfera é municipal.</summary>
    private static string OrgaoPartidario(List<string> campos, Dictionary<string, int> colunas)
    {
        var esfera = campos[colunas["DS_ESFERA_PARTIDARIA"]];
        var municipio = campos[colunas["NM_MUNICIPIO"]];
        var partido = campos[colunas["SG_PARTIDO"]];

        return string.IsNullOrWhiteSpace(municipio) || municipio.StartsWith('#')
            ? $"{partido} — {esfera}"
            : $"{partido} — {esfera} ({municipio})";
    }

    private ZipArchive Abrir(string arquivo)
    {
        var caminho = Path.Combine(pasta, arquivo);

        if (!File.Exists(caminho))
        {
            throw new FileNotFoundException(
                $"Não achei {caminho}. Os arquivos do TSE se baixam com ferramentas/baixar-tse.mjs.",
                caminho);
        }

        return ZipFile.OpenRead(caminho);
    }

    private static Dictionary<string, int> Indice(string? cabecalho)
    {
        if (string.IsNullOrWhiteSpace(cabecalho))
        {
            throw new InvalidOperationException("Arquivo do TSE sem cabeçalho.");
        }

        return CsvComAspas.Separar(cabecalho, Separador)
            .Select((nome, posicao) => (nome, posicao))
            .ToDictionary(par => par.nome.Trim(), par => par.posicao, StringComparer.Ordinal);
    }

    private static int Inteiro(string valor) =>
        int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) ? numero : 0;

    private static DateOnly Data(string valor) =>
        DateOnly.ParseExact(valor, "dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"20000,00" — vírgula decimal, sem separador de milhar.</summary>
    private static decimal Valor(string valor) =>
        decimal.Parse(valor.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);
}
