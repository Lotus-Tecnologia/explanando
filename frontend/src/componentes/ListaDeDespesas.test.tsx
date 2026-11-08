import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { FlagDaDespesa, Gravidade, ItemDeDespesa } from "@/lib/contrato";
import { LinhaDeDespesa } from "./ListaDeDespesas";

function flag(gravidade: Gravidade, codigoRegra: string): FlagDaDespesa {
  return { codigoRegra, gravidade, descricao: `Achado ${codigoRegra}.`, grauDeConfianca: 1, versaoDoMotor: 6 };
}

function despesa(ajustes: Partial<ItemDeDespesa> = {}): ItemDeDespesa {
  return {
    id: "d1",
    ano: 2026,
    mes: 7,
    tipoDespesa: "LOCAÇÃO OU FRETAMENTO DE VEÍCULOS",
    dataDocumento: "2026-07-06T00:00:00Z",
    nomeFornecedor: "LOCADORA EXEMPLO LTDA",
    documentoFornecedor: "36.826.246/0001-64",
    valorLiquido: 4500,
    valorDocumento: 4500,
    valorGlosa: null,
    temDocumentoPublicado: true,
    urlDocumento: "https://www.camara.leg.br/nota.pdf",
    ehEstorno: false,
    flags: [flag("Baixa", "REG_A")],
    maiorGravidade: "Baixa",
    respostas: [],
    ...ajustes,
  };
}

const naLista = (no: React.ReactNode) => render(<ul>{no}</ul>);

describe("LinhaDeDespesa", () => {
  it("linha fechada mostra só o essencial", () => {
    const { container } = naLista(<LinhaDeDespesa despesa={despesa()} casa="Câmara dos Deputados" />);

    const detalhes = container.querySelector("details")!;
    const resumo = container.querySelector("summary")!;
    expect(detalhes.open).toBe(false);
    expect(resumo).toHaveTextContent("Locação ou fretamento de veículos");
    expect(resumo).toHaveTextContent("LOCADORA EXEMPLO LTDA");
    expect(resumo).toHaveTextContent("R$ 4.500,00");
    expect(resumo.querySelectorAll('[role="status"]')).toHaveLength(1);
    // A nota fiscal mora no corpo, nunca no resumo.
    expect(resumo.querySelector("a")).toBeNull();
  });

  it("várias regras viram maior gravidade + contagem", () => {
    const { container } = naLista(
      <LinhaDeDespesa
        despesa={despesa({
          flags: [flag("Baixa", "REG_A"), flag("Alta", "REG_B"), flag("Media", "REG_C")],
          maiorGravidade: null,
        })}
      />,
    );

    const resumo = container.querySelector("summary")!;
    expect(resumo.querySelector('[role="status"]')).toHaveAccessibleName(/alta relevância/);
    expect(resumo).toHaveTextContent("+2");
  });

  it("o resumo abre a linha e o link da nota fica alcançável", async () => {
    const usuario = userEvent.setup();
    const { container } = naLista(<LinhaDeDespesa despesa={despesa()} casa="Câmara dos Deputados" />);

    // O jsdom não converte Enter no <summary> em clique, como o navegador faz; o teclado é
    // conferido no Chrome (Tarefa 9, passo 4).
    await usuario.click(container.querySelector("summary")!);

    expect(container.querySelector("details")!.open).toBe(true);
    expect(screen.getByRole("link", { name: /ver nota fiscal/i })).toBeInTheDocument();
  });

  it("glosa desconhecida não mostra R$ 0,00", () => {
    const { container } = naLista(<LinhaDeDespesa despesa={despesa({ valorGlosa: null })} aberta />);

    expect(container.textContent).not.toContain("R$ 0,00");
    expect(container.textContent).not.toContain("glosa");
  });

  it("senador mostra a tag inativa no lugar do botão", () => {
    naLista(<LinhaDeDespesa despesa={despesa({ urlDocumento: null })} casa="Senado Federal" aberta />);

    expect(screen.getByText("O Senado não disponibiliza o comprovante")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /ver nota fiscal/i })).not.toBeInTheDocument();
  });
});
