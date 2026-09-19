import type { NoDoEstado } from "./contrato";

/**
 * O desenho radial da estrutura do Estado.
 *
 * **Por que anéis e não uma teia de pontos e linhas.** A referência desenha um grafo de nós
 * ligados por arestas; com os 340 órgãos brasileiros isso vira uma bola cinza onde nada se
 * lê. O anel resolve as duas coisas de uma vez: a hierarquia *é* a distância do centro, e o
 * tamanho de cada fatia pode carregar informação — aqui, quantas unidades pendem daquele
 * ramo.
 *
 * **O peso é o que impede o mapa de mentir.** Sem ele, o Ministério da Saúde e um conselho
 * de três pessoas ocupariam o mesmo espaço, e um mapa em que tudo tem o mesmo tamanho não
 * informa nada sobre o Estado — informa sobre a nossa lista.
 *
 * Tudo aqui é função pura sobre números: o desenho é calculado no servidor e vai para o
 * navegador como SVG pronto, sem JavaScript nenhum. É o mesmo raciocínio do ADR-0001 — a
 * página que um link viral abre precisa ser um arquivo.
 */

export interface Fatia {
  codigo: number;
  nome: string;
  sigla: string | null;
  tipo: string;
  poder: string | null;
  baseLegal: string | null;
  unidadesSubordinadas: number;

  /** A que distância do centro, contando de 0. */
  nivel: number;

  /** Em radianos, a partir das 12 horas, no sentido horário. */
  inicio: number;
  fim: number;
}

export interface DesenhoRadial {
  fatias: Fatia[];

  /** O nó do centro — a raiz da árvore. Nulo quando não há raiz identificável. */
  centro: NoDoEstado | null;

  /** Quantos anéis o desenho tem. */
  niveis: number;
}

/**
 * Quanto uma unidade "pesa" no desenho.
 *
 * `+1` porque a própria unidade conta: sem isso, uma folha teria peso zero e desapareceria
 * do anel — e um órgão sem subordinadas existe tanto quanto um com mil.
 *
 * **A raiz quadrada é uma escolha, e ela precisa estar dita na tela.** Medido na base real:
 * o Executivo tem 95.687 unidades, o Judiciário 743, as Funções Essenciais 125 e o
 * Legislativo 101. Numa escala linear, três dos quatro poderes viram fatias de menos de um
 * grau — invisíveis —, e um mapa em que três quartos do Estado somem não cumpre a função de
 * mapa. Com a raiz, o Executivo ainda domina (86% da volta) e os outros três continuam
 * visíveis e clicáveis.
 *
 * A compressão só vale para a **largura**. O número exato aparece na legenda, na lista e no
 * `title` de cada fatia, e a legenda do desenho diz que a escala é comprimida — não se
 * comprime um dado sem avisar.
 */
function peso(no: NoDoEstado): number {
  return Math.sqrt(no.unidadesSubordinadas + 1);
}

/** O pai que o desenho usa: o do mapa quando existe, o real quando não. */
function paiNoDesenho(no: NoDoEstado): number | null {
  return no.codigoPaiNoMapa ?? no.codigoPai;
}

/**
 * Monta o desenho a partir da lista plana de nós.
 *
 * A raiz é o nó cujo pai não está na lista — e não um código fixo. Amarrar o desenho ao
 * código 200000 quebraria em silêncio no dia em que a coleta rodasse com outro escopo.
 *
 * `nivelMaximo` para em cinco anéis por uma razão medida: a base real tem 216 nós no quinto
 * nível e **cinco** no sexto. Esses cinco viravam agulhas finas atravessando a borda do
 * disco, e uma agulha de um décimo de grau não é informação — é ruído que faz o desenho
 * parecer quebrado. Eles continuam na lista e a um clique de distância.
 */
export function montarDesenho(nos: readonly NoDoEstado[], nivelMaximo = 5): DesenhoRadial {
  const porCodigo = new Map(nos.map((no) => [no.codigo, no]));

  const filhos = new Map<number, NoDoEstado[]>();

  for (const no of nos) {
    const pai = paiNoDesenho(no);

    // Nó cujo pai não veio na lista é tratado como raiz: é o que acontece com um órgão
    // cujo ancestral a fonte não soube descrever, e perdê-lo seria pior que enraizá-lo.
    const chave = pai !== null && porCodigo.has(pai) ? pai : null;

    if (chave === null) continue;

    const lista = filhos.get(chave);

    if (lista) lista.push(no);
    else filhos.set(chave, [no]);
  }

  const raizes = nos.filter((no) => {
    const pai = paiNoDesenho(no);

    return pai === null || !porCodigo.has(pai);
  });

  // Com mais de uma raiz, a de maior peso manda: é a árvore que representa o Estado, e as
  // outras são fragmentos de coleta incompleta.
  const centro = [...raizes].sort((a, b) => peso(b) - peso(a))[0] ?? null;

  if (!centro) {
    return { fatias: [], centro: null, niveis: 0 };
  }

  const fatias: Fatia[] = [];

  const empilhar = (pai: NoDoEstado, inicio: number, fim: number, nivel: number): void => {
    if (nivel > nivelMaximo) return;

    const filhas = filhos.get(pai.codigo);

    if (!filhas || filhas.length === 0) return;

    // Do mais pesado para o mais leve: o olho encontra os grandes sem procurar, e a ordem
    // fica estável entre coletas, o que importa para comparar duas capturas da tela.
    const ordenadas = [...filhas].sort(
      (a, b) => peso(b) - peso(a) || a.nome.localeCompare(b.nome, "pt-BR"),
    );

    const total = ordenadas.reduce((soma, no) => soma + peso(no), 0);

    if (total === 0) return;

    let cursor = inicio;

    for (const filho of ordenadas) {
      const largura = ((fim - inicio) * peso(filho)) / total;

      fatias.push({
        codigo: filho.codigo,
        nome: filho.nome,
        sigla: filho.sigla,
        tipo: filho.tipo,
        poder: filho.poder,
        baseLegal: filho.baseLegal,
        unidadesSubordinadas: filho.unidadesSubordinadas,
        nivel,
        inicio: cursor,
        fim: cursor + largura,
      });

      empilhar(filho, cursor, cursor + largura, nivel + 1);
      cursor += largura;
    }
  };

  empilhar(centro, 0, Math.PI * 2, 1);

  return {
    fatias,
    centro,
    niveis: fatias.reduce((maior, fatia) => Math.max(maior, fatia.nivel), 0),
  };
}

