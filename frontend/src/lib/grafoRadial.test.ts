import { describe, expect, it } from "vitest";
import {
  RAIO_DO_MIOLO,
  arcoDoRotulo,
  caminhoDaForma,
  caminhoDoSetor,
  caminhoDoTalo,
  fonteDoRotuloDoSetor,
  formaDoNo,
  giroPara,
  montarGrafo,
} from "./grafoRadial";
import type { NoDoEstado } from "./contrato";

/**
 * O layout do grafo do Estado.
 *
 * O que está protegido aqui é **o que o desenho afirma**: que o ângulo é o poder, que o
 * raio é a natureza jurídica, e que nenhum nó some do mapa por conveniência de desenho.
 */
describe("montarGrafo", () => {
  const no = (
    codigo: number,
    codigoPai: number | null,
    chaveDoTipo: string,
    anel: number,
    chaveDoPoder: string | null,
    nome = `Unidade ${codigo}`,
  ): NoDoEstado => ({
    codigo,
    codigoPai,
    codigoPaiNoMapa: null,
    nome,
    sigla: null,
    tipo: chaveDoTipo,
    poder: chaveDoPoder,
    chaveDoTipo,
    chaveDoPoder,
    anel,

    // O rotulo vem do dominio; aqui basta ser distinto por anel, porque o que os testes
    // checam e' que o desenho leia o rotulo do anel certo.
    rotuloDoAnel: `Anel ${anel}`,

    natureza: null,
    baseLegal: null,
    unidadesSubordinadas: 0,
    ativa: true,
    servidores: null,
    emFuncaoDeConfianca: null,
    servidoresNoRamo: null,
  });

  /** A forma real da base: raiz → União → quatro poderes → órgãos → entidades. */
  const base: NoDoEstado[] = [
    no(200000, null, "ente", 0, null, "República Federativa do Brasil"),
    no(200001, 200000, "ente", 0, null, "União"),
    no(1, 200001, "ente", 0, "executivo", "Poder Executivo"),
    no(2, 200001, "ente", 0, "judiciario", "Poder Judiciário"),
    no(3, 200001, "ente", 0, "legislativo", "Poder Legislativo"),
    no(10, 1, "orgao", 1, "executivo", "Presidência"),
    no(11, 1, "orgao", 1, "executivo", "Ministério A"),
    no(12, 10, "entidade", 2, "executivo", "Autarquia B"),
    no(20, 2, "orgao", 1, "judiciario", "Tribunal X"),
    no(30, 3, "orgao", 1, "legislativo", "Senado"),
  ];

  it("o miolo absorve a corrente de no' unico que sai da raiz", () => {
    // Medido: a raiz tem um filho so' (a Uniao), que ramifica nos quatro poderes. Dois
    // aneis com um no' cada empurrariam o resto para longe do centro sem dizer nada.
    const grafo = montarGrafo(base);

    expect(grafo.miolo.map((n) => n.codigo)).toEqual([200000, 200001]);
  });

  it("a regra do miolo e' generica: raiz que ramifica nao absorve nada", () => {
    // Se o escopo mudar e a raiz passar a ter dois filhos, o miolo precisa encolher
    // sozinho — e nao continuar engolindo por causa de um codigo fixo.
    const ramifica = base.filter((n) => n.codigo !== 200001).map((n) =>
      n.codigoPai === 200001 ? { ...n, codigoPai: 200000 } : n,
    );

    expect(montarGrafo(ramifica).miolo.map((n) => n.codigo)).toEqual([200000]);
  });

  it("nenhum no' desaparece: miolo mais desenhados e' o total", () => {
    // O defeito que este teste impede e' o pior de um mapa: um orgao existir na base e
    // nao existir no desenho, sem nada dizendo isso.
    const grafo = montarGrafo(base);

    expect(grafo.miolo.length + grafo.nos.length).toBe(base.length);
  });

  it("cada poder vira um setor, e os setores nao se sobrepoem", () => {
    const grafo = montarGrafo(base);

    expect(grafo.setores.map((s) => s.chave).sort()).toEqual([
      "executivo",
      "judiciario",
      "legislativo",
    ]);

    for (let i = 1; i < grafo.setores.length; i++) {
      expect(grafo.setores[i].inicio).toBeGreaterThanOrEqual(grafo.setores[i - 1].fim);
    }

    const ultimo = grafo.setores[grafo.setores.length - 1];
    expect(ultimo.fim).toBeLessThanOrEqual(Math.PI * 2 + 0.001);
  });

  it("o poder pequeno tem piso de angulo, para continuar clicavel", () => {
    // Medido na base real: 266 dos 336 nos sao do Executivo e 4 do Legislativo. Numa
    // divisao puramente proporcional o Legislativo ficaria com 4 graus.
    const grafo = montarGrafo(base);
    const legislativo = grafo.setores.find((s) => s.chave === "legislativo")!;

    expect(legislativo.fim - legislativo.inicio).toBeGreaterThanOrEqual(
      (30 * Math.PI) / 180 - 0.001,
    );
  });

  it("o Executivo ainda domina: o piso nao apaga a desproporcao", () => {
    // O piso existe para o pequeno ser clicavel, nao para fingir equilibrio. Se ele
    // igualasse os setores, o desenho mentiria sobre o tamanho do Executivo.
    const muitos = [
      ...base,
      ...Array.from({ length: 40 }, (_, i) => no(500 + i, 1, "entidade", 2, "executivo")),
    ];

    const grafo = montarGrafo(muitos);
    const exec = grafo.setores.find((s) => s.chave === "executivo")!;
    const legis = grafo.setores.find((s) => s.chave === "legislativo")!;

    expect(exec.fim - exec.inicio).toBeGreaterThan((legis.fim - legis.inicio) * 3);
  });

  it("o raio e' a natureza juridica, e nao a profundidade na arvore", () => {
    // A correcao mais importante do desenho. Uma entidade ligada DIRETO ao poder tem a
    // mesma profundidade de um orgao, e precisa ficar no anel de fora mesmo assim.
    const direta = [...base, no(99, 1, "entidade", 2, "executivo", "Autarquia direta")];
    const grafo = montarGrafo(direta);

    const orgao = grafo.nos.find((p) => p.no.codigo === 11)!;
    const entidade = grafo.nos.find((p) => p.no.codigo === 99)!;

    expect(entidade.raio).toBeGreaterThan(orgao.raio);
  });

  it("todo no' fica dentro do setor do proprio poder", () => {
    const grafo = montarGrafo(base);

    for (const posicionado of grafo.nos) {
      const setor = grafo.setores.find((s) => s.chave === posicionado.no.chaveDoPoder)!;

      expect(posicionado.angulo).toBeGreaterThan(setor.inicio);
      expect(posicionado.angulo).toBeLessThan(setor.fim);
    }
  });

  it("os nos de um anel nao se encostam na borda do setor", () => {
    // Encostar faria o primeiro e o ultimo parecerem do setor vizinho.
    const grafo = montarGrafo(base);
    const setor = grafo.setores.find((s) => s.chave === "executivo")!;
    const doSetor = grafo.nos.filter((p) => p.no.chaveDoPoder === "executivo" && p.anel === 1);

    for (const p of doSetor) {
      expect(p.angulo - setor.inicio).toBeGreaterThan(0.001);
      expect(setor.fim - p.angulo).toBeGreaterThan(0.001);
    }
  });

  it("toda aresta liga dois nos que existem no desenho", () => {
    // Aresta para o vazio e' uma linha que sai do nada — e o miolo conta como destino.
    const grafo = montarGrafo(base);
    const existentes = new Set([
      ...grafo.miolo.map((n) => n.codigo),
      ...grafo.nos.map((p) => p.no.codigo),
    ]);

    for (const aresta of grafo.arestas) {
      expect(existentes.has(aresta.de)).toBe(true);
      expect(existentes.has(aresta.para)).toBe(true);
    }
  });

  it("base vazia nao quebra", () => {
    const grafo = montarGrafo([]);

    expect(grafo.nos).toEqual([]);
    expect(grafo.miolo).toEqual([]);
  });

  it("o miolo comeca fora do raio dos aneis", () => {
    const grafo = montarGrafo(base);

    for (const p of grafo.nos) {
      expect(p.raio).toBeGreaterThan(RAIO_DO_MIOLO);
    }
  });
});

