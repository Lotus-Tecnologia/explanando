import { afterEach, describe, expect, it, vi } from "vitest";
import { buscarMudancasDaUnidade } from "./mudancasDaUnidade";

/**
 * A ilha do mapa busca pelo próprio site, nunca pela API.
 *
 * Em produção o navegador não conhece o endereço da API (só as `NEXT_PUBLIC_` chegam a ele, e
 * a imagem não recebe `NEXT_PUBLIC_API`): pela `lib/api`, a ilha pedia a
 * `http://localhost:5199` a todo visitante. O caminho relativo é a proteção.
 */
describe("buscarMudancasDaUnidade", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("pede ao proprio site, por caminho relativo", async () => {
    const buscar = vi.fn().mockResolvedValue(new Response("[]", { status: 200 }));
    vi.stubGlobal("fetch", buscar);

    await buscarMudancasDaUnidade(26);

    expect(buscar).toHaveBeenCalledWith("/estado/26/mudancas", expect.anything());
  });

  it("devolve a lista quando o site responde", async () => {
    const mudanca = { codigoUnidade: 26, descricao: "mudou de nome" };
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response(JSON.stringify([mudanca]), { status: 200 })),
    );

    expect(await buscarMudancasDaUnidade(26)).toEqual([mudanca]);
  });

  it("falha vira nulo, e nao lista vazia", async () => {
    // Lista vazia e' "nada mudou" — uma afirmacao sobre o orgao. Falha e' sobre nos.
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("{}", { status: 502 })));
    expect(await buscarMudancasDaUnidade(26)).toBeNull();

    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("rede")));
    expect(await buscarMudancasDaUnidade(26)).toBeNull();
  });
});
