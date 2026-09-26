import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DashboardDaCasa } from "./DashboardDaCasa";
import { casaPorChave } from "@/lib/casas";
import { ordenacaoPorChave } from "@/lib/ordenacoes";
import type { ItemDoRanking } from "@/lib/contrato";

const CASA_CAMARA = casaPorChave("camara")!;
const ORDENACAO_VOLUME = ordenacaoPorChave("volume")!;

const MOCK_ITENS: ItemDoRanking[] = [
  {
    id: "p1",
    idExterno: 101,
    nomeParlamentar: "Deputado Alfa",
    siglaPartido: "PL",
    uf: "SP",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 2000000,
    despesasAvaliadas: 1000,
    indiceDeRisco: 5.0,
    indiceDeAltas: 1.0,
    flagsAltas: 10,
    flagsMedias: 30,
    flagsBaixas: 10,
    amostraSuficiente: true,
    coberturaDeFornecedor: 1,
    coberturaSuficiente: true,
  },
  {
    id: "p2",
    idExterno: 102,
    nomeParlamentar: "Deputada Beta",
    siglaPartido: "PT",
    uf: "RJ",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 1500000,
    despesasAvaliadas: 800,
    indiceDeRisco: 2.5,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 15,
    flagsBaixas: 5,
    amostraSuficiente: true,
    coberturaDeFornecedor: 1,
    coberturaSuficiente: true,
  },
  {
    id: "p3",
    idExterno: 103,
    nomeParlamentar: "Deputado Gama",
    siglaPartido: "PL",
    uf: "MG",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 1000000,
    despesasAvaliadas: 500,
    indiceDeRisco: 1.0,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 5,
    flagsBaixas: 0,
    amostraSuficiente: true,
    coberturaDeFornecedor: 1,
    coberturaSuficiente: true,
  },
];

describe("DashboardDaCasa", () => {
  it("renderiza KPIs executivos corretamente", () => {
    render(
      <DashboardDaCasa
        casa={CASA_CAMARA}
        itens={MOCK_ITENS}
        ordenacao={ORDENACAO_VOLUME}
      />,
    );

    // Volume total: 2M + 1.5M + 1M = 4.5M -> R$ 4.500.000,00
    expect(screen.getByText(/4\.500\.000/i)).toBeInTheDocument();

    // Despesas auditadas: 1000 + 800 + 500 = 2300
    expect(screen.getByText(/2\.300/i)).toBeInTheDocument();

    // Gasto mediano e média de 1.5M aparecem nos KPIs
    expect(screen.getAllByText(/1\.500\.000/i).length).toBeGreaterThan(0);
  });

  it("renderiza o pódio com os líderes do ranking", () => {
    render(
      <DashboardDaCasa
        casa={CASA_CAMARA}
        itens={MOCK_ITENS}
        ordenacao={ORDENACAO_VOLUME}
      />,
    );

    expect(screen.getByText("Deputado Alfa")).toBeInTheDocument();
    expect(screen.getByText("Deputada Beta")).toBeInTheDocument();
    expect(screen.getByText("Deputado Gama")).toBeInTheDocument();
  });

  it("permite filtrar por bancada ao clicar na barra do partido", async () => {
    const user = userEvent.setup();
    const mockFiltrarPartido = vi.fn();

    render(
      <DashboardDaCasa
        casa={CASA_CAMARA}
        itens={MOCK_ITENS}
        ordenacao={ORDENACAO_VOLUME}
        onFiltrarPartido={mockFiltrarPartido}
      />,
    );

    // Encontra o botão do partido PL
    const botaoPl = screen.getByRole("button", { name: /PL/i });
    await user.click(botaoPl);

    expect(mockFiltrarPartido).toHaveBeenCalledWith("PL");
  });

  it("permite filtrar por UF ao clicar no botão do estado", async () => {
    const user = userEvent.setup();
    const mockFiltrarUf = vi.fn();

    render(
      <DashboardDaCasa
        casa={CASA_CAMARA}
        itens={MOCK_ITENS}
        ordenacao={ORDENACAO_VOLUME}
        onFiltrarUf={mockFiltrarUf}
      />,
    );

    // Pela sigla no começo do nome: "com despesa", no mesmo botão, também contém "sp".
    const botaoSp = screen.getByRole("button", { name: /^SP\b/ });
    await user.click(botaoSp);

    expect(mockFiltrarUf).toHaveBeenCalledWith("SP");
  });

  it("exibe estado amigável quando itens está vazio", () => {
    render(
      <DashboardDaCasa
        casa={CASA_CAMARA}
        itens={[]}
        ordenacao={ORDENACAO_VOLUME}
      />,
    );

    expect(screen.getByText(/Base de dados em sincronização/i)).toBeInTheDocument();
  });
});
