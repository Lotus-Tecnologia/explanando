using Explanando.Application.Ingestao;
using Explanando.Domain.Parlamentares;
using Explanando.Infrastructure.Integracoes.Senado;

namespace Explanando.Integration.Tests.Integracoes;

/// <summary>
/// O que separa a coleta do Senado da coleta da Câmara.
///
/// <para>A Câmara serve despesa por deputado; a CEAPS serve o ano inteiro de todos num array
/// só. O agrupamento por <c>codSenador</c> é a peça que reconcilia as duas formas — sem ela,
/// <see cref="DespesaDaFonte"/> chega sem dono e a despesa não tem a quem pertencer.</para>
/// </summary>
public sealed class ClienteDoSenadoTests
{
    private static readonly string Amostra = Fixtures.Ler("senado-ceaps-amostra.json");

    // ------------------------------------------------------------------
    //  O agrupamento que devolve a identidade
    // ------------------------------------------------------------------

    [Fact]
    public void Agrupar_por_senador_preserva_todas_as_despesas_do_lote()
    {
        // A soma tem de bater com a conversao plana: agrupar nao pode perder lancamento.
        var planas = ConversorDoSenado.ConverterLote(Amostra);
        var agrupadas = ConversorDoSenado.ConverterLotePorSenador(Amostra);

        agrupadas.Sum(lote => lote.Despesas.Count).ShouldBe(planas.Count);
    }

    [Fact]
    public void Cada_grupo_carrega_o_codigo_do_senador()
    {
        var agrupadas = ConversorDoSenado.ConverterLotePorSenador(Amostra);

        agrupadas.ShouldNotBeEmpty();
        agrupadas.ShouldAllBe(lote => lote.CodSenador > 0);

        // Um codigo por grupo: agrupar duas vezes o mesmo senador dividiria as despesas
        // dele em dois checkpoints, e o segundo sobrescreveria o primeiro.
        agrupadas.Select(lote => lote.CodSenador).Distinct().Count().ShouldBe(agrupadas.Count);
    }

    [Fact]
    public void O_nome_da_origem_vem_junto_porque_e_a_rede_de_seguranca()
    {
        // 2025 teve 82 senadores para 81 cadeiras. Quem assumiu como suplente e saiu some
        // das listas de exercicio, e sem este nome a pessoa viraria "Senador 1234".
        var agrupadas = ConversorDoSenado.ConverterLotePorSenador(Amostra);

        agrupadas.ShouldContain(lote => !string.IsNullOrWhiteSpace(lote.NomeNaOrigem));
    }

    [Fact]
    public void Registro_sem_codigo_de_senador_e_descartado()
    {
        // Sem codigo a despesa nao tem dono, e atribui-la a alguem seria inventar. O
        // conversor plano segue aceitando: e' o agrupamento que exige identidade.
        const string SemCodigo = """
            [
              { "id": 1, "codSenador": 0,  "ano": 2025, "mes": 3, "valorReembolsado": 10.0 },
              { "id": 2, "codSenador": 22, "ano": 2025, "mes": 3, "valorReembolsado": 20.0 }
            ]
            """;

        var agrupadas = ConversorDoSenado.ConverterLotePorSenador(SemCodigo);

        agrupadas.Count.ShouldBe(1);
        agrupadas[0].CodSenador.ShouldBe(22);
    }

    [Fact]
    public void Lote_vazio_nao_explode()
    {
        ConversorDoSenado.ConverterLotePorSenador("[]").ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  A fronteira com o banco
    // ------------------------------------------------------------------

    [Fact]
    public void Toda_data_sai_em_UTC_porque_o_Npgsql_recusa_qualquer_outro_offset()
    {
        // Custou a primeira coleta. A CEAPS entrega a data com o offset de Brasilia, e
        // `timestamp with time zone` so' aceita offset zero: as tres primeiras gravacoes
        // morreram em SaveChangesAsync com "only offset 0 (UTC) is supported".
        //
        // Nenhum teste de conversao pegava, porque o defeito so' existe na fronteira com o
        // banco. Este fecha a porta sem precisar de banco.
        var despesas = ConversorDoSenado.ConverterLote(Amostra);

        despesas.ShouldNotBeEmpty();
        despesas.ShouldAllBe(despesa => despesa.DataDocumento.Offset == TimeSpan.Zero);
    }

    [Fact]
    public void A_data_do_documento_e_calendario_e_nao_instante()
    {
        // `ToUniversalTime()` tambem zeraria o offset — e estragaria o dado. Uma nota de
        // 10/03 emitida as 23h viraria 11/03, porque Brasilia esta tres horas atras de UTC.
        const string TardeDaNoite = """
            [{ "id": 1, "codSenador": 22, "ano": 2025, "mes": 3,
               "data": "2025-03-10T23:30:00-03:00", "valorReembolsado": 10.0 }]
            """;

        var despesa = ConversorDoSenado.ConverterLote(TardeDaNoite).ShouldHaveSingleItem();

        despesa.DataDocumento.Offset.ShouldBe(TimeSpan.Zero);
        despesa.DataDocumento.Day.ShouldBe(10);
        despesa.DataDocumento.Hour.ShouldBe(23);
    }

    // ------------------------------------------------------------------
    //  Legislatura -> anos, que e' o que a CEAPS entende
    // ------------------------------------------------------------------

    [Fact]
    public void A_legislatura_57_cobre_2023_a_2026()
    {
        ClienteDoSenado.AnosDaLegislatura(57, 2026).ShouldBe([2023, 2024, 2025, 2026]);
    }

    [Fact]
    public void A_legislatura_anterior_cobre_os_quatro_anos_dela()
    {
        ClienteDoSenado.AnosDaLegislatura(56, 2026).ShouldBe([2019, 2020, 2021, 2022]);
    }

    [Fact]
    public void Nao_pede_ano_que_ainda_nao_aconteceu()
    {
        // Pedir 2027 a' CEAPS devolveria lista vazia, e lista vazia e' exatamente o que o
        // produto nao pode confundir com "nao houve gasto".
        ClienteDoSenado.AnosDaLegislatura(57, 2024).ShouldBe([2023, 2024]);
    }

    [Fact]
    public void Legislatura_que_ainda_nao_comecou_nao_devolve_ano_nenhum()
    {
        ClienteDoSenado.AnosDaLegislatura(58, 2026).ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    //  A casa atravessa a fronteira
    // ------------------------------------------------------------------

    [Fact]
    public void O_padrao_da_fonte_continua_sendo_deputado()
    {
        // O caminho da Camara nao conhece este campo. Se o padrao mudasse, 513 deputados
        // seriam regravados como senadores na proxima coleta.
        var deputado = new DeputadoDaFonte
        {
            IdExterno = 1,
            NomeCivil = "x",
            NomeParlamentar = "x",
            SiglaPartido = "x",
            Uf = "SP",
        };

        deputado.Tipo.ShouldBe(TipoParlamentar.DeputadoFederal);
    }
}
