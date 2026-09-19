using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Explanando.Application.Estado;
using Explanando.Domain.Comum;

namespace Explanando.Infrastructure.Integracoes.Pessoal;

/// <summary>
/// Lê o cadastro de pessoal do Executivo federal publicado pelo Portal da Transparência.
///
/// <para><b>É dado aberto e não exige cadastro nenhum</b> — o que contradiz o que eu
/// afirmei durante a Etapa 11. O arquivo mensal traz 778.337 vínculos em 444 MB de CSV,
/// dentro de um ZIP de 83 MB.</para>
///
/// <para><b>Três decisões de leitura, e as três são sobre não carregar o que não se usa:</b></para>
///
/// <list type="number">
/// <item>O ZIP vai para um arquivo temporário, porque <c>ZipArchive</c> precisa de fluxo
/// navegável — e some no <c>Dispose</c>, ainda que a coleta falhe no meio.</item>
/// <item>O CSV é lido linha a linha e <b>nunca</b> materializado: 444 MB numa lista de
/// strings é mais memória do que o plano de hospedagem tem.</item>
/// <item>De cada linha saem <b>três campos</b>. O nome e o CPF mascarado existem no arquivo
/// e param aqui: <see cref="VinculoDaFonte"/> não tem onde guardá-los.</item>
/// </list>
/// </summary>
public sealed class ClienteDoPortalDaTransparencia(HttpClient http) : IFonteDePessoal
{
    private const string Base =
        "https://dadosabertos-download.cgu.gov.br/PortalDaTransparencia/saida/servidores/";

    /// <summary>
    /// A origem dentro do arquivo. <c>SIAPE</c> é o cadastro civil do Executivo.
    ///
    /// <para>Há também <c>BACEN</c> (518 KB) e outros; ficam de fora porque o SIAPE já
    /// cobre o Executivo civil inteiro, e somar cadastros com regras próprias no mesmo
    /// número é o erro que o ADR-0012 evita entre Câmara e Senado.</para>
    /// </summary>
    private const string Origem = "Servidores_SIAPE";

    /// <summary>
    /// O CSV é <b>Latin-1</b>, não UTF-8.
    ///
    /// <para>Lido como UTF-8, "Ministério" vira caractere de substituição e a ponte por
    /// nome deixa de casar — em silêncio, porque nada lança exceção.
    /// <c>Encoding.Latin1</c> é built-in e não depende de ICU, o que importa aqui: o
    /// projeto roda com <c>InvariantGlobalization</c>.</para>
    /// </summary>
    private static readonly Encoding Codificacao = Encoding.Latin1;

    private const char Separador = ';';

    public async IAsyncEnumerable<VinculoDaFonte> LerVinculosAsync(
        Competencia competencia,
        [EnumeratorCancellation] CancellationToken cancelamento = default)
    {
        var rotulo = string.Create(
            CultureInfo.InvariantCulture,
            $"{competencia.Ano:D4}{competencia.Mes:D2}");

        var temporario = Path.Combine(Path.GetTempPath(), $"explanando-{rotulo}-{Guid.NewGuid():N}.zip");

        try
        {
            await BaixarAsync($"{Base}{rotulo}_{Origem}.zip", temporario, cancelamento)
                .ConfigureAwait(false);

            using var pacote = ZipFile.OpenRead(temporario);

            var cadastro = pacote.Entries.FirstOrDefault(entrada =>
                entrada.Name.EndsWith("_Cadastro.csv", StringComparison.OrdinalIgnoreCase));

            if (cadastro is null)
            {
                throw new InvalidOperationException(
                    $"O pacote de {rotulo} não traz o arquivo de cadastro. "
                    + $"Entradas: {string.Join(", ", pacote.Entries.Select(e => e.Name))}.");
            }

            await using var fluxo = cadastro.Open();
            using var leitor = new StreamReader(fluxo, Codificacao);

            var cabecalho = await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false);

            if (cabecalho is null)
            {
                yield break;
            }

            var colunas = Posicoes(cabecalho);

            while (await leitor.ReadLineAsync(cancelamento).ConfigureAwait(false) is { } linha)
            {
                if (Converter(linha, colunas) is { } vinculo)
                {
                    yield return vinculo;
                }
            }
        }
        finally
        {
            // O temporario some mesmo se a coleta morrer no meio: sao 83 MB por tentativa,
            // e um Cloud Run Job que reinicia algumas vezes encheria o disco efemero.
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }

    private async Task BaixarAsync(string url, string destino, CancellationToken cancelamento)
    {
        using var resposta = await http
            .GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, cancelamento)
            .ConfigureAwait(false);

        if (!resposta.IsSuccessStatusCode)
        {
            // 403 e' o que a origem responde para competencia que ainda nao existe — o
            // arquivo de um mes so' aparece semanas depois do fim dele.
            throw new InvalidOperationException(
                $"O Portal da Transparência respondeu {(int)resposta.StatusCode} para {url}. "
                + "Competência inexistente responde 403; o arquivo de um mês só é publicado "
                + "semanas depois do fechamento.");
        }

        await using var origem = await resposta.Content.ReadAsStreamAsync(cancelamento)
            .ConfigureAwait(false);

        await using var arquivo = File.Create(destino);

        await origem.CopyToAsync(arquivo, cancelamento).ConfigureAwait(false);
    }

    /// <summary>
    /// Onde estão as três colunas que interessam.
    ///
    /// <para>Por nome, e não por posição fixa: o arquivo tem 43 colunas, e uma coluna nova
    /// inserida no meio deslocaria tudo. Procurar pelo nome transforma essa mudança em erro
    /// claro no lugar de dado trocado.</para>
    /// </summary>
    private static (int Codigo, int Nome, int Funcao) Posicoes(string cabecalho)
    {
        var nomes = Separar(cabecalho)
            .Select(c => c.Trim().ToUpperInvariant())
            .ToList();

        var codigo = nomes.IndexOf("COD_ORG_EXERCICIO");
        var nome = nomes.IndexOf("ORG_EXERCICIO");
        var funcao = nomes.IndexOf("SIGLA_FUNCAO");

        if (codigo < 0 || nome < 0)
        {
            throw new InvalidOperationException(
                "O cadastro mudou de formato: não achei COD_ORG_EXERCICIO e ORG_EXERCICIO. "
                + $"Colunas: {string.Join(", ", nomes)}.");
        }

        return (codigo, nome, funcao);
    }

    private static VinculoDaFonte? Converter(string linha, (int Codigo, int Nome, int Funcao) onde)
    {
        var campos = Separar(linha);

        if (campos.Count <= onde.Nome)
        {
            // Linha truncada. Descartar uma e seguir e' melhor que derrubar a coleta de
            // 778 mil — e o total lido no resumo denuncia se isso virar rotina.
            return null;
        }

        if (!int.TryParse(
                campos[onde.Codigo].Trim(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var codigo))
        {
            return null;
        }

        var funcao = onde.Funcao >= 0 && onde.Funcao < campos.Count
            ? campos[onde.Funcao].Trim()
            : null;

        return new VinculoDaFonte
        {
            CodigoSiafi = codigo,
            NomeDoOrgao = campos[onde.Nome].Trim(),

            // "-1" e' como a fonte escreve "sem funcao". O dominio descarta; aqui so' se
            // evita criar uma string para 638 mil linhas que nao tem funcao nenhuma.
            SiglaDaFuncao = string.IsNullOrWhiteSpace(funcao) || funcao == "-1" ? null : funcao,
        };
    }

    /// <summary>
    /// Separa uma linha respeitando as aspas.
    ///
    /// <para>Um <c>Split(';')</c> bastaria hoje, e é justamente por isso que ele não serve:
    /// o dia em que um nome de órgão trouxer um ponto e vírgula, ele deslocaria todas as
    /// colunas daquela linha sem erro nenhum.</para>
    /// </summary>
    private static List<string> Separar(string linha)
    {
        var campos = new List<string>(48);
        var atual = new StringBuilder(64);
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];

            if (c == '"')
            {
                // Aspas duplicadas dentro de campo entre aspas representam uma aspa.
                if (dentroDeAspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                    continue;
                }

                dentroDeAspas = !dentroDeAspas;
                continue;
            }

            if (c == Separador && !dentroDeAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
                continue;
            }

            atual.Append(c);
        }

        campos.Add(atual.ToString());

        return campos;
    }
}