describe("formaDoNo", () => {
  it("cada tipo tem forma propria — ADR-0013, forma e nao so' cor", () => {
    const formas = ["ente", "orgao", "entidade", "colegiada"].map(formaDoNo);

    expect(new Set(formas).size).toBe(4);
  });

  it("tipo desconhecido tem forma, e nao vira invisivel", () => {
    expect(formaDoNo("coisa-nova")).toBeTruthy();
  });
});

describe("caminhoDaForma", () => {
  it("toda forma produz um caminho fechado", () => {
    for (const forma of ["circulo", "quadrado", "hexagono", "losango"]) {
      const d = caminhoDaForma(forma, 10);

      expect(d.startsWith("M")).toBe(true);
      expect(d.length).toBeGreaterThan(10);
    }
  });
});

describe("giroPara", () => {
  it("poe o no' seleccionado embaixo, no centro", () => {
    // Embaixo sempre cabe: no topo o rotulo cai no cabecalho, na esquerda sai da tela.
    // Angulo 0 e' meio-dia, entao o giro precisa ser meia volta.
    expect(giroPara(0)).toBeCloseTo(Math.PI, 6);

    // Um no' que ja' esta' embaixo nao gira.
    expect(giroPara(Math.PI)).toBeCloseTo(0, 6);
  });
});

