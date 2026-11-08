import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/navigation", () => ({ usePathname: () => "/camara/indice" }));

import { Cabecalho } from "./Cabecalho";

describe("Cabecalho", () => {
  it("marca a seção e a ordenação atuais com aria-current", () => {
    render(<Cabecalho />);

    const atuais = screen.getAllByRole("link", { current: "page" }).map((l) => l.textContent);
    expect(atuais).toContain("Câmara");
    expect(atuais).toContain("Densidade de indicadores");
  });

  it("menu do celular abre com botão rotulado de 44px", async () => {
    render(<Cabecalho />);

    const botao = screen.getByRole("button", { name: "Abrir menu" });
    expect(botao.className).toContain("size-11");
    expect(botao).toHaveAttribute("aria-expanded", "false");

    await userEvent.click(botao);

    expect(screen.getByRole("button", { name: "Fechar menu" })).toHaveAttribute("aria-expanded", "true");
    expect(document.getElementById("menu-do-celular")).not.toBeNull();
  });
});
