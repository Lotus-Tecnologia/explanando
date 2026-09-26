import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { CoberturaDeRegra, ParcelaDoIndice, RespostaDoGabinete } from "@/lib/contrato";
import { porcentagensInteiras } from "@/lib/formato";
import { ComposicaoDoIndice } from "./ComposicaoDoIndice";
import { RespostasDoGabinete, SemRespostaDoGabinete } from "./RespostasDoGabinete";

/**
 * A Fase 1 do plano de execução: "índice 4,10 — e é daqui que ele vem".
 *
 * O que se protege aqui é a promessa aritmética (as porcentagens fecham 100) e as duas
 * ausências que não podem virar caixa vazia: sem amostra e sem achado.
 */

function parcela(ajustes: Partial<ParcelaDoIndice> = {}): ParcelaDoIndice {
  return {
    codigo: "REG_KYB_001",
    descricao: "A atividade registrada da empresa não corresponde ao tipo do gasto.",
    quantidade: 29,
    peso: 58,
    fatia: 58 / 118,
    pontosNoIndice: 2.015,
    altas: 0,
    medias: 29,
    baixas: 0,
    ...ajustes,
  };
}

// As seis regras do deputado de maior volume na base local — índice 4,10.
const POMPEO: ParcelaDoIndice[] = [
  parcela(),
  parcela({ codigo: "REG_KYB_005", descricao: "Notas com numeração inicial.", quantidade: 14, peso: 28, fatia: 28 / 118, medias: 14 }),
  parcela({ codigo: "REG_TX_003", descricao: "Mesmo valor repetido.", quantidade: 19, peso: 19, fatia: 19 / 118, medias: 0, baixas: 19 }),
  parcela({ codigo: "REG_TX_004", descricao: "Valor acima da média.", quantidade: 5, peso: 10, fatia: 10 / 118, medias: 5 }),
  parcela({ codigo: "REG_TX_001", descricao: "Várias notas no mês.", quantidade: 1, peso: 2, fatia: 2 / 118, medias: 1 }),
  parcela({ codigo: "REG_GEO_002", descricao: "Gasto fora do estado.", quantidade: 1, peso: 1, fatia: 1 / 118, medias: 0, baixas: 1 }),
];

function regra(ajustes: Partial<CoberturaDeRegra> = {}): CoberturaDeRegra {
  return {
    codigo: "REG_TX_002",
    descricao: "O gasto do mês ficou perto do limite mensal da cota do estado.",
    situacao: "SemDado",
    despesasComDado: 0,
    despesasSemDado: 2878,
    flagsEmitidas: 0,
    silencioEhConclusivo: false,
    motivoDaAbstencao: "Nenhuma despesa pôde ser avaliada: não há teto de cota publicado.",
    ...ajustes,
  };
}

describe("porcentagensInteiras", () => {
  it("fecha 100 onde arredondar cada uma daria 99", () => {
    expect(porcentagensInteiras([1 / 3, 1 / 3, 1 / 3])).toEqual([34, 33, 33]);
  });

  it("fecha 100 nas fatias reais do índice", () => {
    const soma = porcentagensInteiras(POMPEO.map((p) => p.fatia)).reduce((a, b) => a + b, 0);
    expect(soma).toBe(100);
  });

  it("dá o ponto que sobra para a maior sobra, não para a primeira da lista", () => {
    expect(porcentagensInteiras([0.101, 0.899])).toEqual([10, 90]);
    expect(porcentagensInteiras([0.105, 0.895])).toEqual([11, 89]);
  });

  it("sem nada a distribuir, devolve zeros e não NaN", () => {
    expect(porcentagensInteiras([0, 0])).toEqual([0, 0]);
    expect(porcentagensInteiras([])).toEqual([]);
  });
});