/**
 * A moldura do desenho: as cunhas de poder, a régua dos anéis e o talo.
 *
 * **Estes testes nasceram de erros que só a tela pegou.** O rótulo do Legislativo saiu
 * espelhado; o de "Funções essenciais à Justiça" atravessou por cima de dois setores
 * vizinhos; e os talos do anel externo viraram uma faixa hachurada. Nenhum dos três
 * quebrava nada — o desenho continuava aparecendo, só que errado.
 */
describe("arcoDoRotulo", () => {
  const meiaVolta = Math.PI;

  it("no alto do disco, o texto corre no sentido horario", () => {
    // Setor em volta do meio-dia. Horario = da esquerda para a direita, que e' como se le.
    const d = arcoDoRotulo(-0.4, 0.4, 100);

    // O ultimo parametro antes das coordenadas finais e' o sentido do arco: 1 e' horario.
    expect(d).toMatch(/A 100 100 0 0 1 /);
  });

  it("embaixo do disco, o texto corre ao contrario — senao sai de ponta-cabeca", () => {
    const d = arcoDoRotulo(meiaVolta - 0.4, meiaVolta + 0.4, 100);

    expect(d).toMatch(/A 100 100 0 0 0 /);
  });

  it("na metade ESQUERDA do alto, NAO inverte", () => {
    // O erro real: a primeira versao testava a metade esquerda em vez da de baixo, e
    // escrevia "LEGISLATIVO" espelhado num setor que esta' em cima.
    const d = arcoDoRotulo(5.4, 6.1, 100);

    expect(d).toMatch(/A 100 100 0 0 1 /);
  });

  it("na metade ESQUERDA de baixo, inverte", () => {
    const d = arcoDoRotulo(3.6, 4.3, 100);

    expect(d).toMatch(/A 100 100 0 0 0 /);
  });
});

