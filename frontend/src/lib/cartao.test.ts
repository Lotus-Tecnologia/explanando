import { describe, expect, it } from "vitest";
import { textosDoCartao } from "./cartao";
import type { PerfilDoParlamentar } from "./contrato";

/**
 * O card é o que aparece no WhatsApp e no X quando alguém compartilha um perfil — muitas
 * vezes a única coisa que a pessoa vê, sem abrir a página. Por isso a ressalva do ADR-0004 vai
 * NA IMAGEM, e não só na página: o card circula sozinho.
 */
function perfil(ajustes: Partial<PerfilDoParlamentar> = {}): PerfilDoParlamentar {
  return {
    id: "01a0",
    idExterno: 1,
    nomeParlamentar: "Maria da Silva",
    nomeCivil: "MARIA DA SILVA SOUZA",
    siglaPartido: "ABC",
    uf: "SP",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 1_234_567.89,
    despesasAvaliadas: 1_500,
    indiceDeRisco: 12.3456,
    indiceDeAltas: 0.5,
    amostraSuficiente: true,
    flagsAltas: 3,
    flagsMedias: 10,
    flagsBaixas: 20,
    dataUltimaVarredura: "2026-09-25T00:00:00Z",
    versaoDoMotor: 5,
    serieMensal: [],
    cobertura: [],
    regrasConclusivas: 10,
    regrasSemDado: 2,
    tiposDeDespesa: [],
    ...ajustes,
  } as PerfilDoParlamentar;
}

describe("textosDoCartao", () => {
  it("traz quem é, quanto gastou e o índice, formatados", () => {
    const textos = textosDoCartao(perfil());

    expect(textos.nome).toBe("Maria da Silva");
    expect(textos.legenda).toBe("ABC · SP · Câmara dos Deputados");
    expect(textos.volume).toContain("1.234.567,89");
    expect(textos.volumeLegenda).toBe("em 1.500 despesas da cota parlamentar");
    expect(textos.indice).toBe("12,35");
  });

  it("abaixo do piso, o índice sai como não calculado — nunca como zero", () => {
    // ADR-0009: abaixo de 100 despesas não há índice, e zero diria "medido e nada achado".
    const textos = textosDoCartao(perfil({ indiceDeRisco: null, amostraSuficiente: false, despesasAvaliadas: 40 }));

    expect(textos.indice).toBeNull();
    expect(textos.indiceLegenda).toMatch(/não calculado/);
    expect(textos.indiceLegenda).toMatch(/menos de 100 despesas/);
  });

  it("a ressalva vai sempre na imagem", () => {
    for (const p of [perfil(), perfil({ indiceDeRisco: null, amostraSuficiente: false })]) {
      expect(textosDoCartao(p).ressalva).toMatch(/não são juízo de conduta/i);
    }
  });

  it("conta os achados de alta relevância no singular e no plural", () => {
    expect(textosDoCartao(perfil({ flagsAltas: 1 })).altasLegenda).toBe("achado de alta relevância");
    expect(textosDoCartao(perfil({ flagsAltas: 0 })).altasLegenda).toBe("achados de alta relevância");
  });

  it("nenhum texto do card imputa conduta", () => {
    const acusatorio = /fraud|crime|corrup|suspeit|irregular|desvi|propina|ilegal|lavagem/i;

    for (const p of [perfil(), perfil({ indiceDeRisco: null, amostraSuficiente: false })]) {
      for (const texto of Object.values(textosDoCartao(p))) {
        if (typeof texto === "string") {
          expect(texto).not.toMatch(acusatorio);
        }
      }
    }
  });
});
