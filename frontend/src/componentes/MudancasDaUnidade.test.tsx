import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { MudancaDoEstado } from "@/lib/contrato";

const listar = vi.fn();

vi.mock("@/lib/api", () => ({
  listarMudancasDaUnidade: (codigo: number) => listar(codigo),
}));

const { MudancasDaUnidade } = await import("./MudancasDaUnidade");

/**
 * O feed da barra lateral.
 *
 * **Os três vazios têm que ser distinguíveis, e é isso que está protegido aqui.**
 * "Carregando", "não conseguimos carregar" e "nada mudou" parecem a mesma coisa numa tela
 * mal feita, e só o terceiro é uma afirmação sobre o Estado. O quarto caso — "ainda não
 * comparamos" — é o ADR-0009 aplicado por unidade: lista vazia antes da segunda coleta
 * não significa que nada mudou, significa que ninguém olhou duas vezes.
 */
describe("MudancasDaUnidade", () => {
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

  beforeEach(() => {
    listar.mockReset();
  });

  it("pergunta pela unidade que recebeu", async () => {
    listar.mockResolvedValue([]);

    render(<MudancasDaUnidade codigo={26} temHistorico />);

    await screen.findByText(/Nada mudou nesta unidade/);

    expect(listar).toHaveBeenCalledWith(26);
  });

  it("sem historico, diz que ainda nao ha' com o que comparar", async () => {
    // O zero do ADR-0009, agora por unidade: antes da SEGUNDA coleta, lista vazia nao
    // significa "nada mudou" — significa que ninguem olhou duas vezes.
    listar.mockResolvedValue([]);

    render(<MudancasDaUnidade codigo={26} temHistorico={false} />);

    expect(await screen.findByText(/ainda não há com o que comparar/i)).toBeInTheDocument();
    expect(screen.queryByText(/Nada mudou/)).toBeNull();
  });

  it("com historico e sem mudanca, diz que nada mudou", async () => {
    listar.mockResolvedValue([]);

    render(<MudancasDaUnidade codigo={26} temHistorico />);

    expect(await screen.findByText(/Nada mudou nesta unidade/)).toBeInTheDocument();
    expect(screen.queryByText(/ainda não há com o que comparar/i)).toBeNull();
  });

  it("falha de carga NAO e' confundida com ausencia de mudanca", async () => {
    // O erro que esta tela existe para nao cometer: a rede caiu, e o leitor concluiria
    // que o orgao esta' parado desde sempre.
    listar.mockResolvedValue(null);

    render(<MudancasDaUnidade codigo={26} temHistorico />);

    expect(await screen.findByText(/não pôde ser carregado/i)).toBeInTheDocument();
    expect(screen.queryByText(/Nada mudou/)).toBeNull();
  });

  it("enquanto carrega, nao afirma nada", async () => {
    listar.mockReturnValue(new Promise(() => {}));

    render(<MudancasDaUnidade codigo={26} temHistorico />);

    expect(screen.getByText(/Carregando/)).toBeInTheDocument();
    expect(screen.queryByText(/Nada mudou/)).toBeNull();
    expect(screen.queryByText(/não pôde ser carregado/i)).toBeNull();
  });

  it("lista as mudancas no formato saiu/entrou", async () => {
    listar.mockResolvedValue([mudanca()]);

    const { container } = render(<MudancasDaUnidade codigo={26} temHistorico />);

    await screen.findByText("Alterada");

    // O valor antigo RISCADO e' a ideia inteira do cartao — ver PainelDoEstado.
    expect(container.querySelector(".line-through")?.textContent).toBe("Presidência");
  });
});
