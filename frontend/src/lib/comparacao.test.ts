import { describe, expect, it } from "vitest";
import {
  avaliarComparacao,
  caminhoDaComparacao,
  compararComposicoes,
  mesesComDespesa,
  parCanonico,
} from "./comparacao";
import type { CoberturaDeRegra, ParcelaDoIndice, PerfilDoParlamentar } from "./contrato";

/**
 * O comparador — a Fase 2 do plano de execução.
 *
 * As três travas do plano estão aqui: o par misto é recusado, a URL invertida tem uma forma
 * canônica, e quem não tem amostra aparece como "não calculado" e não como zero.
 */

function parcela(codigo: string, quantidade: number, fatia: number): ParcelaDoIndice {
  return {
    codigo,
    descricao: `Regra ${codigo}`,
    quantidade,
    peso: quantidade,
    fatia,
    pontosNoIndice: 0,
    altas: 0,
    medias: quantidade,
    baixas: 0,
  };
}

function perfil(ajustes: Partial<PerfilDoParlamentar> = {}): PerfilDoParlamentar {
  return {
    id: "a",
    idExterno: 1,
    nomeParlamentar: "Fulana",
    nomeCivil: "Fulana de Tal",
    siglaPartido: "XYZ",
    uf: "SP",
    casa: "Câmara dos Deputados",
    fotoUrl: null,
    volumeTotalGasto: 1000,
    despesasAvaliadas: 500,
    indiceDeRisco: 2,
    indiceDeAltas: 0,
    amostraSuficiente: true,
    coberturaDeFornecedor: 1,
    coberturaSuficiente: true,
    flagsAltas: 0,
    flagsMedias: 3,
    flagsBaixas: 0,
    dataUltimaVarredura: null,
    versaoDoMotor: 4,
    serieMensal: [],
    cobertura: [],
    regrasConclusivas: 11,
    regrasSemDado: 0,
    tiposDeDespesa: [],
    composicao: [],
    fornecedoresDistintos: 10,
    ...ajustes,
  };
}

function semDado(codigo: string): CoberturaDeRegra {
  return {
    codigo,
    descricao: null,
    situacao: "SemDado",
    despesasComDado: 0,
    despesasSemDado: 500,
    flagsEmitidas: 0,
    silencioEhConclusivo: false,
    motivoDaAbstencao: "sem dado",
  };
}

describe("parCanonico", () => {
  it("a×b e b×a dão o mesmo endereço", () => {
    expect(caminhoDaComparacao("b", "a")).toBe(caminhoDaComparacao("a", "b"));
    expect(parCanonico("01a0227c", "01a0227b")).toEqual(["01a0227b", "01a0227c"]);
  });
});

describe("avaliarComparacao", () => {
  it("recusa deputado com senador, dizendo as duas casas", () => {
    const situacao = avaliarComparacao(
      perfil({ id: "a" }),
      perfil({ id: "b", casa: "Senado Federal" }),
    );

    expect(situacao).toEqual({
      tipo: "casas-diferentes",
      casaA: "Câmara dos Deputados",
      casaB: "Senado Federal",
    });
  });

  it("reconhece o mesmo parlamentar dos dois lados", () => {
    expect(avaliarComparacao(perfil(), perfil()).tipo).toBe("mesmo-parlamentar");
  });

  it("aceita dois da mesma casa", () => {
    expect(avaliarComparacao(perfil({ id: "a" }), perfil({ id: "b" })).tipo).toBe("comparavel");
  });
});

describe("compararComposicoes", () => {
  it("cada coluna fecha 100 e a regra de um lado só vira 0% do outro", () => {
    const a = perfil({
      id: "a",
      composicao: [parcela("R1", 2, 2 / 3), parcela("R2", 1, 1 / 3)],
    });
    const b = perfil({ id: "b", composicao: [parcela("R1", 5, 1)] });

    const regras = compararComposicoes(a, b);

    expect(regras.map((r) => r.codigo)).toEqual(["R1", "R2"]);
    expect(regras.reduce((soma, r) => soma + (r.a.porcentagem ?? 0), 0)).toBe(100);
    expect(regras.reduce((soma, r) => soma + (r.b.porcentagem ?? 0), 0)).toBe(100);
    expect(regras[1].b).toEqual({ quantidade: 0, porcentagem: 0, avaliada: true });
  });

  it("sem amostra, a porcentagem é nula e não zero", () => {
    const a = perfil({ id: "a", composicao: [parcela("R1", 2, 1)] });
    const b = perfil({ id: "b", indiceDeRisco: null, composicao: [parcela("R1", 1, 1)] });

    const [regra] = compararComposicoes(a, b);

    expect(regra.a.porcentagem).toBe(100);
    expect(regra.b.porcentagem).toBeNull();
    expect(regra.b.quantidade).toBe(1);
  });

  it("regra sem dado do outro lado é não avaliada, e não 0%", () => {
    const a = perfil({ id: "a", composicao: [parcela("REG_KYB_002", 2, 1)] });
    const b = perfil({ id: "b", cobertura: [semDado("REG_KYB_002")] });

    const [regra] = compararComposicoes(a, b);

    expect(regra.b).toEqual({ quantidade: 0, porcentagem: null, avaliada: false });
  });
});

describe("mesesComDespesa", () => {
  it("conta só os meses que tiveram despesa", () => {
    const serie = [
      { ano: 2026, mes: 1, totalLiquido: 10, quantidadeDeDespesas: 2, totalGlosa: 0, detalheDisponivel: true },
      { ano: 2026, mes: 2, totalLiquido: 0, quantidadeDeDespesas: 0, totalGlosa: 0, detalheDisponivel: true },
    ];

    expect(mesesComDespesa(perfil({ serieMensal: serie }))).toBe(1);
  });
});
