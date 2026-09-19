import { describe, expect, it } from "vitest";
import { espessuraDaAresta, montarAgrupamentos } from "./grafoDaRede";
import type { ParDaRede, ParlamentarNaRede } from "./contrato";

/**
 * O arranjo do mapa de fornecedores compartilhados.
 *
 * **O que estes testes protegem, antes de qualquer estética:** que o desenho seja o mesmo
 * a cada build. A página é estática e a simulação é de forças — bastaria uma posição
 * inicial sorteada para a mesma pessoa mudar de lugar sem nenhum dado ter mudado, e
 * ninguém notaria até alguém comparar duas capturas de tela.
 *
 * O resto guarda o que o desenho **afirma**: que os agrupamentos são separados de
 * verdade, que ninguém escapa do quadro, e que o forasteiro — o parlamentar de outro
 * estado, que é o caso raro — chega marcado ao componente.
 */
describe("montarAgrupamentos", () => {
  const pessoa = (id: string, uf = "PB", partido = "PP"): ParlamentarNaRede => ({
    id,
    nome: `Deputado ${id}`,
    partido,
    uf,
    casa: "Deputado",
    chaveDaCasa: "camara",
    fotoUrl: null,
    ligacoes: 2,
    vizinhos: 1,
    valorNoMapa: 100_000,
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

  it("separa os componentes que nao se tocam", () => {
    // O ponto do desenho inteiro: sao 49 ilhas, e nao um emaranhado. Se a uniao juntasse
    // componentes que nao se tocam, a tela voltaria a ser o emaranhado que o corte evita.
    const agrupamentos = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c"), pessoa("d")],
      [par("a", "b"), par("c", "d")],
    );

    expect(agrupamentos).toHaveLength(2);
    expect(agrupamentos[0].nos).toHaveLength(2);
    expect(agrupamentos[1].nos).toHaveLength(2);
  });

  it("junta tudo que esta' ligado, mesmo indiretamente", () => {
    const agrupamentos = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c")],
      [par("a", "b"), par("b", "c")],
    );

    expect(agrupamentos).toHaveLength(1);
    expect(agrupamentos[0].nos).toHaveLength(3);
  });

  it("quem nao tem par denso nao aparece", () => {
    // 239 dos 463 parlamentares do mapa estao neste caso. Desenha-los como pontos soltos
    // encheria a tela de nos sem nenhuma ligacao a mostrar.
    const agrupamentos = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("sozinho")],
      [par("a", "b")],
    );

    const ids = agrupamentos.flatMap((grupo) => grupo.nos.map((no) => no.pessoa.id));

    expect(ids).not.toContain("sozinho");
  });

  it("um par cujo parlamentar nao conhecemos nao vira no' sem nome", () => {
    const agrupamentos = montarAgrupamentos([pessoa("a")], [par("a", "fantasma")]);

    expect(agrupamentos).toHaveLength(0);
  });

  it("o desenho e' o mesmo a cada chamada", () => {
    // O teste que justifica a simulacao ser deterministica. Com posicoes iniciais
    // sorteadas, isto aqui falharia — e a pagina estatica mudaria sozinha a cada build.
    const pessoas = ["a", "b", "c", "d", "e"].map((id) => pessoa(id));
    const pares = [par("a", "b"), par("b", "c"), par("c", "d"), par("d", "e"), par("a", "e")];

    const primeiro = montarAgrupamentos(pessoas, pares);
    const segundo = montarAgrupamentos(pessoas, pares);

    expect(JSON.stringify(primeiro)).toBe(JSON.stringify(segundo));
  });

  it("nenhum no' escapa do quadro", () => {
    const pessoas = Array.from({ length: 14 }, (_, i) => pessoa(`p${i}`));
    const pares = pessoas.slice(1).map((p, i) => par(pessoas[i].id, p.id));

    const grupo = montarAgrupamentos(pessoas, pares)[0];

    for (const no of grupo.nos) {
      expect(no.x - no.raio).toBeGreaterThanOrEqual(0);
      expect(no.y - no.raio).toBeGreaterThanOrEqual(0);
      expect(no.x + no.raio).toBeLessThanOrEqual(grupo.largura);
      expect(no.y + no.raio).toBeLessThanOrEqual(grupo.altura);
    }
  });

  it("o agrupamento grande ganha um quadro LARGO, e nao um quadrado maior", () => {
    // Um quadrado do dobro do lado viraria uma faixa vertical de tela e meia com o grafo
    // perdido no meio — foi o que a primeira tentativa produziu na tela.
    const grandes = Array.from({ length: 10 }, (_, i) => pessoa(`p${i}`));
    const grande = montarAgrupamentos(
      grandes,
      grandes.slice(1).map((p, i) => par(grandes[i].id, p.id)),
    )[0];

    const pequeno = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c")],
      [par("a", "b"), par("b", "c")],
    )[0];

    expect(grande.largura).toBeGreaterThan(grande.altura);
    expect(pequeno.largura).toBe(pequeno.altura);
  });

  it("dois nos nunca ficam um em cima do outro", () => {
    // Dois retratos sobrepostos nao sao so' feios: escondem uma pessoa do mapa.
    const pessoas = Array.from({ length: 8 }, (_, i) => pessoa(`p${i}`));
    const pares = pessoas.slice(1).map((p, i) => par(pessoas[i].id, p.id));

    const grupo = montarAgrupamentos(pessoas, pares)[0];

    for (const um of grupo.nos) {
      for (const outro of grupo.nos) {
        if (um.pessoa.id >= outro.pessoa.id) continue;

        expect(Math.hypot(um.x - outro.x, um.y - outro.y)).toBeGreaterThan(um.raio);
      }
    }
  });

  it("o forasteiro vem marcado, e a maioria nao", () => {
    // 91% dos pares densos sao do mesmo estado. O de fora e' o caso raro, e um caso raro
    // que nao se destaca e' um caso que ninguem ve.
    const grupo = montarAgrupamentos(
      [pessoa("a", "PB"), pessoa("b", "PB"), pessoa("c", "SP")],
      [par("a", "b"), par("b", "c", { mesmaUf: false })],
    )[0];

    const forasteiros = grupo.nos.filter((no) => no.forasteiro).map((no) => no.pessoa.id);

    expect(forasteiros).toEqual(["c"]);
  });

  it("uma minoria grande NAO e' forasteira — so' quem esta' sozinho", () => {
    // O caso que a primeira versao errava, e que so' apareceu na tela: um agrupamento de
    // nove cearenses com cinco goianos acendia cinco aneis. Cinco pessoas de um estado nao
    // sao forasteiras, sao a outra metade do grupo.
    const cearenses = ["c1", "c2", "c3"].map((id) => pessoa(id, "CE"));
    const goianos = ["g1", "g2"].map((id) => pessoa(id, "GO"));
    const sozinho = pessoa("s1", "RR");

    const todos = [...cearenses, ...goianos, sozinho];
    const pares = todos.slice(1).map((p, i) => par(todos[i].id, p.id, { mesmaUf: false }));

    const grupo = montarAgrupamentos(todos, pares)[0];

    expect(grupo.nos.filter((no) => no.forasteiro).map((no) => no.pessoa.id)).toEqual(["s1"]);
  });

  it("num agrupamento de uma UF so', ninguem e' forasteiro", () => {
    const grupo = montarAgrupamentos(
      [pessoa("a", "PB"), pessoa("b", "PB")],
      [par("a", "b")],
    )[0];

    expect(grupo.nos.every((no) => !no.forasteiro)).toBe(true);
  });

  it("as UFs vem contadas, da mais frequente para a menos", () => {
    const grupo = montarAgrupamentos(
      [pessoa("a", "CE"), pessoa("b", "CE"), pessoa("c", "GO")],
      [par("a", "b"), par("b", "c", { mesmaUf: false })],
    )[0];

    expect(grupo.ufs).toEqual([
      { uf: "CE", quantos: 2 },
      { uf: "GO", quantos: 1 },
    ]);
  });

  it("o no' mais ligado e' o maior", () => {
    const grupo = montarAgrupamentos(
      [pessoa("centro"), pessoa("a"), pessoa("b"), pessoa("c")],
      [par("centro", "a"), par("centro", "b"), par("centro", "c")],
    )[0];

    const centro = grupo.nos.find((no) => no.pessoa.id === "centro");
    const folha = grupo.nos.find((no) => no.pessoa.id === "a");

    expect(centro?.grau).toBe(3);
    expect(folha?.grau).toBe(1);
    expect(centro!.raio).toBeGreaterThan(folha!.raio);
  });

  it("os agrupamentos vem do maior para o menor", () => {
    const agrupamentos = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c"), pessoa("d"), pessoa("e")],
      [par("a", "b"), par("c", "d"), par("d", "e")],
    );

    expect(agrupamentos[0].nos).toHaveLength(3);
    expect(agrupamentos[1].nos).toHaveLength(2);
  });

  it("a aresta guarda as pontas, o valor e se cruza estados", () => {
    const grupo = montarAgrupamentos(
      [pessoa("a", "PB"), pessoa("b", "SP")],
      [par("a", "b", { fornecedores: 4, valor: 900_000, mesmaUf: false, mesmoPartido: false })],
    )[0];

    const aresta = grupo.arestas[0];

    expect(aresta.fornecedores).toBe(4);
    expect(aresta.valor).toBe(900_000);
    expect(aresta.mesmaUf).toBe(false);
    expect(aresta.mesmoPartido).toBe(false);
    expect(aresta.x1).not.toBe(aresta.x2);
  });

  it("o valor do agrupamento e' a soma dos pares, e nao o gasto de ninguem", () => {
    const grupo = montarAgrupamentos(
      [pessoa("a"), pessoa("b"), pessoa("c")],
      [par("a", "b", { valor: 100 }), par("b", "c", { valor: 250 })],
    )[0];

    expect(grupo.valor).toBe(350);
    expect(grupo.pares).toBe(2);
  });
});

describe("espessuraDaAresta", () => {
  it("cresce com os fornecedores em comum", () => {
    expect(espessuraDaAresta(3)).toBeGreaterThan(espessuraDaAresta(2));
    expect(espessuraDaAresta(6)).toBeGreaterThan(espessuraDaAresta(3));
  });

  it("nao deixa o par extremo achatar todos os outros", () => {
    // O par extremo da base divide ONZE fornecedores. Numa escala linear ele viraria uma
    // barra atravessando o quadro, e os pares de dois e tres ficariam indistinguiveis.
    const onze = espessuraDaAresta(11);
    const dois = espessuraDaAresta(2);

    expect(onze).toBeLessThanOrEqual(5);
    expect(onze / dois).toBeLessThan(3);
  });
});
