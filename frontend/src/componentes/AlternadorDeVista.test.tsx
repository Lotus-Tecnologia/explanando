import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { AlternadorDeVista } from "./AlternadorDeVista";

/**
 * O par de vistas.
 *
 * O que está protegido aqui é o que ele **afirma**: que a vista ativa é a página em que se
 * está, e não um estado do cliente, e que os dois destinos continuam sendo links de
 * verdade — sem JavaScript, trocar de vista tem que funcionar.
 */
describe("AlternadorDeVista", () => {
  it("leva as duas vistas, como links de verdade", () => {
    render(<AlternadorDeVista atual="instituicoes" />);

    expect(screen.getByRole("link", { name: "Instituições" })).toHaveAttribute(
      "href",
      "/estado",
    );
    expect(screen.getByRole("link", { name: "Pessoas" })).toHaveAttribute("href", "/rede");
  });

  it("marca a vista atual com aria-current=page", () => {
    // `aria-current` e nao `aria-pressed`: nao e' um liga-desliga, e' onde o leitor esta'.
    render(<AlternadorDeVista atual="pessoas" />);

    expect(screen.getByRole("link", { name: "Pessoas" })).toHaveAttribute(
      "aria-current",
      "page",
    );
    expect(screen.getByRole("link", { name: "Instituições" })).not.toHaveAttribute(
      "aria-current",
    );
  });

  it("os rotulos dizem o CONTEUDO, e nao a forma do desenho", () => {
    // "Grafo" e "Rede" descreveriam o tipo de desenho — que e' o mesmo nos dois — e nao o
    // que se vai ver. Quem chega quer saber se a tela fala de orgaos ou de gente.
    render(<AlternadorDeVista atual="instituicoes" />);

    expect(screen.queryByRole("link", { name: /grafo/i })).toBeNull();
    expect(screen.getByRole("group", { name: /vista/i })).toBeInTheDocument();
  });
});
