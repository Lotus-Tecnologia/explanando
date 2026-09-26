import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Paginacao } from "./Paginacao";
import type { FiltrosDeDespesas } from "@/lib/filtros";

const filtros = (pagina: number) => ({ pagina }) as FiltrosDeDespesas;

/**
 * O rodapé de paginação.
 *
 * Medido em 25/09: `?pagina=999` num perfil de 89 páginas mostrava "Página 999 de 89" e um
 * "← Anterior" que levava à 998, também vazia. Fora da faixa, quem orienta é a mensagem da
 * lista ("esta página não existe… voltar ao começo"), e o rodapé some.
 */
describe("Paginacao", () => {
  it("dentro da faixa, mostra a posicao e os dois lados", () => {
    render(<Paginacao base="/p" filtros={filtros(2)} totalDePaginas={89} />);

    expect(screen.getByText("Página 2 de 89")).toBeInTheDocument();
    expect(screen.getByText(/Anterior/)).toBeInTheDocument();
    expect(screen.getByText(/Próxima/)).toBeInTheDocument();
  });

  it("fora da faixa, nao desenha nada", () => {
    const { container } = render(<Paginacao base="/p" filtros={filtros(999)} totalDePaginas={89} />);

    expect(container).toBeEmptyDOMElement();
  });
});
