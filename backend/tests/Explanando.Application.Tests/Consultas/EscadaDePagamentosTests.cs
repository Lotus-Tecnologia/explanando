using Explanando.Application.Consultas;

namespace Explanando.Application.Tests.Consultas;

/// <summary>
/// A escada de pagamentos por fornecedor — a Fase 4 do plano de execução.
///
/// <para>"Pronto quando o ano parcial aparece marcado em teste, e um fornecedor com um ano só
/// não ganha seta de crescimento."</para>
/// </summary>
public class EscadaDePagamentosTests
{
    private static readonly DateTimeOffset Qualquer = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Um pagamento por mês, nos meses informados.</summary>
    private static IEnumerable<PagamentoBruto> Mensal(
        string documento, int ano, IEnumerable<int> meses, decimal valor, string nome = "EMPRESA") =>
        meses.Select(mes => new PagamentoBruto(documento, nome, ano, mes, valor, Qualquer));

    private static IEnumerable<int> Ano => Enumerable.Range(1, 12);

    [Fact]
    public void O_ano_sem_os_doze_meses_e_marcado_parcial()
    {
        // O mandato começa em fevereiro: o primeiro ano tem onze meses.
        var pagamentos = Mensal("A", 2023, Enumerable.Range(2, 11), 100m)
            .Concat(Mensal("A", 2024, Ano, 100m))
            .ToList();

        var escada = EscadaDePagamentos.Montar(pagamentos);

        escada.Anos.Single(a => a.Ano == 2023).Parcial.ShouldBeTrue();
        escada.Anos.Single(a => a.Ano == 2023).MesesComDespesa.ShouldBe(11);
        escada.Anos.Single(a => a.Ano == 2024).Parcial.ShouldBeFalse();
    }

    [Fact]
    public void Nao_calcula_variacao_contra_ano_parcial()
    {
        // 2023 com 11 meses e 2024 com 12: "cresceu 9%" seria o mês a mais, e nada mais.
        var pagamentos = Mensal("A", 2023, Enumerable.Range(2, 11), 100m)
            .Concat(Mensal("A", 2024, Ano, 100m))
            .ToList();

        var escada = EscadaDePagamentos.Montar(pagamentos);

        escada.AnoDaVariacao.ShouldBeNull();
        escada.Fornecedores.Single().Variacao.ShouldBeNull();
    }

    [Fact]
    public void Mede_a_variacao_entre_os_dois_ultimos_anos_completos()
    {
        var pagamentos = Mensal("A", 2024, Ano, 100m)
            .Concat(Mensal("A", 2025, Ano, 300m))
            .Concat(Mensal("A", 2026, Enumerable.Range(1, 8), 900m))
            .ToList();

        var escada = EscadaDePagamentos.Montar(pagamentos);

        escada.AnoBaseDaVariacao.ShouldBe(2024);
        escada.AnoDaVariacao.ShouldBe(2025);
        escada.Fornecedores.Single().Variacao.ShouldBe(2m, "de 1.200 para 3.600 é +200%");
    }

    [Fact]
    public void Fornecedor_com_um_ano_so_nao_ganha_seta()
    {
        // Os anos são completos — é o fornecedor B que só aparece em 2025.
        var pagamentos = Mensal("A", 2024, Ano, 100m)
            .Concat(Mensal("A", 2025, Ano, 100m))
            .Concat(Mensal("B", 2025, [3], 5_000m))
            .ToList();

        var escada = EscadaDePagamentos.Montar(pagamentos);

        escada.Fornecedores.Single(f => f.Documento == "B").Variacao.ShouldBeNull();
        escada.Fornecedores.Single(f => f.Documento == "A").Variacao.ShouldBe(0m);
    }

    [Fact]
    public void Anos_completos_com_buraco_no_meio_nao_sao_seguidos()
    {
        var pagamentos = Mensal("A", 2023, Ano, 100m)
            .Concat(Mensal("A", 2024, [1, 2], 100m))
            .Concat(Mensal("A", 2025, Ano, 300m))
            .ToList();

        EscadaDePagamentos.Montar(pagamentos).AnoDaVariacao.ShouldBeNull();
    }

    [Fact]
    public void Cada_fornecedor_tem_um_valor_por_ano_com_zero_onde_nao_recebeu()
    {
        var pagamentos = Mensal("A", 2024, Ano, 100m)
            .Concat(Mensal("B", 2025, [5], 50m))
            .ToList();

        var b = EscadaDePagamentos.Montar(pagamentos).Fornecedores.Single(f => f.Documento == "B");

        b.PorAno.Select(v => v.Ano).ShouldBe([2024, 2025]);
        b.PorAno.Select(v => v.Valor).ShouldBe([0m, 50m]);
    }

    [Fact]
    public void Traz_os_maiores_e_conta_o_resto()
    {
        var pagamentos = Enumerable.Range(1, 15)
            .SelectMany(i => Mensal($"F{i:D2}", 2025, [1], i * 10m))
            .ToList();

        var escada = EscadaDePagamentos.Montar(pagamentos, quantos: 3);

        escada.TotalDeFornecedores.ShouldBe(15);
        escada.Fornecedores.Select(f => f.Documento).ShouldBe(["F15", "F14", "F13"]);
    }

    [Fact]
    public void O_nome_e_o_da_nota_mais_recente()
    {
        PagamentoBruto[] pagamentos =
        [
            new("A", "NOME ANTIGO LTDA", 2024, 1, 10m, new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero)),
            new("A", "NOME NOVO LTDA", 2025, 1, 10m, new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero)),
        ];

        EscadaDePagamentos.Montar(pagamentos).Fornecedores.Single().Nome.ShouldBe("NOME NOVO LTDA");
    }

    [Fact]
    public void Ano_com_estorno_maior_que_o_gasto_nao_vira_variacao_com_sinal_trocado()
    {
        var pagamentos = Mensal("A", 2024, Ano, 100m)
            .Concat(Mensal("A", 2025, Ano, 0m))
            .Append(new PagamentoBruto("A", "EMPRESA", 2025, 6, -50m, Qualquer))
            .ToList();

        EscadaDePagamentos.Montar(pagamentos).Fornecedores.Single().Variacao.ShouldBeNull();
    }

    [Fact]
    public void Sem_pagamento_a_escada_vem_vazia()
    {
        var escada = EscadaDePagamentos.Montar([]);

        escada.Anos.ShouldBeEmpty();
        escada.Fornecedores.ShouldBeEmpty();
    }
}
