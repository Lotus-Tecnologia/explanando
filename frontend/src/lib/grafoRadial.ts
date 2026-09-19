import type { NoDoEstado } from "./contrato";

/**
 * O layout do grafo do Estado: nós, arestas, setores e anéis.
 *
 * **Duas dimensões independentes, e é isso que faz o desenho informar.**
 *
 * - O **ângulo** é o poder. Cada poder é um setor do disco, com o rótulo na borda.
 * - O **raio** é a natureza jurídica: ente federativo, administração direta, administração
 *   indireta, colegiado.
 *
 * O raio ser natureza — e não profundidade na árvore — é a correção mais importante em
 * relação ao desenho anterior. Um conselho ligado direto à Presidência está a um salto da
 * raiz e não tem a autoridade de um ministério; a profundidade dizia que sim, a natureza
 * diz a verdade. É também o equivalente brasileiro do eixo de autoridade da referência
 * (`docs/referencias/civlab.md`), com a vantagem de ser distinção jurídica e não editorial.
 *
 * Tudo aqui é função pura: o mesmo cálculo roda no servidor, para o SVG chegar pronto, e
 * no navegador, para a interação. Sem isso a página precisaria montar o desenho depois de
 * carregar — e a aposta do ADR-0001 é que ela seja um arquivo.
 */

export interface NoPosicionado {
  no: NoDoEstado;
  anel: number;
  /** Radianos a partir das 12 horas, no sentido horário. */
  angulo: number;
  raio: number;
  x: number;
  y: number;
  /** O raio do desenho do nó, pelo tamanho do ramo. */
  tamanho: number;
}

export interface SetorDoGrafo {
  chave: string;
  rotulo: string;
  inicio: number;
  fim: number;
  /** Quantos nós o setor tem, para a legenda. */
  nos: number;
}

export interface ArestaDoGrafo {
  de: number;
  para: number;
}

export interface AnelDoGrafo {
  indice: number;
  raio: number;
  rotulo: string;
}

export interface DesenhoDoGrafo {
  /** O que fica no miolo. Pode ser mais de um nó — ver `montarGrafo`. */
  miolo: NoDoEstado[];
  nos: NoPosicionado[];
  arestas: ArestaDoGrafo[];
  setores: SetorDoGrafo[];
  aneis: AnelDoGrafo[];
  raioExterno: number;
}

/** O raio do miolo, que carrega o rótulo da raiz. */
export const RAIO_DO_MIOLO = 66;

/**
 * A distância entre anéis.
 *
 * Generosa de propósito: o anel externo tem 222 entidades: com espaçamento apertado elas
 * viram uma faixa sólida em que nenhum nó é clicável. O que decide o número é o
 * comprimento do arco disponível para o anel mais cheio, não a estética.
 */
const PASSO_DO_ANEL = 98;

/**
 * O menor ângulo que um poder ocupa, mesmo tendo poucos órgãos.
 *
 * **É uma escolha declarada, não um acaso.** Medido: dos 336 nós do mapa, 266 são do
 * Executivo e 4 do Legislativo. Numa divisão puramente proporcional o Legislativo ficaria
 * com 4 graus — pequeno demais para ter rótulo, e quase impossível de acertar com o
 * ponteiro. Com o piso, ele fica visível e clicável, e a desproporção real continua óbvia:
 * o Executivo ainda ocupa quase dois terços do círculo.
 *
 * A tela diz isso em texto, ao lado do desenho.
 */
const ANGULO_MINIMO_DO_SETOR = (30 * Math.PI) / 180;

/** Um respiro entre setores, para a divisa ser visível sem uma linha. */
const FOLGA_ENTRE_SETORES = (2 * Math.PI) / 180;

const VOLTA = Math.PI * 2;

/** Os poderes na ordem em que aparecem no disco, começando ao meio-dia. */
const ORDEM_DOS_PODERES = ["executivo", "judiciario", "essenciais", "legislativo"];

const SEM_PODER = "sem-poder";

function rotuloDoSetor(chave: string, nos: readonly NoDoEstado[]): string {
  // O rótulo vem do próprio dado, e não de uma tabela duplicada aqui: quem traduz é o
  // domínio, e repetir a tradução no navegador é como as duas saem de sincronia.
  const comRotulo = nos.find((no) => no.poder !== null);

  return comRotulo?.poder ?? (chave === SEM_PODER ? "Sem classificação" : chave);
}

function pesoDoSetor(nos: readonly NoDoEstado[]): number {
  // O peso é o anel mais cheio, não o total: é ele que decide de quanto arco o setor
  // precisa para os nós não se encostarem.
  const porAnel = new Map<number, number>();

  for (const no of nos) {
    porAnel.set(no.anel, (porAnel.get(no.anel) ?? 0) + 1);
  }

  return Math.max(1, ...porAnel.values());
}

