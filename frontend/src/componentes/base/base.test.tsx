import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Botao } from "./Botao";
import { Campo } from "./Campo";
import { TabelaDeDados } from "./TabelaDeDados";

describe("componentes base", () => {
  it("botão externo avisa que abre nova aba", () => {
    render(
      <Botao href="https://exemplo.gov.br/nota.pdf" externo>
        Ver nota fiscal
      </Botao>,
    );

    const link = screen.getByRole("link", { name: /ver nota fiscal \(abre em nova aba\)/i });
    expect(link).toHaveAttribute("target", "_blank");
    expect(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("campo de busca tem rótulo acessível mesmo sem rótulo visível", () => {
    render(<Campo id="busca" rotulo="Buscar parlamentar" rotuloVisivel={false} tipo="busca" />);

    expect(screen.getByRole("searchbox", { name: "Buscar parlamentar" })).toBeInTheDocument();
  });

  it("tabela rola no próprio contêiner", () => {
    render(
      <TabelaDeDados legenda="Ranking da Câmara" colunas={[{ rotulo: "Nome" }]}>
        <tr>
          <td>Fulano</td>
        </tr>
      </TabelaDeDados>,
    );

    const regiao = screen.getByRole("region", { name: "Ranking da Câmara" });
    expect(regiao.className).toContain("overflow-x-auto");
    expect(screen.getByRole("table")).toBeInTheDocument();
  });
});
