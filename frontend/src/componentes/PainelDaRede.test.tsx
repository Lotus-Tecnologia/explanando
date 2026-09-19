import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { GrafoDaRede, LegendaDaRede } from "./GrafoDaRede";
import {
  AgrupamentosDaRede,
  DuplasDaRede,
  FornecedoresDaRede,
  OQueORecorteMostra,
} from "./PainelDaRede";
import { montarAgrupamentos } from "@/lib/grafoDaRede";
import type { LigacaoDaRede, MapaDaRede, ParDaRede, ParlamentarNaRede } from "@/lib/contrato";

/**
 * O mapa de fornecedores compartilhados, na tela.
 *
 * **O que estes testes guardam é a honestidade da página, e não o seu visual.** Três
 * coisas, em ordem de importância: que as porcentagens venham do dado e não estejam
 * escritas à mão; que um fornecedor de cadastro desconhecido não apareça como se
 * soubéssemos onde fica; e que cada retrato seja um link de verdade, que funcione com o
 * JavaScript desligado.
 */

const pessoa = (id: string, parcial: Partial<ParlamentarNaRede> = {}): ParlamentarNaRede => ({
  id,
  nome: `Deputada ${id}`,
  partido: "PP",
  uf: "PB",
  casa: "Deputado",
  chaveDaCasa: "camara",
  fotoUrl: null,
  ligacoes: 2,
  vizinhos: 1,
  valorNoMapa: 100_000,
  ...parcial,
});

const par = (de: string, para: string, parcial: Partial<ParDaRede> = {}): ParDaRede => ({
  de,
  para,
  fornecedores: 2,
  valor: 150_000,
  mesmaUf: true,
  mesmoPartido: true,
  ...parcial,
});

describe("AgrupamentosDaRede", () => {
  it("desenha um quadro por agrupamento, com as UFs no cabecalho", () => {
    const agrupamentos = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c", { uf: "SP" })],
      [par("a", "b"), par("b", "c", { mesmaUf: false })],
    );

    render(<AgrupamentosDaRede agrupamentos={agrupamentos} />);

    expect(screen.getByRole("heading", { name: /PB \(2\) · SP \(1\)/ })).toBeInTheDocument();
    expect(screen.getByText(/3 parlamentares/)).toBeInTheDocument();
  });

  it("sem agrupamento nenhum, diz isso em vez de mostrar um quadro vazio", () => {
    render(<AgrupamentosDaRede agrupamentos={[]} />);

    expect(screen.getByText(/Nenhum grupo de parlamentares divide/i)).toBeInTheDocument();
  });
});

describe("GrafoDaRede", () => {
  const agrupamento = montarAgrupamentos(
    [pessoa("a", { nome: "Ana Ribeiro" }), pessoa("b", { nome: "Bruno Sales" })],
    [par("a", "b")],
  )[0];

  it("cada retrato e' um link de verdade para o perfil", () => {
    // Sem JavaScript, clicar no retrato ainda precisa abrir o perfil — e o robo de busca
    // so' enxerga a ligacao entre as paginas se ela for um href.
    const { container } = render(<GrafoDaRede agrupamento={agrupamento} />);

    const destinos = [...container.querySelectorAll("a")].map((elemento) =>
      elemento.getAttribute("href"),
    );

    expect(destinos).toEqual(["/parlamentares/a", "/parlamentares/b"]);
  });

  it("o nome fica na tela, e nao so' no hover", () => {
    // Um mapa de PESSOAS cujos nomes so' aparecem ao passar o mouse nao se le no celular
    // nem numa captura de tela.
    render(<GrafoDaRede agrupamento={agrupamento} />);

    expect(screen.getByText("Ana Ribeiro")).toBeInTheDocument();
    expect(screen.getByText("Bruno Sales")).toBeInTheDocument();
  });

  it("o desenho inteiro tem descricao para leitor de tela", () => {
    render(<GrafoDaRede agrupamento={agrupamento} />);

    expect(
      screen.getByRole("img", { name: /Agrupamento de 2 parlamentares de PB/ }),
    ).toBeInTheDocument();
  });

  it("sem retrato, aparecem as iniciais — e nao um buraco", () => {
    render(<GrafoDaRede agrupamento={agrupamento} />);

    expect(screen.getByText("AR")).toBeInTheDocument();
    expect(screen.getByText("BS")).toBeInTheDocument();
  });
});

describe("LegendaDaRede", () => {
  it("explica o traco, a espessura e o anel — nao so' a cor", () => {
    // ADR-0013: cor sozinha nao comunica. Se a legenda perder qualquer uma destas linhas,
    // um elemento do desenho vira decoracao.
    render(<LegendaDaRede />);

    expect(screen.getByText(/Mesmo estado/)).toBeInTheDocument();
    expect(screen.getByText(/Estados diferentes/)).toBeInTheDocument();
    expect(screen.getByText(/mais fornecedores em comum/)).toBeInTheDocument();
    expect(screen.getByText(/Único do seu estado no grupo/)).toBeInTheDocument();
  });
});

