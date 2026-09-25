import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { PagamentosPorFornecedor } from "@/lib/contrato";
import { EscadaDeFornecedores, Variacao } from "./EscadaDeFornecedores";

/**
 * A escada de fornecedores — a Fase 4 do plano de execução.
 *
 * "Pronto quando o ano parcial aparece marcado em teste, e um fornecedor com um ano só não
 * ganha seta de crescimento." A regra vem da API; aqui se prova que a tela a mostra.
 */

const ESCADA: PagamentosPorFornecedor = {
  anos: [
    { ano: 2023, mesesComDespesa: 11, parcial: true },
    { ano: 2024, mesesComDespesa: 12, parcial: false },
    { ano: 2025, mesesComDespesa: 12, parcial: false },
  ],
  anoBaseDaVariacao: 2024,
  anoDaVariacao: 2025,
  totalDeFornecedores: 40,
  fornecedores: [
    {
      documento: "48.114.026/0001-97",
      nome: "DFCROSSMIDIA",
      total: 107_000,
      despesas: 20,
      porAno: [
        { ano: 2023, valor: 0, despesas: 0 },
        { ano: 2024, valor: 16_000, despesas: 2 },
        { ano: 2025, valor: 91_000, despesas: 18 },
      ],
      variacao: 4.6875,
    },
    {
      documento: "50.515.058/0001-46",
      nome: "NOVOGRAF",
      total: 146_000,
      despesas: 10,
      porAno: [
        { ano: 2023, valor: 146_000, despesas: 10 },
        { ano: 2024, valor: 0, despesas: 0 },
        { ano: 2025, valor: 0, despesas: 0 },
      ],
      variacao: null,
    },
  ],
};

describe("EscadaDeFornecedores", () => {
  it("marca o ano parcial no cabeçalho, com os meses", () => {
    render(<EscadaDeFornecedores escada={ESCADA} />);

    const cabecalho = screen.getByRole("columnheader", { name: /2023/ });
    expect(cabecalho).toHaveTextContent("parcial · 11 meses");
    expect(screen.getByRole("columnheader", { name: "2024" })).not.toHaveTextContent("parcial");
  });

  it("diz entre que anos a variação foi medida", () => {
    render(<EscadaDeFornecedores escada={ESCADA} />);

    expect(screen.getByText(/compara 2024 com 2025/)).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: "2024→2025" })).toBeInTheDocument();
  });

  it("fornecedor sem os dois anos não ganha seta", () => {
    render(<EscadaDeFornecedores escada={ESCADA} />);

    const linha = screen.getByRole("row", { name: /NOVOGRAF/ });
    expect(within(linha).queryByText(/[↑↓]/)).not.toBeInTheDocument();
    expect(within(linha).getByTitle("Não recebeu nos dois anos comparados")).toBeInTheDocument();
  });

  it("sem dois anos completos, não há coluna de variação e a tela diz por quê", () => {
    render(
      <EscadaDeFornecedores
        escada={{ ...ESCADA, anoBaseDaVariacao: null, anoDaVariacao: null }}
      />,
    );

    expect(screen.getByText(/não há variação a medir/)).toBeInTheDocument();
    expect(screen.queryByRole("columnheader", { name: /→/ })).not.toBeInTheDocument();
  });

  it("sem fornecedor não desenha nada", () => {
    const { container } = render(
      <EscadaDeFornecedores escada={{ ...ESCADA, fornecedores: [] }} />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});

describe("Variacao", () => {
  it("sobe com seta e sinal, e diz em palavras para leitor de tela", () => {
    render(<Variacao valor={4.6875} />);
    expect(screen.getByLabelText("subiu 469%")).toHaveTextContent("↑ +469%");
  });

  it("cai com o sinal de menos tipográfico", () => {
    render(<Variacao valor={-0.39} />);
    expect(screen.getByLabelText("caiu 39%")).toHaveTextContent("↓ −39%");
  });

  it("variação que arredonda para zero é estável, não '+0%'", () => {
    render(<Variacao valor={0.001} />);
    expect(screen.getByText("estável")).toBeInTheDocument();
  });
});
