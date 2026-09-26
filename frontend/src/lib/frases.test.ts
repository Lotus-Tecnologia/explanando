import { describe, expect, it } from "vitest";
import { fraseDosAchadosAbaixo, fraseDosMaioresFornecedores } from "./frases";

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