describe("DuplasDaRede", () => {
  it("nomeia os dois e conta os fornecedores", () => {
    const duplas = montarAgrupamentos(
      [pessoa("a", { nome: "Ana Ribeiro" }), pessoa("b", { nome: "Bruno Sales" })],
      [par("a", "b", { fornecedores: 3 })],
    );

    render(<DuplasDaRede agrupamentos={duplas} />);

    expect(screen.getByRole("link", { name: "Ana Ribeiro" })).toHaveAttribute(
      "href",
      "/parlamentares/a",
    );
    expect(screen.getByText(/3 fornecedores/)).toBeInTheDocument();
  });

  it("a dupla de estados diferentes vem marcada", () => {
    // 91% dos pares sao do mesmo estado. O par que cruza estados e' o caso raro, e passar
    // batido na lista o esconderia justamente de quem o procura.
    const duplas = montarAgrupamentos(
      [pessoa("a", { uf: "PB" }), pessoa("b", { uf: "SP" })],
      [par("a", "b", { mesmaUf: false })],
    );

    render(<DuplasDaRede agrupamentos={duplas} />);

    expect(screen.getByText("estados diferentes")).toBeInTheDocument();
  });

  it("a dupla do mesmo estado nao ganha marca nenhuma", () => {
    const duplas = montarAgrupamentos([pessoa("a"), pessoa("b")], [par("a", "b")]);

    render(<DuplasDaRede agrupamentos={duplas} />);

    expect(screen.queryByText("estados diferentes")).toBeNull();
  });
});

describe("OQueORecorteMostra", () => {
  const mapa = (pares: ParDaRede[], soltos: number): MapaDaRede => ({
    parlamentares: [],
    ligacoes: [],
    pares,
    paresComUmFornecedorSo: soltos,
    fornecedoresNaBase: 41_105,
    fornecedoresCompartilhados: 15_272,
    maiorFornecedorCompartilhado: "TAM",
    parlamentaresAtendidosPeloMaior: 500,
    ressalva: "…",
    porQueEsteCorte: "…",
    oQueADensidadeMostra: "…",
  });

  it("as porcentagens sao CALCULADAS, e nao escritas a mao", () => {
    // Os 91% e os 19% da base real sao o achado da pagina. Deixa-los como texto fixo faria
    // a tela continuar afirmando-os depois de uma ingestao que mudasse os dois.
    render(
      <OQueORecorteMostra
        mapa={mapa(
          [
            par("a", "b", { mesmaUf: true, mesmoPartido: true }),
            par("c", "d", { mesmaUf: true, mesmoPartido: false }),
            par("e", "f", { mesmaUf: true, mesmoPartido: false }),
            par("g", "h", { mesmaUf: false, mesmoPartido: false }),
          ],
          96,
        )}
      />,
    );

    expect(screen.getByText("75%")).toBeInTheDocument();
    expect(screen.getByText("25%")).toBeInTheDocument();
  });

  it("diz quantos pares ficaram de fora do desenho", () => {
    // Sem este numero, o leitor nao tem como saber que o grafo e' um recorte.
    render(<OQueORecorteMostra mapa={mapa([par("a", "b")], 1_139)} />);

    expect(screen.getByText(/de 1\.140 pares/)).toBeInTheDocument();
  });

  it("sem par nenhum, mostra um traco em vez de dividir por zero", () => {
    render(<OQueORecorteMostra mapa={mapa([], 0)} />);

    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });
});

describe("FornecedoresDaRede", () => {
  const ligacao = (parcial: Partial<LigacaoDaRede> = {}): LigacaoDaRede => ({
    documento: "12345678000199",
    fornecedor: "AGENCIA COMPARTILHADA LTDA",
    parlamentares: ["a", "b"],
    valor: 1_200_000,
    notas: 34,
    cnaePrincipal: "7311400",
    municipio: "JOÃO PESSOA",
    uf: "PB",
    abertaEm: "2019-04-02",
    ...parcial,
  });

  const pessoas = [pessoa("a", { nome: "Ana Ribeiro" }), pessoa("b", { nome: "Bruno Sales" })];

  it("traz a empresa, onde fica, o valor e quem ela atende", () => {
    render(<FornecedoresDaRede ligacoes={[ligacao()]} parlamentares={pessoas} />);

    const linha = screen.getByRole("row", { name: /AGENCIA COMPARTILHADA/ });

    expect(within(linha).getByText(/JOÃO PESSOA · PB/)).toBeInTheDocument();
    expect(within(linha).getByText(/34 notas/)).toBeInTheDocument();
    expect(within(linha).getByRole("link", { name: /Ana Ribeiro/ })).toBeInTheDocument();
  });

  it("sem cadastro do CNPJ, escreve um traco — e nao um lugar inventado", () => {
    // ADR-0007: ausencia nao e' fato. "Brasília" por omissao seria pior que o traco.
    render(
      <FornecedoresDaRede
        ligacoes={[ligacao({ municipio: null, uf: null, abertaEm: null, cnaePrincipal: null })]}
        parlamentares={pessoas}
      />,
    );

    expect(screen.getByRole("cell", { name: "—" })).toBeInTheDocument();
    expect(screen.queryByText(/aberta em/)).toBeNull();
  });

  it("o CNAE aparece como numero, e nao traduzido", () => {
    // Nao temos tabela de descricao de CNAE. Escrever "publicidade" a partir do prefixo
    // seria inventar a leitura do codigo.
    render(<FornecedoresDaRede ligacoes={[ligacao()]} parlamentares={pessoas} />);

    expect(screen.getByText(/CNAE 7311400/)).toBeInTheDocument();
  });

  it("um parlamentar que nao veio no mapa nao vira linha em branco", () => {
    render(
      <FornecedoresDaRede
        ligacoes={[ligacao({ parlamentares: ["a", "fantasma"] })]}
        parlamentares={pessoas}
      />,
    );

    expect(screen.getByRole("link", { name: /Ana Ribeiro/ })).toBeInTheDocument();
    expect(screen.getByText("—")).toBeInTheDocument();
  });
});
