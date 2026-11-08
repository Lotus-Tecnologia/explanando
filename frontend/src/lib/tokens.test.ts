import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

/*
  Os tokens de cor medidos no próprio CSS. Se alguém trocar um valor sem recalcular, o teste
  reprova antes do navegador.
*/
const css = readFileSync(join(__dirname, "../app/globals.css"), "utf8");

function bloco(inicio: string): string {
  const i = css.indexOf(inicio);
  return css.slice(i, css.indexOf("}", i));
}

function tokens(texto: string): Record<string, string> {
  const saida: Record<string, string> = {};
  for (const m of texto.matchAll(/--color-([a-z-]+):\s*(#[0-9a-f]{6})/gi)) saida[m[1]] = m[2];
  return saida;
}

function luminancia(hex: string): number {
  const [r, g, b] = hex
    .slice(1)
    .match(/../g)!
    .map((x) => parseInt(x, 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contraste(a: string, b: string): number {
  const [x, y] = [luminancia(a), luminancia(b)].sort((p, q) => q - p);
  return (x + 0.05) / (y + 0.05);
}

const claro = tokens(bloco("@theme {"));
const escuro = tokens(bloco(':root[data-theme="dark"] {'));
const temas = { claro, escuro };

describe("tokens de cor", () => {
  it.each(Object.entries(temas))("todo par texto/fundo passa 4,5 no tema %s", (_, t) => {
    for (const texto of ["texto", "texto-suave", "titulo"]) {
      for (const fundo of ["fundo", "surface"]) {
        expect(contraste(t[texto], t[fundo]), `${texto} sobre ${fundo}`).toBeGreaterThanOrEqual(4.5);
      }
    }
    expect(contraste(t["sobre-titulo"], t.titulo)).toBeGreaterThanOrEqual(4.5);
    expect(contraste(t["texto-suave"], t.realce)).toBeGreaterThanOrEqual(4.5);
  });

  it.each(Object.entries(temas))("acento e borda estrutural passam 3 no tema %s", (_, t) => {
    for (const fundo of ["fundo", "surface"]) {
      expect(contraste(t.acento, t[fundo])).toBeGreaterThanOrEqual(3);
      expect(contraste(t["borda-estrutural"], t[fundo])).toBeGreaterThanOrEqual(3);
    }
  });

  it("a matriz de risco não mudou", () => {
    expect(claro).toMatchObject({
      "baixa-fundo": "#f8fafc",
      "baixa-texto": "#334155",
      "media-fundo": "#fef3c7",
      "media-texto": "#78350f",
      "alta-fundo": "#fee2e2",
      "alta-texto": "#991b1b",
      "abstencao-fundo": "#f1f5f9",
      "abstencao-texto": "#475569",
    });
    expect(escuro).toMatchObject({
      "baixa-fundo": "#1e293b",
      "baixa-texto": "#cbd5e1",
      "media-fundo": "#422006",
      "media-texto": "#fcd34d",
      "alta-fundo": "#450a0a",
      "alta-texto": "#fca5a5",
      "abstencao-fundo": "#1e293b",
      "abstencao-texto": "#adbcd0",
    });
  });

  it("os dois blocos escuros são iguais", () => {
    expect(tokens(bloco(":root:not([data-theme=\"light\"]) {"))).toEqual(escuro);
  });
});
