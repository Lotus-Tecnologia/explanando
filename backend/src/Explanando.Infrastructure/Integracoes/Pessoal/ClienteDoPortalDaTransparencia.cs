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
            // Dois motivos diferentes, e confundi-los manda alguem procurar no lugar
            // errado. Ambos foram medidos:
            //
            //   403  a competencia nao existe ainda. O arquivo de um mes so' aparece
            //        semanas depois do fechamento — o de julho/2026 saiu em 3 de setembro.
            //   405  a origem esta' recusando. Aconteceu numa carga de 19 meses: os dez
            //        primeiros baixaram normalmente e os nove seguintes voltaram 405,
            //        INCLUSIVE um mes que tinha baixado minutos antes. Nao e' ausencia de
            //        arquivo; e' protecao contra volume.
            var motivo = (int)resposta.StatusCode switch
            {
                403 => "essa competência ainda não foi publicada — o arquivo de um mês só "
                    + "sai semanas depois do fechamento",
                405 or 429 => "a origem está recusando por volume. Espere e tente de novo, "
                    + "com menos meses de cada vez",
                _ => "resposta inesperada da origem",
            };

            throw new InvalidOperationException(
                $"O Portal da Transparência respondeu {(int)resposta.StatusCode} para {url}: "
                + $"{motivo}.");
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
        var nomes = CsvComAspas.Separar(cabecalho, Separador)
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
        var campos = CsvComAspas.Separar(linha, Separador);

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
}
