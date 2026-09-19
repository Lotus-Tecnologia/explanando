import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { LinhaDoTempoDoEstado } from "./PainelDoEstado";
import type { MudancaDoEstado } from "@/lib/contrato";

/**
 * A linha do tempo da estrutura.
 *
 * Duas coisas estão protegidas aqui. A primeira é o **estado vazio**, que tem dois
 * significados opostos — "ainda não comparamos" e "comparamos e nada mudou" — e trocá-los
 * é o erro dos zeros do ADR-0009 noutra tabela.
 *
 * A segunda é o **cartão saiu/entrou**: o valor antigo riscado é o que faz a transição se
 * ler sem uma frase, e o que impede o leitor de ter que decidir qual dos dois é o novo.
 */
describe("LinhaDoTempoDoEstado", () => {
  const mudanca = (parcial: Partial<MudancaDoEstado> = {}): MudancaDoEstado => ({
    codigoUnidade: 26,
    unidade: "Presidência da República",
    descricao: "mudou de nome",
    chave: "alterada",
    selo: "Alterada",
    de: "Presidência",
    para: "Presidência da República",
    detectadaEm: "2026-09-19T03:00:00Z",
    ...parcial,
  });

  it("sem historico, diz que ainda nao ha' com o que comparar", () => {
    render(<LinhaDoTempoDoEstado mudancas={[]} temHistorico={false} />);

    expect(screen.getByText(/ainda não há com o que comparar/i)).toBeInTheDocument();
  });

  it("com historico e sem mudanca, diz que nada mudou", () => {
    // Sao afirmacoes opostas, e trocar uma pela outra e' o erro que este teste impede.
    render(<LinhaDoTempoDoEstado mudancas={[]} temHistorico={true} />);

    expect(screen.getByText(/Nada mudou na estrutura/i)).toBeInTheDocument();
    expect(screen.queryByText(/ainda não há com o que comparar/i)).toBeNull();
  });

  it("falha de carga nao e' confundida com ausencia de mudanca", () => {
    render(<LinhaDoTempoDoEstado mudancas={null} temHistorico={true} />);

    expect(screen.getByText(/não pôde ser carregada/i)).toBeInTheDocument();
    expect(screen.queryByText(/Nada mudou/i)).toBeNull();
  });

  it("o valor que saiu vem RISCADO, e o que entrou nao", () => {
    // E' a ideia inteira do cartao. Sem o risco, "de X para Y" obriga o leitor a decidir
    // qual e' qual.
    const { container } = render(
      <LinhaDoTempoDoEstado mudancas={[mudanca()]} temHistorico />,
    );

    const riscado = container.querySelector(".line-through");

    expect(riscado?.textContent).toBe("Presidência");
    expect(screen.getByText("Presidência da República", { selector: "span" })).toBeInTheDocument();
  });

  it("o selo aparece, e vem do dominio", () => {
    render(<LinhaDoTempoDoEstado mudancas={[mudanca({ selo: "Deixou de constar" })]} temHistorico />);

    expect(screen.getByText("Deixou de constar")).toBeInTheDocument();
  });

  it("sem valor anterior, diz que nao havia registro — e nao inventa um", () => {
    render(<LinhaDoTempoDoEstado mudancas={[mudanca({ de: null })]} temHistorico />);

    expect(screen.getByText(/Não havia registro anterior/i)).toBeInTheDocument();
  });

  it("quando nada entra no lugar, o cartao diz isso", () => {
    render(
      <LinhaDoTempoDoEstado
        mudancas={[mudanca({ selo: "Deixou de constar", para: null })]}
        temHistorico
      />,
    );

    expect(screen.getByText(/Nada no lugar/i)).toBeInTheDocument();
  });

  it("a data e' de DETECCAO, nao de ocorrencia", () => {
    // A fonte nao data a mudanca; nos a deduzimos comparando coletas. Escrever
    // "aconteceu em" seria precisao inventada.
    render(<LinhaDoTempoDoEstado mudancas={[mudanca()]} temHistorico />);

    expect(screen.getByText(/detectada em/i)).toBeInTheDocument();
  });

  it("uma mudanca sem par de valores nao desenha o bloco saiu/entrou", () => {
    // E' o caso de "passou a constar": nao ha' de nem para, e um bloco com dois vazios
    // so' ocuparia espaco.
    const { container } = render(
      <LinhaDoTempoDoEstado
        mudancas={[mudanca({ chave: "criada", selo: "Passou a constar", de: null, para: null })]}
        temHistorico
      />,
    );

    expect(container.querySelector("dl")).toBeNull();
  });

  it("lista todas as mudancas, na ordem recebida", () => {
    const lista = [
      mudanca({ unidade: "Primeira" }),
      mudanca({ unidade: "Segunda" }),
    ];

    render(<LinhaDoTempoDoEstado mudancas={lista} temHistorico />);

    const itens = screen.getAllByRole("listitem");

    expect(itens).toHaveLength(2);
    expect(within(itens[0]).getByText("Primeira")).toBeInTheDocument();
    expect(within(itens[1]).getByText("Segunda")).toBeInTheDocument();
  });
});
