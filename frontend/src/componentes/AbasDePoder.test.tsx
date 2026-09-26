import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { MapaDePoder, MapaDePoderCompleto } from "@/lib/contrato";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => "/estado",
  useSearchParams: () => new URLSearchParams(),
}));

const { AbasDePoder } = await import("./VistasDoEstado");

const poder = (chave: string, nome: string, total: number): MapaDePoder => ({
  chaveDoPoder: chave,
  poder: nome,
  camadas: [],
  totalDeCadeiras: total,
  vagas: 0,
  verificadoDesde: null,
  porQueFalta: total === 0 ? "A fonte não publica." : null,
});

/**
 * As abas do "Quem comanda".
 *
 * Medido em 25/09: "Judiciário 0" na aba, e "não sabemos quem ocupa" no conteúdo. Poder sem
 * cadeira é poder sem fonte — o número zero afirmaria que ninguém ocupa.
 */
describe("AbasDePoder", () => {
  const mapa: MapaDePoderCompleto = {
    poderes: [poder("executivo", "Executivo", 40), poder("judiciario", "Judiciário", 0)],
    ressalvaDaCuradoria: "",
  };

  it("com cadeiras, mostra a contagem", () => {
    render(<AbasDePoder mapa={mapa} atual="executivo" aoEscolher={() => {}} />);

    expect(screen.getByRole("tab", { name: /Executivo/ })).toHaveTextContent("40");
  });

  it("sem fonte, diz 'sem dado' e nao zero", () => {
    render(<AbasDePoder mapa={mapa} atual="executivo" aoEscolher={() => {}} />);

    const aba = screen.getByRole("tab", { name: /Judiciário/ });
    expect(aba).toHaveTextContent("sem dado");
    expect(aba).not.toHaveTextContent("0");
  });
});