describe("fonteDoRotuloDoSetor", () => {
  it("nome curto em setor largo fica no corpo cheio", () => {
    const medida = fonteDoRotuloDoSetor("EXECUTIVO", 0, Math.PI, 380);

    expect(medida?.fonte).toBe(13);
  });

  it("nome comprido em setor de 30 graus ENCOLHE, em vez de invadir o vizinho", () => {
    // O caso real: "FUNÇÕES ESSENCIAIS À JUSTIÇA" media 285 pixels de texto para 152 de
    // arco, e passava por cima do Judiciario e do Legislativo.
    const trintaGraus = (30 * Math.PI) / 180;
    const medida = fonteDoRotuloDoSetor("FUNÇÕES ESSENCIAIS À JUSTIÇA", 0, trintaGraus, 380);

    expect(medida).not.toBeNull();
    expect(medida!.fonte).toBeLessThan(13);

    // E o texto tem que caber mesmo: e' a conta que o componente confia.
    expect(medida!.fonte * 28 * 0.62).toBeLessThanOrEqual(trintaGraus * 380 + 0.01);
  });

  it("quando nem encolhido cabe, o rotulo some do desenho", () => {
    // Sumir e' melhor que espremer ate' ficar ilegivel: o nome continua na legenda.
    expect(fonteDoRotuloDoSetor("FUNÇÕES ESSENCIAIS À JUSTIÇA", 0, 0.08, 100)).toBeNull();
  });

  it("o espacamento entre letras e' a primeira coisa a ir embora", () => {
    const largo = fonteDoRotuloDoSetor("EXECUTIVO", 0, Math.PI, 380);
    const apertado = fonteDoRotuloDoSetor("EXECUTIVO", 0, 0.2, 380);

    expect(largo!.espaco).toBeGreaterThan(apertado!.espaco);
  });
});

describe("caminhoDoSetor", () => {
  it("um setor de mais de meia volta usa o flag de arco maior", () => {
    // Sem ele o SVG desenha o caminho curto, e a cunha do Executivo vira o complemento
    // dela mesma — o desenho fica com o buraco no lugar da tinta.
    const d = caminhoDoSetor(0, Math.PI * 1.4, 60, 300);

    expect(d).toMatch(/A 300 300 0 1 1 /);
  });

  it("um setor pequeno nao usa", () => {
    expect(caminhoDoSetor(0, 0.5, 60, 300)).toMatch(/A 300 300 0 0 1 /);
  });

  it("o caminho e' fechado, senao o preenchimento vaza", () => {
    expect(caminhoDoSetor(0, 0.5, 60, 300).trim().endsWith("Z")).toBe(true);
  });
});

describe("caminhoDoTalo", () => {
  it("aponta para o centro, e nao para longe dele", () => {
    const d = caminhoDoTalo({ x: 0, y: -300, raio: 300, arcoAteOVizinho: 40 });
    const numeros = (d.match(/-?\d+(\.\d+)?/g) ?? []).map(Number);

    expect(numeros).toHaveLength(4);

    const [, comeco, , fim] = numeros;

    // Comeca em -300 e termina mais perto de zero, sem atravessar o centro.
    expect(comeco).toBe(-300);
    expect(Math.abs(fim)).toBeLessThan(Math.abs(comeco));
    expect(Math.abs(fim)).toBeGreaterThan(0);
    expect(Math.sign(fim)).toBe(Math.sign(comeco));
  });

  it("some quando os vizinhos estao colados", () => {
    // 222 entidades ombro a ombro dao 222 tracinhos paralelos, que nao dizem "cada uma
    // pende de alguem" — dizem "ha' uma borda cinza aqui".
    expect(caminhoDoTalo({ x: 0, y: -300, raio: 300, arcoAteOVizinho: 2 })).toBe("");
  });

  it("no centro exato nao ha talo", () => {
    expect(caminhoDoTalo({ x: 0, y: 0, raio: 0, arcoAteOVizinho: 40 })).toBe("");
  });
});
