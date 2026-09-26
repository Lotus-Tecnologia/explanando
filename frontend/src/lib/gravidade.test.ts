import { describe, expect, it } from "vitest";
import { distribuicaoDeGravidade } from "./gravidade";
import type { ItemDoRanking } from "./contrato";

const item = (altas: number, medias: number, baixas: number) =>
  ({ flagsAltas: altas, flagsMedias: medias, flagsBaixas: baixas }) as ItemDoRanking;

/**
 * A parte de cada gravidade nos achados, calculada — nunca escrita à mão.
 *
 * A home dizia 43,3% / 53,4% / 3,3%; a base, na auditoria de 25/09, dava 41,9% / 55,1% / 3,0%.
 * Número fixo envelhece a cada varredura, e o de uma casa acabava aparecendo na outra.
 */
describe("distribuicaoDeGravidade", () => {
  it("soma as duas casas e divide pelo total de achados", () => {
    const d = distribuicaoDeGravidade([[item(1, 2, 1)], [item(0, 3, 3)]]);

    expect(d).toEqual({ alta: 0.1, media: 0.5, baixa: 0.4, total: 10 });
  });

  it("sem uma das casas, nao calcula: a parte de uma casa so' nao e' a da base", () => {
    expect(distribuicaoDeGravidade([[item(1, 1, 1)], null])).toBeNull();
  });

  it("sem achado nenhum, nulo e nao zero por cento", () => {
    // 0% seria afirmar que nada foi encontrado; sem achado, nao ha' distribuicao a mostrar.
    expect(distribuicaoDeGravidade([[item(0, 0, 0)], []])).toBeNull();
  });
});
