using System.IO.Compression;
using System.Text;
using Explanando.Domain.Eleicoes;
using Explanando.Infrastructure.Integracoes.Tse;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// O leitor dos arquivos do TSE, contra zips montados aqui com o <b>cabeçalho real</b> de 2022.
///
/// <para>Os arquivos verdadeiros têm quase 1 GB e CPF de gente real, então não viram fixture:
/// os zips são gerados no teste, com CPFs inventados, e cobrem as armadilhas medidas no arquivo
/// real — Latin-1, o arquivo BRASIL repetindo os das UFs, "recursos próprios", CNPJ de partido
/// no lugar do doador e o mesmo município em várias zonas.</para>
/// </summary>
public sealed class LeitorDoTseTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"tse-{Guid.NewGuid():N}");

    private const string CabecalhoDaVotacao =
        "\"DT_GERACAO\";\"HH_GERACAO\";\"ANO_ELEICAO\";\"NR_TURNO\";\"SG_UF\";\"CD_MUNICIPIO\";\"NM_MUNICIPIO\";"
        + "\"NR_ZONA\";\"CD_CARGO\";\"DS_CARGO\";\"SQ_CANDIDATO\";\"NR_CANDIDATO\";\"NM_CANDIDATO\";"
        + "\"NM_URNA_CANDIDATO\";\"SG_PARTIDO\";\"QT_VOTOS_NOMINAIS\";\"DS_SIT_TOT_TURNO\"";

    private const string CabecalhoDeReceitas =
        "\"SQ_RECEITA\";\"SQ_CANDIDATO\";\"NM_CANDIDATO\";\"SG_PARTIDO\";\"SG_UF\";\"DS_CARGO\";"
        + "\"DS_ORIGEM_RECEITA\";\"NR_CPF_CNPJ_DOADOR\";\"NM_DOADOR\";\"NM_DOADOR_RFB\";\"DT_RECEITA\";\"VR_RECEITA\"";

    private const string CabecalhoDePartidos =
        "\"SQ_RECEITA\";\"SG_PARTIDO\";\"SG_UF\";\"DS_ESFERA_PARTIDARIA\";\"NM_MUNICIPIO\";"
        + "\"DS_ORIGEM_RECEITA\";\"NR_CPF_CNPJ_DOADOR\";\"NM_DOADOR\";\"NM_DOADOR_RFB\";\"DT_RECEITA\";\"VR_RECEITA\"";

    public LeitorDoTseTests()
    {
        Directory.CreateDirectory(_pasta);

        Zip("votacao_munzona_2022.zip", new()
        {
            ["votacao_candidato_munzona_2022_AP.csv"] =
            [
                CabecalhoDaVotacao,
                Linha("x", "x", "2022", "1", "AP", "6050", "MACAPÁ", "2", "6", "Deputado Federal", "30001", "1515", "ACÁCIO DA SILVA", "ACÁCIO", "MDB", "100", "ELEITO POR QP"),
                Linha("x", "x", "2022", "1", "AP", "6050", "MACAPÁ", "10", "6", "Deputado Federal", "30001", "1515", "ACÁCIO DA SILVA", "ACÁCIO", "MDB", "50", "ELEITO POR QP"),
                Linha("x", "x", "2022", "1", "AP", "6076", "SANTANA", "4", "6", "Deputado Federal", "30001", "1515", "ACÁCIO DA SILVA", "ACÁCIO", "MDB", "30", "ELEITO POR QP"),
                // Deputado estadual e segundo turno: fora.
                Linha("x", "x", "2022", "1", "AP", "6050", "MACAPÁ", "2", "7", "Deputado Estadual", "30002", "15000", "OUTRO", "OUTRO", "MDB", "999", "SUPLENTE"),
                Linha("x", "x", "2022", "2", "AP", "6050", "MACAPÁ", "2", "6", "Deputado Federal", "30001", "1515", "ACÁCIO DA SILVA", "ACÁCIO", "MDB", "999", "ELEITO POR QP"),
            ],
            // O BRASIL repete as UFs: se fosse lido, Macapá teria votos em dobro.
            ["votacao_candidato_munzona_2022_BRASIL.csv"] =
            [
                CabecalhoDaVotacao,
                Linha("x", "x", "2022", "1", "AP", "6050", "MACAPÁ", "2", "6", "Deputado Federal", "30001", "1515", "ACÁCIO DA SILVA", "ACÁCIO", "MDB", "100", "ELEITO POR QP"),
            ],
        });

        Zip("candidatos_2022.zip", new()
        {
            ["receitas_candidatos_2022_BRASIL.csv"] =
            [
                CabecalhoDeReceitas,
                Linha("1", "30001", "ACÁCIO DA SILVA", "MDB", "AP", "Deputado Federal", "Recursos de pessoas físicas", "11122233344", "MARIA DOADORA", "MARIA DOADORA DA SILVA", "15/09/2022", "20000,00"),
                Linha("2", "30001", "ACÁCIO DA SILVA", "MDB", "AP", "Deputado Federal", "Recursos próprios", "99988877766", "ACÁCIO DA SILVA", "ACÁCIO DA SILVA", "01/09/2022", "5000,00"),
                Linha("3", "30001", "ACÁCIO DA SILVA", "MDB", "AP", "Deputado Federal", "Recursos de partido político", "12345678000190", "DIRETORIO", "#NULO", "01/09/2022", "100000,00"),
                Linha("4", "30001", "ACÁCIO DA SILVA", "MDB", "AP", "Deputado Federal", "Recursos de pessoas físicas", "55566677788", "JOÃO SEM RFB", "#NULO", "20/09/2022", "1500,50"),
            ],
            // O arquivo da UF repete o BRASIL: se fosse lido, cada doação sairia em dobro.
            ["receitas_candidatos_2022_AP.csv"] =
            [
                CabecalhoDeReceitas,
                Linha("1", "30001", "ACÁCIO DA SILVA", "MDB", "AP", "Deputado Federal", "Recursos de pessoas físicas", "11122233344", "MARIA DOADORA", "MARIA DOADORA DA SILVA", "15/09/2022", "20000,00"),
            ],
        });

        Zip("orgaos_partidarios_2022.zip", new()
        {
            ["receitas_orgaos_partidarios_2022_BRASIL.csv"] =
            [
                CabecalhoDePartidos,
                Linha("9", "PT", "SP", "Direção Estadual/Distrital", "#NULO", "Recursos de pessoas físicas", "12345678901", "JOSÉ DA SILVA", "JOSÉ DA SILVA", "10/08/2022", "3000,00"),
            ],
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
        {
            Directory.Delete(_pasta, recursive: true);
        }
    }

    [Fact]
    public async Task A_votacao_soma_as_zonas_do_municipio_e_le_so_as_ufs()
    {
        var candidatos = new List<Application.Eleicoes.CandidatoNaVotacao>();

        await foreach (var candidato in new LeitorDoTse(_pasta).LerVotacaoAsync(2022))
        {
            candidatos.Add(candidato);
        }

        var acacio = candidatos.ShouldHaveSingleItem();
        acacio.NomeCivil.ShouldBe("ACÁCIO DA SILVA", "Latin-1 lido como Latin-1");
        acacio.PorMunicipio.Single(m => m.NomeDoMunicipio == "MACAPÁ").Votos.ShouldBe(150);
        acacio.PorMunicipio.Single(m => m.NomeDoMunicipio == "SANTANA").Votos.ShouldBe(30);
    }

    [Fact]
    public async Task As_receitas_sao_so_de_pessoa_fisica_sem_recursos_proprios_e_sem_duplicar()
    {
        var receitas = new List<Application.Eleicoes.ReceitaDaFonte>();

        await foreach (var receita in new LeitorDoTse(_pasta).LerReceitasAsync(2022))
        {
            receitas.Add(receita);
        }

        receitas.Select(r => r.Sequencial).ShouldBe([1L, 4L, 9L]);

        var maria = receitas.Single(r => r.Sequencial == 1);
        maria.NomeDoDoador.ShouldBe("MARIA DOADORA DA SILVA", "o nome da Receita, quando o TSE traz");
        maria.Valor.ShouldBe(20_000m);
        maria.Data.ShouldBe(new DateOnly(2022, 9, 15));
        maria.Destino.ShouldBe(DestinoDaDoacao.Candidato);
        maria.SequencialDoCandidato.ShouldBe(30001);

        receitas.Single(r => r.Sequencial == 4).NomeDoDoador.ShouldBe("JOÃO SEM RFB", "sem o nome da Receita, fica o declarado");
        receitas.Single(r => r.Sequencial == 4).Valor.ShouldBe(1_500.50m);

        var partido = receitas.Single(r => r.Sequencial == 9);
        partido.Destino.ShouldBe(DestinoDaDoacao.Partido);
        partido.NomeDoDestinatario.ShouldBe("PT — Direção Estadual/Distrital");
    }

    [Fact]
    public async Task Pasta_sem_os_arquivos_diz_como_baixar()
    {
        var vazia = Path.Combine(_pasta, "vazia");
        Directory.CreateDirectory(vazia);

        var erro = await Should.ThrowAsync<FileNotFoundException>(async () =>
        {
            await foreach (var _ in new LeitorDoTse(vazia).LerVotacaoAsync(2022))
            {
            }
        });

        erro.Message.ShouldContain("baixar-tse.mjs");
    }

    private static string Linha(params string[] campos) =>
        string.Join(';', campos.Select(c => $"\"{c}\""));

    private void Zip(string nome, Dictionary<string, string[]> arquivos)
    {
        using var zip = ZipFile.Open(Path.Combine(_pasta, nome), ZipArchiveMode.Create);

        foreach (var (entrada, linhas) in arquivos)
        {
            using var fluxo = zip.CreateEntry(entrada).Open();
            using var escritor = new StreamWriter(fluxo, Encoding.Latin1);
            escritor.Write(string.Join('\n', linhas));
        }
    }
}
