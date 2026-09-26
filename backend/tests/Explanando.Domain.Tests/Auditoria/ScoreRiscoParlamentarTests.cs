using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;

namespace Explanando.Domain.Tests.Auditoria;

/// <summary>
/// Dois números independentes, um para cada comprador (ADR-0009): volume em reais para o
/// ranking público, densidade de indicadores para a API de due diligence.
/// </summary>
public sealed class ScoreRiscoParlamentarTests
{
    private const int PisoAmostral = 100;

    private static readonly Guid ParlamentarId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DespesaId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static FlagAnomalia Flag(Gravidade gravidade, decimal confianca = 1.0m) =>
        FlagAnomalia.Criar(DespesaId, "REG_KYB_001", gravidade, "Inconsistência financeira.", confianca);

    /// <summary>Gera <paramref name="quantidade"/> despesas de igual valor.</summary>
    private static List<Despesa> Despesas(int quantidade, decimal valorCada = 1_000m) =>
        [.. Enumerable.Range(1, quantidade).Select(i => Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = ParlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
            NomeFornecedor = "FORNECEDOR",
            Competencia = Competencia.Criar(2026, 7),
            TipoDespesa = "TELEFONIA",
            CodigoDocumento = $"doc-{i}",
            NumeroDocumento = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = valorCada,
            ValorLiquido = valorCada,
        }))];

    private static ScoreRiscoParlamentar Recalculado(
        int quantidadeDeDespesas,
        IReadOnlyCollection<FlagAnomalia> flags,
        decimal valorCada = 1_000m,
        int piso = PisoAmostral)
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular(
            Despesas(quantidadeDeDespesas, valorCada), flags, DateTimeOffset.UtcNow, piso,
            despesasComFornecedor: quantidadeDeDespesas);
        return score;
    }

    // ------------------------------------------------------------------
    //  A cobertura de fornecedor (decisão de 26/09/2026)
    // ------------------------------------------------------------------

    [Fact]
    public void A_cobertura_e_a_fracao_das_despesas_com_fornecedor_identificado()
    {
        // Auditoria de 25/09: 92 dos 498 deputados ranqueados tinham menos da metade das
        // despesas com fornecedor identificado — quase sempre por passagem aérea, que a
        // Câmara publica sem fornecedor. O índice deles descrevia menos da metade do gasto.
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular(Despesas(200), [], DateTimeOffset.UtcNow, PisoAmostral, despesasComFornecedor: 60);

        score.DespesasComFornecedor.ShouldBe(60);
        score.CoberturaDeFornecedor.ShouldBe(0.3m);
        score.CoberturaSuficiente.ShouldBeFalse();
    }

    [Fact]
    public void Metade_ou_mais_com_fornecedor_basta_para_o_ranking_de_indice()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular(Despesas(200), [], DateTimeOffset.UtcNow, PisoAmostral, despesasComFornecedor: 100);

        score.CoberturaSuficiente.ShouldBeTrue();
    }

    [Fact]
    public void Sem_despesa_nao_ha_cobertura_suficiente()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        score.Recalcular([], [], DateTimeOffset.UtcNow, PisoAmostral, despesasComFornecedor: 0);

        score.CoberturaDeFornecedor.ShouldBe(0m);
        score.CoberturaSuficiente.ShouldBeFalse();
    }

    [Fact]
    public void Mais_despesas_com_fornecedor_do_que_despesas_e_erro_de_quem_chamou()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        Should.Throw<ArgumentOutOfRangeException>(() => score.Recalcular(
            Despesas(10), [], DateTimeOffset.UtcNow, PisoAmostral, despesasComFornecedor: 11));
    }

    [Fact]
    public void Nasce_zerado()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.VolumeTotalGasto.ShouldBe(0m);
        score.IndiceDeRisco.ShouldBe(0m);
        score.PesoDasFlags.ShouldBe(0m);
        score.DespesasAvaliadas.ShouldBe(0);
        score.AmostraSuficiente.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  Volume — o número do B2C
    // ------------------------------------------------------------------

    [Fact]
    public void Volume_soma_o_que_o_erario_pagou()
    {
        var score = Recalculado(200, [], valorCada: 500m);

        score.VolumeTotalGasto.ShouldBe(100_000m);
    }

    [Fact]
    public void Volume_nao_depende_de_flag_nenhuma()
    {
        // É fato bruto: gastar a cota inteira é legal. O número não imputa conduta.
        var semFlag = Recalculado(200, [], valorCada: 500m);
        var comFlags = Recalculado(200, [Flag(Gravidade.Alta), Flag(Gravidade.Alta)], valorCada: 500m);

        comFlags.VolumeTotalGasto.ShouldBe(semFlag.VolumeTotalGasto);
    }

    [Fact]
    public void Volume_e_publicado_mesmo_sem_amostra_suficiente()
    {
        // O ranking de volume não depende de amostra — é só uma soma.
        var score = Recalculado(5, [], valorCada: 20_000m);

        score.AmostraSuficiente.ShouldBeFalse();
        score.VolumeTotalGasto.ShouldBe(100_000m);
    }

    // ------------------------------------------------------------------
    //  Índice de risco — o número do B2B
    // ------------------------------------------------------------------

    [Fact]
    public void Indice_de_risco_e_densidade_por_cem_despesas()
    {
        // 2 flags altas (peso 3 cada) = 6, sobre 200 despesas = 3 pontos por 100.
        var score = Recalculado(200, [Flag(Gravidade.Alta), Flag(Gravidade.Alta)]);

        score.PesoDasFlags.ShouldBe(6m);
        score.IndiceDeRisco.ShouldBe(3m);
    }

    [Fact]
    public void Indice_corrige_a_distorcao_que_a_calibragem_real_expos()
    {
        // O caso concreto do ADR-0009: na base real, Afonso Hamm aparecia ACIMA de
        // Albuquerque tendo 1 flag vermelha contra 14, só por ter emitido 5x mais notas.
        var muitasNotasPoucasFlags = Recalculado(3_575, [Flag(Gravidade.Alta)]);
        var poucasNotasMuitasFlags = Recalculado(
            752, [.. Enumerable.Range(1, 14).Select(_ => Flag(Gravidade.Alta))]);

        muitasNotasPoucasFlags.PesoDasFlags.ShouldBeLessThan(poucasNotasMuitasFlags.PesoDasFlags);
        poucasNotasMuitasFlags.IndiceDeRisco.ShouldBeGreaterThan(muitasNotasPoucasFlags.IndiceDeRisco);
    }

    [Fact]
    public void Achado_incerto_pesa_menos_que_achado_certo()
    {
        var certo = Recalculado(200, [Flag(Gravidade.Alta, 1.0m)]);
        var incerto = Recalculado(200, [Flag(Gravidade.Alta, 0.5m)]);

        incerto.IndiceDeRisco.ShouldBeLessThan(certo.IndiceDeRisco);
    }

    [Fact]
    public void Conta_apenas_as_vermelhas_no_total_de_vermelhas()
    {
        var score = Recalculado(
            200, [Flag(Gravidade.Alta), Flag(Gravidade.Alta), Flag(Gravidade.Media)]);

        score.TotalFlagsVermelhas.ShouldBe(2);
    }

    // ------------------------------------------------------------------
    //  Composição do índice — para não sugerir gravidade que não existe
    // ------------------------------------------------------------------

    [Fact]
    public void Indice_se_decompoe_por_gravidade()
    {
        // 1 alta (3) + 2 médias (2 cada = 4) + 1 baixa (1) = 8, sobre 200 despesas.
        var score = Recalculado(200,
            [Flag(Gravidade.Alta), Flag(Gravidade.Media), Flag(Gravidade.Media), Flag(Gravidade.Baixa)]);

        score.IndiceDeRisco.ShouldBe(4m);
        score.IndiceDeAltas.ShouldBe(1.5m);
        score.IndiceDeMedias.ShouldBe(2m);
        score.IndiceDeBaixas.ShouldBe(0.5m);
    }

    [Fact]
    public void As_partes_somam_o_total()
    {
        var score = Recalculado(347,
            [Flag(Gravidade.Alta), Flag(Gravidade.Media, 0.85m), Flag(Gravidade.Baixa)]);

        (score.IndiceDeAltas + score.IndiceDeMedias + score.IndiceDeBaixas)
            .ShouldBe(score.IndiceDeRisco, tolerance: 0.0001m);
    }

    [Fact]
    public void Conta_as_flags_de_cada_gravidade()
    {
        var score = Recalculado(200,
            [Flag(Gravidade.Alta), Flag(Gravidade.Media), Flag(Gravidade.Media), Flag(Gravidade.Baixa)]);

        score.TotalFlagsVermelhas.ShouldBe(1);
        score.TotalFlagsMedias.ShouldBe(2);
        score.TotalFlagsBaixas.ShouldBe(1);
    }

    [Fact]
    public void Marca_indice_sem_gravidade_alta()
    {
        // Aconteceu na base real: o primeiro colocado em risco tinha ZERO flags altas.
        // O número é honesto, mas quem lê só o total pode inferir gravidade que não existe.
        var soMedias = Recalculado(200, [Flag(Gravidade.Media), Flag(Gravidade.Media)]);

        soMedias.IndiceDeRisco.ShouldBeGreaterThan(0m);
        soMedias.TotalFlagsVermelhas.ShouldBe(0);
        soMedias.IndiceSemGravidadeAlta.ShouldBeTrue();
    }

    [Fact]
    public void Indice_com_alguma_flag_alta_nao_recebe_o_alerta_de_leitura()
    {
        Recalculado(200, [Flag(Gravidade.Alta), Flag(Gravidade.Media)])
            .IndiceSemGravidadeAlta.ShouldBeFalse();
    }

    [Fact]
    public void Parlamentar_limpo_nao_recebe_o_alerta_de_leitura()
    {
        // Sem flag nenhuma não há o que interpretar mal — o alerta seria ruído.
        Recalculado(200, []).IndiceSemGravidadeAlta.ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    //  Piso amostral — a armadilha da normalização
    // ------------------------------------------------------------------

    [Fact]
    public void Amostra_pequena_nao_entra_no_ranking_de_risco()
    {
        // Densidade pura criaria a distorção inversa: 10 despesas com 2 flags marcaria
        // 20% e lideraria o ranking com prova fina. Na dúvida, o motor se cala (ADR-0007).
        var score = Recalculado(10, [Flag(Gravidade.Alta), Flag(Gravidade.Alta)]);

        score.AmostraSuficiente.ShouldBeFalse();
        score.IndiceDeRisco.ShouldBeGreaterThan(0m, "o valor continua calculado, só nao e' ranqueavel");
    }

    [Fact]
    public void Amostra_no_piso_ja_conta_como_suficiente()
    {
        Recalculado(PisoAmostral, [Flag(Gravidade.Alta)]).AmostraSuficiente.ShouldBeTrue();
    }

    [Fact]
    public void Parlamentar_sem_despesa_nenhuma_nao_tem_indice()
    {
        // Divisão por zero seria o jeito mais bobo de derrubar a varredura.
        var score = Recalculado(0, []);

        score.IndiceDeRisco.ShouldBe(0m);
        score.AmostraSuficiente.ShouldBeFalse();
        score.DespesasAvaliadas.ShouldBe(0);
    }

    // ------------------------------------------------------------------
    //  Recálculo
    // ------------------------------------------------------------------

    [Fact]
    public void Recalcular_substitui_o_valor_anterior_em_vez_de_acumular()
    {
        // A varredura roda de novo sobre o mesmo parlamentar. Acumular inflaria o ranking
        // a cada execução, sem nenhum gasto novo ter existido.
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        var despesas = Despesas(200);
        var agora = DateTimeOffset.UtcNow;

        score.Recalcular(despesas, [Flag(Gravidade.Alta)], agora, PisoAmostral, despesas.Count);
        var primeiro = score.IndiceDeRisco;
        score.Recalcular(despesas, [Flag(Gravidade.Alta)], agora, PisoAmostral, despesas.Count);

        score.IndiceDeRisco.ShouldBe(primeiro);
        score.VolumeTotalGasto.ShouldBe(200_000m);
    }

    [Fact]
    public void Parlamentar_que_deixou_de_ter_flags_volta_a_zero()
    {
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);
        var despesas = Despesas(200);

        score.Recalcular(despesas, [Flag(Gravidade.Alta)], DateTimeOffset.UtcNow, PisoAmostral, despesas.Count);
        score.Recalcular(despesas, [], DateTimeOffset.UtcNow, PisoAmostral, despesas.Count);

        score.IndiceDeRisco.ShouldBe(0m);
        score.PesoDasFlags.ShouldBe(0m);
        score.TotalFlagsVermelhas.ShouldBe(0);
        score.VolumeTotalGasto.ShouldBe(200_000m, "o gasto continua existindo");
    }

    [Fact]
    public void Registra_quando_a_ultima_varredura_rodou()
    {
        var momento = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var score = ScoreRiscoParlamentar.Criar(ParlamentarId);

        score.Recalcular(Despesas(200), [], momento, PisoAmostral, 200);

        score.DataUltimaVarredura.ShouldBe(momento);
    }
}
