import { describe, expect, it } from "vitest";
import { buscarParlamentares } from "./busca";
import type { ItemDoRanking } from "./contrato";

const p = (id: string, nome: string, partido: string, uf: string, casa: string) =>
  ({ id, nomeParlamentar: nome, siglaPartido: partido, uf, casa }) as ItemDoRanking;

const CAMARA = "Câmara dos Deputados";
const SENADO = "Senado Federal";

/**
 * A busca rápida da home.
 *
 * Medido na auditoria de 25/09: "PT" devolvia 7 deputados e nenhum senador, porque a lista
 * vem com a Câmara na frente e o corte era dos 7 primeiros; e "sp" trazia Dilceu Sperafico
 * (PR) antes de deputados de SP, porque o nome casa por pedaço e a UF só por igualdade.
 */
describe("buscarParlamentares", () => {
  const deputados = Array.from({ length: 10 }, (_, i) => p(`d${i}`, `Deputado ${i}`, "PT", "PE", CAMARA));
  const senadores = [p("s1", "Humberto Costa", "PT", "PE", SENADO), p("s2", "Jaques Wagner", "PT", "BA", SENADO)];

  it("com mais resultados que o limite, as duas casas aparecem", () => {
    const achados = buscarParlamentares([...deputados, ...senadores], "PT", 7);

    expect(achados).toHaveLength(7);
    expect(achados.filter((a) => a.casa === SENADO).map((a) => a.id)).toEqual(["s1", "s2"]);
  });

  it("partido ou UF exatos vem antes de pedaco do nome", () => {
    const sperafico = p("x", "Dilceu Sperafico", "PP", "PR", CAMARA);
    const paulista = p("y", "Tabata Amaral", "PSB", "SP", CAMARA);

    expect(buscarParlamentares([sperafico, paulista], "sp", 7).map((a) => a.id)).toEqual(["y", "x"]);
  });

  it("ignora acento e caixa, e exige todos os termos", () => {
    const romario = p("r", "Romário", "PL", "RJ", SENADO);

    expect(buscarParlamentares([romario], "romario rj", 7)).toEqual([romario]);
    expect(buscarParlamentares([romario], "romario sp", 7)).toEqual([]);
  });

  it("com menos de dois caracteres, nao busca", () => {
    expect(buscarParlamentares(senadores, "p", 7)).toEqual([]);
  });

  it("deixa de fora quem foi excluido", () => {
    expect(buscarParlamentares(senadores, "pt", 7, "s1").map((a) => a.id)).toEqual(["s2"]);
  });
});