describe("ComposicaoDoIndice", () => {
  it("diz de onde vem o índice, com as porcentagens fechando 100", () => {
    render(
      <ComposicaoDoIndice composicao={POMPEO} indice={4.1} despesasAvaliadas={2878} cobertura={[]} />,
    );

    const lista = screen.getByRole("list", { name: "Regras que compõem o índice" });
    const porcentagens = within(lista)
      .getAllByText(/^\d+%$/)
      .map((elemento) => Number(elemento.textContent!.replace("%", "")));

    expect(porcentagens.reduce((a, b) => a + b, 0)).toBe(100);
    expect(porcentagens[0]).toBe(49);
    expect(screen.getByText(/O índice de/)).toHaveTextContent("4,10");
  });

  it("explica o peso, para o leitor não achar que é contagem", () => {
    render(
      <ComposicaoDoIndice composicao={POMPEO} indice={4.1} despesasAvaliadas={2878} cobertura={[]} />,
    );

    expect(screen.getByText(/alta vale 3, média 2, baixa 1/)).toBeInTheDocument();
  });

  it("sem amostra, explica a ausência e não mostra porcentagem", () => {
    render(
      <ComposicaoDoIndice
        composicao={[parcela({ quantidade: 2, peso: 4, fatia: 1 })]}
        indice={null}
        despesasAvaliadas={12}
        cobertura={[]}
      />,
    );

    expect(screen.getByText(/não é publicado/)).toBeInTheDocument();
    expect(screen.getByText(/abaixo do piso de/)).toBeInTheDocument();
    expect(screen.queryByText(/^\d+%$/)).not.toBeInTheDocument();
    // Os achados continuam ali: o que falta é o índice, não o que foi apontado.
    expect(screen.getByText(/Os 2 achados existem e estão abaixo/)).toBeInTheDocument();
  });

  it("sem achado, diz que o índice é zero e que isso não é atestado", () => {
    render(<ComposicaoDoIndice composicao={[]} indice={0} despesasAvaliadas={900} cobertura={[]} />);

    expect(screen.getByText(/o índice é zero/)).toBeInTheDocument();
    expect(screen.getByText("Isso não é atestado de regularidade")).toBeInTheDocument();
  });

  it("lista à parte a regra que não avaliou nada, como não avaliada", () => {
    render(
      <ComposicaoDoIndice
        composicao={POMPEO}
        indice={4.1}
        despesasAvaliadas={2878}
        cobertura={[regra(), regra({ codigo: "REG_TX_001", silencioEhConclusivo: true, despesasComDado: 2878 })]}
      />,
    );

    expect(screen.getByText(/Uma regra ficou fora da conta: não pôde ser aplicada a nenhuma despesa/)).toBeInTheDocument();
    expect(screen.getByText(/perto do limite mensal/)).toBeInTheDocument();
    expect(screen.getByRole("status", { name: /Não avaliado/ })).toBeInTheDocument();
  });
});

describe("RespostasDoGabinete", () => {
  const resposta: RespostaDoGabinete = {
    texto: "O serviço foi prestado; a nota complementar está em anexo.",
    urlDoAnexo: "https://exemplo.invalido/anexo.pdf",
    enviadaEm: "2026-08-01T12:00:00Z",
  };

  it("mostra a resposta com a origem declarada e o anexo", () => {
    render(<RespostasDoGabinete respostas={[resposta]} />);

    expect(screen.getByText(/Resposta do gabinete/)).toBeInTheDocument();
    expect(screen.getByText(/O serviço foi prestado/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /anexo/ })).toHaveAttribute(
      "href",
      "https://exemplo.invalido/anexo.pdf",
    );
  });

  it("compacta, na lista, não oferece o anexo", () => {
    render(<RespostasDoGabinete respostas={[resposta]} compacta />);

    expect(screen.queryByRole("link", { name: /anexo/ })).not.toBeInTheDocument();
  });

  it("sem resposta não desenha nada", () => {
    const { container } = render(<RespostasDoGabinete respostas={[]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("o lugar vazio diz como o gabinete responde, e não que ele se calou", () => {
    render(<SemRespostaDoGabinete />);

    expect(screen.getByText(/Não há resposta do gabinete/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Como o gabinete responde" })).toHaveAttribute(
      "href",
      "/privacidade#contestacao",
    );
  });
});

describe("ComposicaoDoIndice, contrato incompleto", () => {
  it("índice positivo sem parcela não vira 'o índice é zero'", () => {
    const { container } = render(
      <ComposicaoDoIndice composicao={[]} indice={4.1} despesasAvaliadas={2878} cobertura={[]} />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it("uma gravidade só não repete a contagem entre parênteses", () => {
    render(
      <ComposicaoDoIndice composicao={[parcela()]} indice={2.02} despesasAvaliadas={2878} cobertura={[]} />,
    );

    expect(screen.getByText(/29 achados de média relevância/)).toBeInTheDocument();
  });
});
