import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { VistasDoEstado } from "./VistasDoEstado";

// O desenho em si não é o que se testa aqui — só se ele começa recolhido no celular.
vi.mock("./GrafoDoEstado", () => ({
  GrafoDoEstado: () => <div data-testid="grafo">desenho</div>,
}));

/**
 * Decisão de 26/09/2026: a 375 px o grafo tem rótulos de ~5 px e nós de 11 px — ilegível e
 * pequeno demais para o dedo (auditoria de 25/09). Em tela pequena o mapa abre na lista; o
 * desenho fica a um toque. A troca é por classe de CSS, e não por medir a tela no navegador,
 * para que o HTML já chegue certo e nada pisque na hidratação.
 */
describe("VistasDoEstado no celular", () => {
  it("o desenho começa escondido abaixo de sm, e visível dali para cima", () => {
    render(<VistasDoEstado nos={[]} poderes={null} />);

    const caixa = screen.getByTestId("grafo").parentElement!;

    expect(caixa.className).toContain("hidden");
    expect(caixa.className).toContain("sm:block");
  });

  it("oferece a lista primeiro e o desenho a um toque", async () => {
    render(<VistasDoEstado nos={[]} poderes={null} />);

    expect(screen.getByRole("link", { name: /lista/i }).getAttribute("href")).toBe("#orgaos-e-entidades");

    await userEvent.setup().click(screen.getByRole("button", { name: /ver o desenho/i }));

    expect(screen.getByTestId("grafo").parentElement!.className).not.toContain("hidden");
  });
});
