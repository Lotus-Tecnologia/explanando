import { describe, expect, it } from "vitest";
import { caixaDeFrase, fraseDosAchadosAbaixo, fraseDosMaioresFornecedores } from "./frases";

/**
 * Frases com número, que precisam concordar com ele.
 *
 * Medido na auditoria de 25/09, no perfil de Tasso Jereissati: "Os 1 achado existe e estão
 * abaixo" e "Os 1 maiores de 1 fornecedores".
 */
describe("fraseDosAchadosAbaixo", () => {
  it("no singular, fala do unico achado", () => {
    expect(fraseDosAchadosAbaixo(1)).toBe("O único achado existe e está abaixo, com a sua regra.");
  });

  it("no plural, conta e usa o milhar brasileiro", () => {
    expect(fraseDosAchadosAbaixo(1234)).toBe("Os 1.234 achados existem e estão abaixo, regra a regra.");
  });
});

describe("fraseDosMaioresFornecedores", () => {
  it("um fornecedor so'", () => {
    expect(fraseDosMaioresFornecedores(1, 1)).toBe("O único fornecedor, pelo total pago.");
  });

  it("todos cabem na lista: nao ha' 'maiores de'", () => {
    expect(fraseDosMaioresFornecedores(4, 4)).toBe("Os 4 fornecedores, pelo total pago.");
  });

  it("parte deles", () => {
    expect(fraseDosMaioresFornecedores(10, 1500)).toBe(
      "Os 10 maiores de 1.500 fornecedores, pelo total pago.",
    );
  });

  it("um so' entre varios", () => {
    expect(fraseDosMaioresFornecedores(1, 3)).toBe("O maior de 3 fornecedores, pelo total pago.");
  });
});

describe("caixaDeFrase", () => {
  it("tira a caixa alta da fonte e o ponto final", () => {
    expect(caixaDeFrase("COMBUSTÍVEIS E LUBRIFICANTES.")).toBe("Combustíveis e lubrificantes");
    expect(caixaDeFrase("LOCAÇÃO OU FRETAMENTO DE VEÍCULOS")).toBe("Locação ou fretamento de veículos");
  });

  it("preserva as siglas", () => {
    expect(caixaDeFrase("CEAP")).toBe("CEAP");
    expect(caixaDeFrase("PASSAGEM AÉREA - SIGEPA")).toBe("Passagem aérea - SIGEPA");
  });

  it("não mexe em texto que já vem em caixa mista", () => {
    expect(caixaDeFrase("Aluguel de imóveis para escritório político")).toBe(
      "Aluguel de imóveis para escritório político",
    );
  });
});