/**
 * O caminho SVG de um anel entre dois raios e dois ângulos.
 *
 * Ângulo zero é meio-dia, e cresce no sentido horário — é como se lê um relógio, e não
 * como a trigonometria conta. A troca acontece aqui, uma vez, em vez de em cada chamada.
 */
export function caminhoDeArco(
  raioInterno: number,
  raioExterno: number,
  inicio: number,
  fim: number,
): string {
  const ponto = (raio: number, angulo: number): [number, number] => [
    raio * Math.sin(angulo),
    -raio * Math.cos(angulo),
  ];

  // Uma fatia que fecha o círculo inteiro não pode virar um arco só: com início e fim no
  // mesmo ponto, o `A` do SVG não desenha nada. Dois semicírculos resolvem.
  const volta = Math.PI * 2;

  if (fim - inicio >= volta - 1e-9) {
    const meio = inicio + Math.PI;

    return [
      caminhoDeArco(raioInterno, raioExterno, inicio, meio),
      caminhoDeArco(raioInterno, raioExterno, meio, inicio + volta),
    ].join(" ");
  }

  const grande = fim - inicio > Math.PI ? 1 : 0;

  const [x1, y1] = ponto(raioExterno, inicio);
  const [x2, y2] = ponto(raioExterno, fim);
  const [x3, y3] = ponto(raioInterno, fim);
  const [x4, y4] = ponto(raioInterno, inicio);

  return [
    `M ${x1.toFixed(2)} ${y1.toFixed(2)}`,
    `A ${raioExterno} ${raioExterno} 0 ${grande} 1 ${x2.toFixed(2)} ${y2.toFixed(2)}`,
    `L ${x3.toFixed(2)} ${y3.toFixed(2)}`,
    `A ${raioInterno} ${raioInterno} 0 ${grande} 0 ${x4.toFixed(2)} ${y4.toFixed(2)}`,
    "Z",
  ].join(" ");
}

/**
 * A cor de uma fatia, decidida pelo poder.
 *
 * São variáveis CSS, e não valores fixos: o site tem tema claro e escuro, e uma cor
 * literal aqui ficaria ilegível num dos dois. O ADR-0013 exige contraste calculado, e só o
 * token declarado no tema entrega isso nos dois fundos.
 *
 * **O poder nulo tem cor própria, e não a do Executivo.** A raiz e os entes federativos
 * chegam sem classificação porque a fonte não os classifica — pintá-los como Executivo
 * inventaria uma informação que o Estado não deu sobre si mesmo.
 */
export function corDoPoder(poder: string | null): string {
  switch (poder) {
    case "Executivo":
      return "var(--color-poder-executivo)";
    case "Legislativo":
      return "var(--color-poder-legislativo)";
    case "Judiciário":
      return "var(--color-poder-judiciario)";
    case "Funções essenciais à Justiça":
      return "var(--color-poder-essenciais)";
    default:
      return "var(--color-poder-indefinido)";
  }
}

/**
 * A escala neutra dos painéis que **não** falam de poder.
 *
 * Tipo de unidade e base legal não têm cor própria no produto, e emprestar a paleta dos
 * poderes faria "criada por lei" parecer Judiciário. São tokens que já existem e já
 * passaram pelo cálculo de contraste do ADR-0013 — nenhuma cor nova entra por aqui, e
 * nenhuma delas é o vermelho de gravidade alta.
 *
 * A ordem vai do mais forte ao mais fraco, e as contagens chegam ordenadas por tamanho:
 * o item maior recebe o tom mais firme, que é como o olho espera encontrá-lo.
 */
const ESCALA_NEUTRA = [
  "var(--color-acento)",
  "var(--color-titulo)",
  "var(--color-borda-estrutural)",
  "var(--color-texto-suave)",
  "var(--color-borda-suave)",
];

export function corDaEscala(indice: number): string {
  return ESCALA_NEUTRA[indice % ESCALA_NEUTRA.length];
}