/**
 * O tamanho do nó, pelo tamanho do ramo que pende dele.
 *
 * **Dois tetos, e o segundo é o que importa.** O primeiro é de escala: raiz quadrada,
 * porque sem compressão a Presidência (95.473 unidades abaixo) teria um raio trezentas
 * vezes maior que o de um conselho e cobriria meio disco.
 *
 * O segundo vem do **espaço que o nó tem**: metade da distância até o vizinho no mesmo
 * anel. Sem ele, o anel das 222 entidades vira uma faixa sólida onde nenhum nó é
 * clicável — e um mapa em que não se pode clicar num órgão não é um mapa.
 */
function tamanhoDoNo(no: NoDoEstado, arcoDisponivel: number): number {
  const porRamo = 2.4 + Math.sqrt(no.unidadesSubordinadas) * 0.32;

  return Math.max(1.8, Math.min(13, porRamo, arcoDisponivel * 0.42));
}

/** O pai que o desenho usa: o do mapa quando existe, o real quando não. */
function paiNoDesenho(no: NoDoEstado): number | null {
  return no.codigoPaiNoMapa ?? no.codigoPai;
}

/**
 * Monta o desenho.
 *
 * **O miolo absorve a corrente de nó único que sai da raiz.** Medido: a raiz é a República
 * Federativa do Brasil, que no escopo federal tem um filho só — a União —, que por sua vez
 * ramifica nos quatro poderes. Dois anéis com um nó cada só empurrariam o resto do desenho
 * para longe do centro sem dizer nada; os dois aparecem juntos no miolo. A regra é
 * genérica: enquanto o nó do miolo tiver exatamente um filho, o filho entra no miolo
 * também — se o escopo mudar e a raiz passar a ramificar, o miolo encolhe sozinho.
 */
export function montarGrafo(todos: readonly NoDoEstado[]): DesenhoDoGrafo {
  const porCodigo = new Map(todos.map((no) => [no.codigo, no]));

  const filhos = new Map<number, NoDoEstado[]>();

  for (const no of todos) {
    const pai = paiNoDesenho(no);

    if (pai === null || !porCodigo.has(pai)) continue;

    const lista = filhos.get(pai);

    if (lista) lista.push(no);
    else filhos.set(pai, [no]);
  }

  const raizes = todos.filter((no) => {
    const pai = paiNoDesenho(no);

    return pai === null || !porCodigo.has(pai);
  });

  const raiz = [...raizes].sort(
    (a, b) => b.unidadesSubordinadas - a.unidadesSubordinadas,
  )[0];

  if (!raiz) {
    return { miolo: [], nos: [], arestas: [], setores: [], aneis: [], raioExterno: 0 };
  }

  const miolo: NoDoEstado[] = [raiz];

  // Enquanto houver um filho só, ele pertence ao miolo. O teto evita que um cadastro em
  // linha reta absorva o grafo inteiro.
  while (miolo.length < 6) {
    const unico = filhos.get(miolo[miolo.length - 1].codigo);

    if (!unico || unico.length !== 1) break;

    miolo.push(unico[0]);
  }

  const noMiolo = new Set(miolo.map((no) => no.codigo));
  const restantes = todos.filter((no) => !noMiolo.has(no.codigo));

  // ---------------------------------------------------------------- setores
  const porPoder = new Map<string, NoDoEstado[]>();

  for (const no of restantes) {
    const chave = no.chaveDoPoder ?? SEM_PODER;
    const lista = porPoder.get(chave);

    if (lista) lista.push(no);
    else porPoder.set(chave, [no]);
  }

  const chaves = [...porPoder.keys()].sort((a, b) => {
    const ia = ORDEM_DOS_PODERES.indexOf(a);
    const ib = ORDEM_DOS_PODERES.indexOf(b);

    return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
  });

  const pesos = chaves.map((chave) => pesoDoSetor(porPoder.get(chave)!));
  const somaDosPesos = pesos.reduce((a, b) => a + b, 0) || 1;

  const folgaTotal = FOLGA_ENTRE_SETORES * chaves.length;
  const pisoTotal = ANGULO_MINIMO_DO_SETOR * chaves.length;
  const aDistribuir = Math.max(0, VOLTA - folgaTotal - pisoTotal);

  const setores: SetorDoGrafo[] = [];
  let cursor = 0;

  chaves.forEach((chave, i) => {
    const nos = porPoder.get(chave)!;
    const largura = ANGULO_MINIMO_DO_SETOR + (aDistribuir * pesos[i]) / somaDosPesos;

    setores.push({
      chave,
      rotulo: rotuloDoSetor(chave, nos),
      inicio: cursor,
      fim: cursor + largura,
      nos: nos.length,
    });

    cursor += largura + FOLGA_ENTRE_SETORES;
  });

  // ---------------------------------------------------------------- nós
  const posicionados: NoPosicionado[] = [];
  const aneisUsados = new Set<number>();

  for (const setor of setores) {
    const nos = porPoder.get(setor.chave)!;
    const porAnel = new Map<number, NoDoEstado[]>();

    for (const no of nos) {
      const lista = porAnel.get(no.anel);

      if (lista) lista.push(no);
      else porAnel.set(no.anel, [no]);
    }

    for (const [anel, doAnel] of porAnel) {
      aneisUsados.add(anel);

      // Ordem alfabética dentro do anel: ela é estável entre coletas, e estabilidade é o
      // que permite comparar duas capturas da mesma tela.
      const ordenados = [...doAnel].sort((a, b) => a.nome.localeCompare(b.nome, "pt-BR"));

      const raio = RAIO_DO_MIOLO + PASSO_DO_ANEL * (anel + 1);
      const arco = setor.fim - setor.inicio;

      // Os nós ficam CENTRADOS no setor, com passo igual. Encostá-los na borda faria o
      // primeiro e o último parecerem pertencer ao setor vizinho.
      const passo = arco / (ordenados.length + 1);

      // O arco entre dois vizinhos deste anel. É ele que limita o tamanho do nó.
      const arcoEntreVizinhos = raio * passo;

      ordenados.forEach((no, i) => {
        const angulo = setor.inicio + passo * (i + 1);

        posicionados.push({
          no,
          anel,
          angulo,
          raio,
          x: raio * Math.sin(angulo),
          y: -raio * Math.cos(angulo),
          tamanho: tamanhoDoNo(no, arcoEntreVizinhos),
        });
      });
    }
  }

  // ---------------------------------------------------------------- arestas
  const desenhados = new Set(posicionados.map((p) => p.no.codigo));

  const arestas: ArestaDoGrafo[] = [];

  for (const no of restantes) {
    const pai = paiNoDesenho(no);

    if (pai === null) continue;

    // Aresta para o miolo também conta: é ela que liga cada poder à raiz.
    if (desenhados.has(pai) || noMiolo.has(pai)) {
      arestas.push({ de: pai, para: no.codigo });
    }
  }

  const aneis: AnelDoGrafo[] = [...aneisUsados]
    .sort((a, b) => a - b)
    .map((indice) => ({
      indice,
      raio: RAIO_DO_MIOLO + PASSO_DO_ANEL * (indice + 1),
      rotulo: "",
    }));

  return {
    miolo,
    nos: posicionados,
    arestas,
    setores,
    aneis,
    raioExterno: RAIO_DO_MIOLO + PASSO_DO_ANEL * (Math.max(0, ...aneisUsados) + 1),
  };
}

