import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SerieDePessoal } from "./SerieDePessoal";
import type { PontoDaSerie } from "@/lib/contrato";

/**
 * A série do quadro de pessoal.
 *
 * O que está sendo protegido aqui não é o desenho — é **o que o desenho afirma**. A série
 * real tem um buraco de nove meses, porque a origem passou a recusar downloads no meio de
 * uma carga. Uma linha cheia atravessando esse buraco diria que houve medição mensal onde
 * não houve.
 */
describe("SerieDePessoal", () => {
  const ponto = (competencia: string, vinculos: number): PontoDaSerie => ({
    competencia,
    vinculos,
    comFuncaoDeConfianca: Math.round(vinculos * 0.18),
  });

  const contigua = [
    ponto("2025-01", 725_394),
    ponto("2025-02", 724_794),
    ponto("2025-03", 732_844),
  ];

  const comVao = [...contigua, ponto("2026-07", 759_351)];

  function desenhar(serie: PontoDaSerie[]) {
    const { container } = render(
      <SerieDePessoal serie={serie} titulo="Teste" explicacao="Explicação" />,
    );

    return container;
  }

  it("nao desenha nada com menos de dois pontos", () => {
    // Um ponto so' num grafico de linha e' um grafico que afirma uma tendencia inexistente.
    expect(desenhar([ponto("2026-07", 759_351)]).querySelector("svg")).toBeNull();
    expect(desenhar([]).querySelector("svg")).toBeNull();
  });

  it("serie contigua nao tem pontilhado nem aviso de vao", () => {
    const container = desenhar(contigua);

    expect(container.querySelectorAll("line[stroke-dasharray]")).toHaveLength(0);
    expect(screen.queryByText(/meses não coletados/)).toBeNull();
  });

  it("o vao vira pontilhado, e a legenda diz o que ele e'", () => {
    const container = desenhar(comVao);

    expect(container.querySelectorAll("line[stroke-dasharray]")).toHaveLength(1);
    expect(screen.getByText(/pontilhado = meses não coletados/)).toBeInTheDocument();
  });

  it("a linha cheia NAO atravessa o vao", () => {
    // O defeito que este teste impede: uma polilinha unica ligando 2025-03 a 2026-07 como
    // se os nove meses do meio tivessem sido medidos.
    const container = desenhar(comVao);
    const polilinhas = [...container.querySelectorAll("polyline")];

    // Um trecho contiguo de tres pontos, e o quarto ponto isolado — que nao vira linha.
    expect(polilinhas).toHaveLength(1);
    expect(polilinhas[0].getAttribute("points")?.split(" ")).toHaveLength(3);
  });

  it("o eixo posiciona por data, nao por indice", () => {
    // Com espacamento por indice, o salto de nove meses teria a mesma largura de um mes.
    const container = desenhar(comVao);
    const pontilhado = container.querySelector("line[stroke-dasharray]")!;

    const x1 = Number(pontilhado.getAttribute("x1"));
    const x2 = Number(pontilhado.getAttribute("x2"));

    // O vao de 2025-03 a 2026-07 sao 16 dos 18 meses da serie: quase toda a largura.
    expect((x2 - x1) / 600).toBeGreaterThan(0.8);
  });

  it("a serie inteira existe em texto, e nao so' no desenho", () => {
    // Tooltip que e' a unica forma de ler um numero e' defeito de acessibilidade. A tabela
    // e' o par limpo do grafico.
    desenhar(comVao);

    // Dentro da TABELA: a competencia tambem aparece no numero em destaque e no eixo, e
    // consultar a pagina toda encontraria aquelas em vez destas.
    const tabela = within(screen.getByRole("table"));

    // No formato de leitor, "01/2025", e não o do banco — o mesmo da tabela do perfil.
    for (const p of comVao) {
      const [ano, mes] = p.competencia.split("-");
      expect(tabela.getByText(`${mes}/${ano}`)).toBeInTheDocument();
    }
  });

  it("o rotulo acessivel avisa do vao", () => {
    desenhar(comVao);

    expect(screen.getByRole("img").getAttribute("aria-label")).toMatch(/não coletados/);
  });

  it("a variacao e' calculada entre a primeira e a ultima competencia", () => {
    desenhar(comVao);

    // 759.351 − 725.394 = 33.957
    expect(screen.getByText(/33\.957/)).toBeInTheDocument();
  });
});
