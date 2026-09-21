import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { BuscaRapidaHome } from "./BuscaRapidaHome";
import type { ItemDoRanking } from "@/lib/contrato";

// Mock de useRouter
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));

const parlamentaresMock: ItemDoRanking[] = [
  {
    id: "uuid-1",
    idExterno: 1001,
    nomeParlamentar: "Pompeo de Mattos",
    siglaPartido: "PDT",
    uf: "RS",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 2287735.08,
    despesasAvaliadas: 2878,
    indiceDeRisco: 4.1,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 49,
    flagsBaixas: 20,
    amostraSuficiente: true,
  },
  {
    id: "uuid-2",
    idExterno: 1002,
    nomeParlamentar: "Helena Lima",
    siglaPartido: "PSD",
    uf: "RR",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 2162905.31,
    despesasAvaliadas: 1609,
    indiceDeRisco: 5.34,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 31,
    flagsBaixas: 24,
    amostraSuficiente: true,
  },
  {
    id: "uuid-3",
    idExterno: 2001,
    nomeParlamentar: "Romário",
    siglaPartido: "PL",
    uf: "RJ",
    casa: "Senado Federal",
    fotoUrl: null,
    volumeTotalGasto: 1500000.0,
    despesasAvaliadas: 950,
    indiceDeRisco: 2.1,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 10,
    flagsBaixas: 15,
    amostraSuficiente: true,
  },
];

describe("BuscaRapidaHome", () => {
  it("renderiza o campo de busca com placeholder acessível", () => {
    render(<BuscaRapidaHome parlamentares={parlamentaresMock} />);

    const input = screen.getByRole("combobox");
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute("placeholder", expect.stringContaining("Buscar por nome"));
  });

  it("filtra parlamentares por nome mesmo com acentos dobrados", async () => {
    const user = userEvent.setup();
    render(<BuscaRapidaHome parlamentares={parlamentaresMock} />);

    const input = screen.getByRole("combobox");
    // Digita 'romario' sem acento
    await user.type(input, "romario");

    expect(screen.getByText("Romário")).toBeInTheDocument();
    expect(screen.queryByText("Pompeo de Mattos")).not.toBeInTheDocument();
  });

  it("filtra parlamentares por partido ou UF", async () => {
    const user = userEvent.setup();
    render(<BuscaRapidaHome parlamentares={parlamentaresMock} />);

    const input = screen.getByRole("combobox");
    await user.type(input, "pdt");

    expect(screen.getByText("Pompeo de Mattos")).toBeInTheDocument();
    expect(screen.queryByText("Romário")).not.toBeInTheDocument();
  });

  it("exibe mensagem quando nada é encontrado", async () => {
    const user = userEvent.setup();
    render(<BuscaRapidaHome parlamentares={parlamentaresMock} />);

    const input = screen.getByRole("combobox");
    await user.type(input, "inexistente");

    expect(screen.getByText(/Nenhum parlamentar encontrado/i)).toBeInTheDocument();
  });

  it("limpa a busca ao clicar no botão de fechar", async () => {
    const user = userEvent.setup();
    render(<BuscaRapidaHome parlamentares={parlamentaresMock} />);

    const input = screen.getByRole("combobox");
    await user.type(input, "helena");

    expect(screen.getByText("Helena Lima")).toBeInTheDocument();

    const botaoLimpar = screen.getByRole("button", { name: /Limpar busca/i });
    await user.click(botaoLimpar);

    expect(input).toHaveValue("");
    expect(screen.queryByText("Helena Lima")).not.toBeInTheDocument();
  });
});
