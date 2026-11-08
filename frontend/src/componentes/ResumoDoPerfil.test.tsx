import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { CoberturaDeRegra, PerfilDoParlamentar } from "@/lib/contrato";
import { ResumoDoPerfil, fraseDosAchados } from "./ResumoDoPerfil";

function regras(conclusivas: number, semDado: number): CoberturaDeRegra[] {
  return Array.from({ length: conclusivas + semDado }, (_, i) => ({
    codigo: `REG_${i}`,
    descricao: `Regra ${i}`,
    flagsEmitidas: 0,
    despesasSemDado: i < conclusivas ? 0 : 10,
    silencioEhConclusivo: i < conclusivas,
    motivoDaAbstencao: i < conclusivas ? null : "sem dado público",
  })) as CoberturaDeRegra[];
}

function perfil(ajustes: Partial<PerfilDoParlamentar> = {}): PerfilDoParlamentar {
  return {
    id: "p1",
    idExterno: 1,
    nomeParlamentar: "Maria Exemplo",
    nomeCivil: "Maria Exemplo da Silva",
    siglaPartido: "PSD",
    uf: "AM",
    casa: "Senado Federal",
    fotoUrl: null,
    volumeTotalGasto: 1000,
    despesasAvaliadas: 40,
    indiceDeRisco: null,
    indiceDeAltas: null,
    amostraSuficiente: false,
    coberturaDeFornecedor: null,
    coberturaSuficiente: false,
    flagsAltas: 0,
    flagsMedias: 0,
    flagsBaixas: 0,
    dataUltimaVarredura: null,
    versaoDoMotor: 6,
    serieMensal: [],
    cobertura: regras(7, 5),
    regrasConclusivas: 7,
    regrasSemDado: 5,
    tiposDeDespesa: [],
    composicao: [],
    ...ajustes,
  } as PerfilDoParlamentar;
}

describe("ResumoDoPerfil", () => {
  it("sem foto, o retrato mostra iniciais", () => {
    render(<ResumoDoPerfil perfil={perfil()} />);

    expect(screen.getByText("ME")).toBeInTheDocument();
  });

  it("zero achados com regra abstida diz quantas regras foram aplicadas", () => {
    const frase = fraseDosAchados(perfil());

    expect(frase).toContain("Nenhum achado nas 7 regras que puderam ser aplicadas");
    expect(frase).toContain("5 de 12 sem dado público");
    expect(frase).not.toMatch(/nenhum indicador/i);
  });

  it("com achados mas sem alta, diz que não há alta", () => {
    expect(fraseDosAchados(perfil({ flagsMedias: 49, flagsBaixas: 20 }))).toContain(
      "Nenhum achado de gravidade alta",
    );
  });

  it("o resumo cabe na primeira tela: retrato, nome, partido e os dois números, nessa ordem", () => {
    const { container } = render(<ResumoDoPerfil perfil={perfil({ flagsMedias: 3 })} />);

    const texto = container.textContent ?? "";
    const ordem = ["ME", "Maria Exemplo", "PSD, AM", "Gastou da cota", "O que o motor encontrou"].map(
      (trecho) => texto.indexOf(trecho),
    );
    expect(ordem.every((posicao) => posicao >= 0)).toBe(true);
    expect([...ordem].sort((a, b) => a - b)).toEqual(ordem);
  });
});
