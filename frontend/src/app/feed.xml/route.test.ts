import { afterEach, describe, expect, it, vi } from "vitest";

vi.mock("@/lib/api", () => ({ listarAchadosRecentes: vi.fn() }));

import { listarAchadosRecentes } from "@/lib/api";
import { GET } from "./route";

describe("feed geral", () => {
  afterEach(() => vi.mocked(listarAchadosRecentes).mockReset());

  it("API fora vira 503 com Retry-After, e não um feed vazio", async () => {
    vi.mocked(listarAchadosRecentes).mockRejectedValue(new TypeError("fetch failed"));
    vi.spyOn(console, "warn").mockImplementation(() => {});

    const resposta = await GET();

    expect(resposta.status).toBe(503);
    expect(resposta.headers.get("Retry-After")).toBe("300");
  });

  it("com a API no ar, devolve o RSS", async () => {
    vi.mocked(listarAchadosRecentes).mockResolvedValue([]);

    const resposta = await GET();

    expect(resposta.status).toBe(200);
    expect(await resposta.text()).toContain("<rss");
  });
});