/**
 * O caminho de uma aresta: uma curva que afunda em direção ao centro.
 *
 * Reta ligando dois pontos do mesmo anel passaria por cima de tudo que está entre eles. A
 * curva com ponto de controle puxado para o miolo é o que faz um feixe de cem arestas
 * continuar legível — é o mesmo princípio do agrupamento por borda dos grafos radiais.
 */
export function caminhoDaAresta(
  de: { x: number; y: number },
  para: { x: number; y: number },
): string {
  const cx = (de.x + para.x) * 0.32;
  const cy = (de.y + para.y) * 0.32;

  return `M ${de.x.toFixed(1)} ${de.y.toFixed(1)} Q ${cx.toFixed(1)} ${cy.toFixed(1)} ${para.x.toFixed(1)} ${para.y.toFixed(1)}`;
}

/**
 * Quanto girar o disco para pôr um nó embaixo, no centro.
 *
 * É o gesto que a referência usa ao selecionar, e ele resolve um problema real: o rótulo
 * de um nó selecionado no topo do círculo cai em cima do cabeçalho, e o de um nó à
 * esquerda sai da tela. Embaixo sempre cabe.
 */
export function giroPara(angulo: number): number {
  return Math.PI - angulo;
}

/**
 * A forma de um nó, pelo tipo da unidade.
 *
 * **Forma, e não só cor** — ADR-0013. Quem não distingue as cores dos poderes continua
 * distinguindo um órgão de uma entidade pelo contorno.
 */
export function formaDoNo(chaveDoTipo: string): "circulo" | "quadrado" | "hexagono" | "losango" {
  switch (chaveDoTipo) {
    case "ente":
      return "circulo";
    case "orgao":
      return "quadrado";
    case "entidade":
      return "hexagono";
    default:
      return "losango";
  }
}

/** O caminho SVG de uma forma, centrada na origem. */
export function caminhoDaForma(forma: string, r: number): string {
  if (forma === "quadrado") {
    const l = r * 0.92;

    return `M ${-l},${-l} h ${l * 2} v ${l * 2} h ${-l * 2} Z`;
  }

  if (forma === "hexagono") {
    const pontos = [0, 1, 2, 3, 4, 5].map((i) => {
      const a = (Math.PI / 3) * i - Math.PI / 2;

      return `${(r * Math.cos(a)).toFixed(1)},${(r * Math.sin(a)).toFixed(1)}`;
    });

    return `M ${pontos.join(" L ")} Z`;
  }

  if (forma === "losango") {
    return `M 0,${-r} L ${r},0 L 0,${r} L ${-r},0 Z`;
  }

  // Círculo desenhado como caminho, para todas as formas saírem do mesmo elemento.
  return `M ${-r},0 a ${r},${r} 0 1,0 ${r * 2},0 a ${r},${r} 0 1,0 ${-r * 2},0`;
}
