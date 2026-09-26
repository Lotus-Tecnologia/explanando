import { describe, expect, it } from "vitest";
import { notaDaCobertura, porcentagemDaCobertura } from "./cobertura";

/**
 * Decisão de 26/09/2026: o índice divide o peso dos achados por todas as despesas, mas a
 * maior parte das regras só examina despesa com fornecedor identificado. Com menos da metade
 * assim, o índice descreve menos da metade do gasto — e a tela precisa dizer isso ao lado dele.
 */
describe("cobertura de fornecedor", () => {
  it("arredonda para porcentagem inteira", () => {
    expect(porcentagemDaCobertura(0.674)).toBe("67%");
    expect(porcentagemDaCobertura(1)).toBe("100%");
  });

  it("sem score, não inventa cobertura", () => {
    expect(porcentagemDaCobertura(null)).toBeNull();
    expect(notaDaCobertura(null, false)).toBeNull();
  });

  it("acima do piso, só informa", () => {
    expect(notaDaCobertura(0.9, true)).toBe("Fornecedor identificado em 90% das despesas.");
  });

  it("abaixo do piso, diz o que isso muda — sem sugerir conduta", () => {
    const nota = notaDaCobertura(0.3, false)!;

    expect(nota).toContain("30%");
    expect(nota).toMatch(/fora do ranking por índice/);
    expect(nota).not.toMatch(/suspeit|irregular|ocult/i);
  });
});
