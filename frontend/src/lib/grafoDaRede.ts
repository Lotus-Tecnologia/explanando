import type { ParDaRede, ParlamentarNaRede } from "./contrato";

/**
 * O desenho do mapa de fornecedores compartilhados.
 *
 * **Não é um grafo só, são quarenta e nove.** Medido na base: com um fornecedor em comum
 * bastando, 454 dos 463 parlamentares caem num componente conexo único — basta uma
 * corrente para ligar quase todo mundo. Exigindo dois fornecedores em comum, o que sobra
 * são 224 pessoas em 49 agrupamentos separados, de 2 a 14 cada um.
 *
 * Por isso a saída é uma lista de agrupamentos, e não uma tela cheia de linhas. Cada um é
 * desenhado no seu próprio quadro, e a ilha se lê sozinha — quem está nela, de que estados
 * são, e quanto passa pelas empresas que dividem.
 *
 * **O arranjo é determinístico.** Uma simulação de forças com posições iniciais sorteadas
 * daria um desenho diferente a cada build, e a página é estática: a mesma pessoa mudaria
 * de lugar sem nenhum dado ter mudado. Aqui as posições iniciais vêm do índice, e o número
 * de passos é fixo — mesma entrada, mesmo desenho, sempre.
 */

/** O lado do quadro de um agrupamento pequeno, em unidades de viewBox. */
export const LADO_DO_QUADRO = 260;

/**
 * A partir de quantas pessoas o quadro passa a ser largo, ocupando duas colunas da grade.
 *
 * **É a correção de um problema que só apareceu na tela.** Um quadro quadrado do mesmo
 * tamanho para todos faz o agrupamento de catorze desenhar catorze nomes no espaço em que
 * o de três desenha três, e os rótulos se sobrepõem. O quadro largo dá o dobro de área
 * física ao mesmo texto.
 *
 * <b>E é largo, não maior.</b> Dobrar a largura mantendo o quadrado dobraria também a
 * altura, e o cartão viraria uma faixa vertical de tela e meia com um grafo perdido no
 * meio — foi o que a primeira tentativa produziu.
 */
export const PESSOAS_PARA_QUADRO_LARGO = 8;

/** O quadro largo: proporção de 16 por 9, e não um quadrado esticado. */
export const QUADRO_LARGO = { largura: 520, altura: 292 } as const;

/** As dimensões do quadro de um agrupamento, pelo tamanho dele. */
export function dimensoesDoQuadro(pessoas: number): { largura: number; altura: number } {
  return pessoas >= PESSOAS_PARA_QUADRO_LARGO
    ? { largura: QUADRO_LARGO.largura, altura: QUADRO_LARGO.altura }
    : { largura: LADO_DO_QUADRO, altura: LADO_DO_QUADRO };
}

/** Folga entre a borda do quadro e o nó mais externo. Cabe o retrato e o rótulo. */
export const MARGEM_DO_QUADRO = 34;

/** Quantos passos a simulação dá. Fixo: é o que a torna reproduzível. */
export const PASSOS_DA_SIMULACAO = 320;

const RAIO_MINIMO = 11;
const RAIO_MAXIMO = 22;

/** O ângulo áureo, para as posições iniciais não caírem alinhadas. */
const ANGULO_AUREO = Math.PI * (3 - Math.sqrt(5));

export interface NoDaRede {
  readonly pessoa: ParlamentarNaRede;
  readonly x: number;
  readonly y: number;
  readonly raio: number;

  /** Quantas ligações densas ele tem **dentro deste agrupamento**. */
  readonly grau: number;

  /**
   * Ele é o **único** do seu estado neste agrupamento?
   *
   * É a informação que o desenho existe para mostrar. 91% dos pares densos são do mesmo
   * estado, então o de fora é o caso incomum — e um caso incomum que não se destaca é um
   * caso que ninguém vê.
   *
   * <b>Único, e não "minoria".</b> A primeira versão marcava quem não fosse da UF mais
   * frequente, e num agrupamento de nove cearenses com cinco goianos isso acendia cinco
   * anéis — cinco pessoas de um estado não são forasteiras, são a outra metade do grupo.
   * O achado é o parlamentar sozinho numa ilha de outro estado, como a Gleisi Hoffmann
   * (PR) entre treze pernambucanos.
   */
  readonly forasteiro: boolean;
}

export interface ArestaDaRede {
  readonly de: string;
  readonly para: string;
  readonly x1: number;
  readonly y1: number;
  readonly x2: number;
  readonly y2: number;
  readonly fornecedores: number;
  readonly valor: number;
  readonly mesmaUf: boolean;
  readonly mesmoPartido: boolean;

  /** A espessura, já resolvida: de 1,4 a 5 conforme os fornecedores em comum. */
  readonly espessura: number;
}

export interface AgrupamentoDaRede {
  /** Estável entre builds: o menor id de parlamentar do agrupamento. */
  readonly id: string;
  readonly nos: readonly NoDaRede[];
  readonly arestas: readonly ArestaDaRede[];

  /** As UFs presentes, da mais frequente para a menos, com a contagem. */
  readonly ufs: readonly { readonly uf: string; readonly quantos: number }[];

  /** A soma dos fornecedores em comum de todos os pares. Não é o gasto de ninguém. */
  readonly valor: number;

  /** Quantos fornecedores distintos aparecem nos pares deste agrupamento. */
  readonly pares: number;

  /** O viewBox em que os nós foram posicionados. Ver <c>dimensoesDoQuadro</c>. */
  readonly largura: number;
  readonly altura: number;
}

/**
 * A espessura de uma aresta, pelo número de fornecedores em comum.
 *
 * Cresce devagar de propósito: o par extremo da base divide **onze** fornecedores, e uma
 * escala linear o desenharia como uma barra atravessando o quadro, achatando todos os
 * outros contra o mínimo.
 */
export function espessuraDaAresta(fornecedores: number): number {
  return Math.min(5, 1.4 + Math.sqrt(Math.max(0, fornecedores - 1)) * 1.3);
}

/** A altura do bloco de rótulos abaixo do retrato: duas linhas. */
const ALTURA_DOS_ROTULOS = 20;

/** O tamanho da fonte do nome, e o da linha de partido-UF. Espelham o componente. */
const FONTE_DO_NOME = 7.4;
const FONTE_DO_PARTIDO = 6.4;

/**
 * O nome que cabe embaixo do retrato.
 *
 * Mora aqui, e não no componente, porque **o arranjo precisa saber exatamente que texto
 * será desenhado**. A separação de rótulos mede a largura da string; se o componente
 * cortasse o nome por conta própria, a medida do arranjo seria de outro texto.
 */
export function nomeCurto(nome: string): string {
  const limpo = nome.trim();

  if (limpo.length <= 16) {
    return limpo;
  }

  const partes = limpo.split(/\s+/);

  if (partes.length === 1) {
    return `${limpo.slice(0, 15)}…`;
  }

  const duas = `${partes[0]} ${partes[1]}`;

  return duas.length <= 16 ? duas : `${partes[0].slice(0, 15)}…`;
}

/**
 * A largura que os dois rótulos de um nó ocupam, em unidades de viewBox.
 *
 * **É o rótulo que decide a distância entre dois nós, e não o retrato.** Os retratos têm
 * 44 unidades no pior caso; `REPUBLICANOS-GO` tem mais de cinquenta. Medir por nó, em vez
 * de usar um valor único, evita afastar demais o agrupamento inteiro por causa de um
 * partido de nome comprido.
 *
 * O fator por caractere é a largura média de um dígito ou letra minúscula na fonte do
 * site, aferida na tela; não precisa ser exato, precisa ser conservador.
 */
export function larguraDoRotulo(pessoa: ParlamentarNaRede): number {
  const nome = nomeCurto(pessoa.nome).length * FONTE_DO_NOME * 0.56;
  const partido = `${pessoa.partido}-${pessoa.uf}`.length * FONTE_DO_PARTIDO * 0.58;

  return Math.max(30, nome, partido);
}

/** O raio de um nó, pelo grau dentro do agrupamento. */
function raioDoNo(grau: number): number {
  return Math.min(RAIO_MAXIMO, RAIO_MINIMO + Math.sqrt(grau) * 3.4);
}

/**
 * Agrupa os parlamentares pelos pares densos e posiciona cada agrupamento.
 *
 * Devolve do maior para o menor: a ilha de catorze pessoas é a que o leitor quer ver
 * primeiro, e a de duas é a que ele olha depois de entender o desenho.
 */
export function montarAgrupamentos(
  parlamentares: readonly ParlamentarNaRede[],
  pares: readonly ParDaRede[],
): AgrupamentoDaRede[] {
  const porId = new Map(parlamentares.map((pessoa) => [pessoa.id, pessoa]));

  // So' entram pares cujos dois lados conhecemos. Um id orfao viraria um no' sem nome, e
  // um no' sem nome num mapa de pessoas e' pior que um no' a menos.
  const validos = pares.filter((par) => porId.has(par.de) && porId.has(par.para));

  const raiz = new Map<string, string>();

  const achar = (id: string): string => {
    let atual = id;

    while (raiz.get(atual) !== atual) {
      const acima = raiz.get(atual) ?? atual;
      raiz.set(atual, raiz.get(acima) ?? acima);
      atual = raiz.get(atual) ?? atual;
    }

    return atual;
  };

  for (const par of validos) {
    if (!raiz.has(par.de)) raiz.set(par.de, par.de);
    if (!raiz.has(par.para)) raiz.set(par.para, par.para);
  }

  for (const par of validos) {
    const um = achar(par.de);
    const outro = achar(par.para);

    if (um !== outro) {
      raiz.set(um, outro);
    }
  }

  const membros = new Map<string, string[]>();

  for (const id of raiz.keys()) {
    const chefe = achar(id);
    const lista = membros.get(chefe);

    if (lista) {
      lista.push(id);
    } else {
      membros.set(chefe, [id]);
    }
  }

  const arestasPorChefe = new Map<string, ParDaRede[]>();

  for (const par of validos) {
    const chefe = achar(par.de);
    const lista = arestasPorChefe.get(chefe);

    if (lista) {
      lista.push(par);
    } else {
      arestasPorChefe.set(chefe, [par]);
    }
  }

  const agrupamentos: AgrupamentoDaRede[] = [];

  for (const [chefe, ids] of membros) {
    const doGrupo = arestasPorChefe.get(chefe) ?? [];
    const pessoas = ids
      .map((id) => porId.get(id))
      .filter((pessoa): pessoa is ParlamentarNaRede => pessoa !== undefined);

    if (pessoas.length === 0) {
      continue;
    }

    agrupamentos.push(posicionar(pessoas, doGrupo));
  }

  agrupamentos.sort(
    (um, outro) =>
      outro.nos.length - um.nos.length ||
      outro.valor - um.valor ||
      um.id.localeCompare(outro.id),
  );

  return agrupamentos;
}

/**
 * A simulação de forças de um agrupamento.
 *
 * Três forças, e nada mais: repulsão entre todos os pares, atração ao longo das arestas, e
 * um puxão fraco para o centro que impede um nó solto de escapar do quadro. Com no máximo
 * catorze nós, o custo quadrático é irrelevante e o resultado é bem melhor que um círculo
 * — num círculo, quem está no meio de tudo fica na borda como qualquer outro.
 */
function posicionar(
  pessoas: readonly ParlamentarNaRede[],
  pares: readonly ParDaRede[],
): AgrupamentoDaRede {
  const indice = new Map(pessoas.map((pessoa, i) => [pessoa.id, i]));

  const grau = new Array<number>(pessoas.length).fill(0);

  for (const par of pares) {
    const um = indice.get(par.de);
    const outro = indice.get(par.para);

    if (um !== undefined) grau[um] += 1;
    if (outro !== undefined) grau[outro] += 1;
  }

  const { largura, altura } = dimensoesDoQuadro(pessoas.length);
  const centroX = largura / 2;
  const centroY = altura / 2;
  const raioInicial = Math.min(largura, altura) / 2 - MARGEM_DO_QUADRO;

  // Posicoes iniciais pelo indice, no angulo aureo: deterministicas e ja' espalhadas.
  // O x nasce mais aberto que o y porque o quadro largo tem mais espaco horizontal, e
  // comecar numa bola redonda dentro de um retangulo desperdicaria as pontas.
  const abertura = (i: number) => 0.35 + (0.65 * i) / Math.max(1, pessoas.length);

  const x = pessoas.map(
    (_, i) =>
      centroX + Math.cos(i * ANGULO_AUREO) * raioInicial * abertura(i) * (largura / altura),
  );
  const y = pessoas.map(
    (_, i) => centroY + Math.sin(i * ANGULO_AUREO) * raioInicial * abertura(i),
  );

  // O piso e' alto porque cada no' carrega DOIS rotulos embaixo — nome e partido-UF —, e
  // rotulo sobreposto some. Foi medido na tela: abaixo disto, um agrupamento de catorze
  // vira uma pilha de nomes ilegivel.
  const distanciaIdeal =
    pessoas.length <= 2 ? 96 : Math.max(72, 170 / Math.sqrt(pessoas.length));

  for (let passo = 0; passo < PASSOS_DA_SIMULACAO; passo++) {
    // O resfriamento faz os primeiros passos moverem muito e os ultimos, quase nada — e' o
    // que impede o desenho de oscilar para sempre entre duas configuracoes.
    const calor = 1 - passo / PASSOS_DA_SIMULACAO;
    const dx = new Array<number>(pessoas.length).fill(0);
    const dy = new Array<number>(pessoas.length).fill(0);

    for (let i = 0; i < pessoas.length; i++) {
      for (let j = i + 1; j < pessoas.length; j++) {
        let ax = x[i] - x[j];
        let ay = y[i] - y[j];
        let dist = Math.hypot(ax, ay);

        if (dist < 0.01) {
          // Dois nos exatamente em cima um do outro nao teriam direcao de fuga. O
          // deslocamento vem do indice, para continuar deterministico.
          ax = (i + 1) * 0.013;
          ay = (j + 1) * 0.011;
          dist = Math.hypot(ax, ay);
        }

        const forca = (distanciaIdeal * distanciaIdeal) / (dist * dist);

        dx[i] += (ax / dist) * forca;
        dy[i] += (ay / dist) * forca;
        dx[j] -= (ax / dist) * forca;
        dy[j] -= (ay / dist) * forca;
      }
    }

    for (const par of pares) {
      const i = indice.get(par.de);
      const j = indice.get(par.para);

      if (i === undefined || j === undefined) {
        continue;
      }

      const ax = x[j] - x[i];
      const ay = y[j] - y[i];
      const dist = Math.max(0.01, Math.hypot(ax, ay));

      // Quanto mais fornecedores em comum, mais curta a aresta: a proximidade no desenho
      // passa a significar a mesma coisa que a espessura da linha.
      const alvo = distanciaIdeal / (1 + Math.min(3, par.fornecedores - 1) * 0.16);
      const forca = (dist - alvo) * 0.06;

      dx[i] += (ax / dist) * forca;
      dy[i] += (ay / dist) * forca;
      dx[j] -= (ax / dist) * forca;
      dy[j] -= (ay / dist) * forca;
    }

    for (let i = 0; i < pessoas.length; i++) {
      dx[i] += (centroX - x[i]) * 0.012;
      dy[i] += (centroY - y[i]) * 0.012;

      const passoMaximo = 9 * calor;
      const tamanho = Math.hypot(dx[i], dy[i]);
      const escala = tamanho > passoMaximo ? passoMaximo / tamanho : 1;

      x[i] += dx[i] * escala;
      y[i] += dy[i] * escala;
    }
  }

  const raios = grau.map((quantos) => raioDoNo(quantos));

  const rotulos = pessoas.map((pessoa) => larguraDoRotulo(pessoa));

  ajustarAoQuadro(x, y, raios, largura, altura);
  separarRotulos(x, y, raios, rotulos, largura, altura);

  const contagemDeUf = new Map<string, number>();

  for (const pessoa of pessoas) {
    contagemDeUf.set(pessoa.uf, (contagemDeUf.get(pessoa.uf) ?? 0) + 1);
  }

  const ufs = [...contagemDeUf.entries()]
    .map(([uf, quantos]) => ({ uf, quantos }))
    .sort((um, outro) => outro.quantos - um.quantos || um.uf.localeCompare(outro.uf));

  const nos: NoDaRede[] = pessoas.map((pessoa, i) => ({
    pessoa,
    x: x[i],
    y: y[i],
    raio: raios[i],
    grau: grau[i],
    forasteiro: pessoas.length > 2 && (contagemDeUf.get(pessoa.uf) ?? 0) === 1,
  }));

  const arestas: ArestaDaRede[] = pares
    .map((par) => {
      const i = indice.get(par.de);
      const j = indice.get(par.para);

      if (i === undefined || j === undefined) {
        return null;
      }

      return {
        de: par.de,
        para: par.para,
        x1: x[i],
        y1: y[i],
        x2: x[j],
        y2: y[j],
        fornecedores: par.fornecedores,
        valor: par.valor,
        mesmaUf: par.mesmaUf,
        mesmoPartido: par.mesmoPartido,
        espessura: espessuraDaAresta(par.fornecedores),
      };
    })
    .filter((aresta): aresta is ArestaDaRede => aresta !== null);

  return {
    id: [...pessoas].map((pessoa) => pessoa.id).sort()[0],
    nos,
    arestas,
    ufs,
    valor: pares.reduce((soma, par) => soma + par.valor, 0),
    pares: pares.length,
    largura,
    altura,
  };
}

/**
 * Afasta os nós até os **rótulos** pararem de se sobrepor.
 *
 * **Uma passada só de simulação de forças não basta, e isso só apareceu na tela.** A
 * simulação separa *retratos*, que têm 44 unidades de largura no máximo; o que se
 * sobrepõe é o par de linhas de texto embaixo deles, que chega a 52. O resultado eram
 * nomes empilhados no miolo dos agrupamentos densos, exatamente onde estão as pessoas
 * mais ligadas — as que o leitor mais quer identificar.
 *
 * Então cada nó vira uma caixa — a largura do rótulo, a altura do retrato mais as duas
 * linhas — e as caixas que se cruzam se empurram. É separação de retângulos, e não mais
 * uma força: rodar depois do enquadramento significa trabalhar nas coordenadas finais, e
 * cada nó fica preso dentro do quadro.
 */
function separarRotulos(
  x: number[],
  y: number[],
  raios: readonly number[],
  rotulos: readonly number[],
  largura: number,
  altura: number,
): void {
  for (let passo = 0; passo < 120; passo++) {
    let mexeu = false;

    for (let i = 0; i < x.length; i++) {
      for (let j = i + 1; j < x.length; j++) {
        const alturaI = raios[i] + ALTURA_DOS_ROTULOS;
        const alturaJ = raios[j] + ALTURA_DOS_ROTULOS;

        const sobraX = (rotulos[i] + rotulos[j]) / 2 + 9 - Math.abs(x[i] - x[j]);
        const sobraY = alturaI + alturaJ - Math.abs(y[i] - y[j]);

        if (sobraX <= 0 || sobraY <= 0) {
          continue;
        }

        mexeu = true;

        // Empurra pelo eixo de MENOR sobreposicao: e' o caminho mais curto para separar, e
        // o que menos desmancha o arranjo que a simulacao produziu.
        if (sobraX < sobraY) {
          const empurrao = (sobraX / 2 + 0.6) * (x[i] <= x[j] ? -1 : 1);

          x[i] += empurrao;
          x[j] -= empurrao;
        } else {
          const empurrao = (sobraY / 2 + 0.6) * (y[i] <= y[j] ? -1 : 1);

          y[i] += empurrao;
          y[j] -= empurrao;
        }
      }
    }

    for (let i = 0; i < x.length; i++) {
      x[i] = Math.min(largura - raios[i], Math.max(raios[i], x[i]));
      y[i] = Math.min(altura - raios[i] - ALTURA_DOS_ROTULOS, Math.max(raios[i], y[i]));
    }

    if (!mexeu) {
      return;
    }
  }
}

/**
 * Reenquadra o resultado da simulação no quadro, sem distorcer.
 *
 * **A escala é uniforme, e isso não é detalhe.** Esticar x e y por fatores diferentes faria
 * cada retrato circular virar uma elipse diferente, e o tamanho do nó deixaria de
 * significar o grau. Sobra espaço num dos eixos — e sobrar espaço é o preço certo.
 *
 * O enquadramento considera **só as posições**, e reserva a folga pelo maior raio: os
 * raios não são escalados junto, porque o tamanho do nó codifica o grau e não a sobra de
 * espaço do agrupamento. Um agrupamento apertado desenharia nós menores pelo motivo
 * errado.
 */
function ajustarAoQuadro(
  x: number[],
  y: number[],
  raios: readonly number[],
  largura: number,
  altura: number,
): void {
  if (x.length === 0) {
    return;
  }

  if (x.length === 1) {
    x[0] = largura / 2;
    y[0] = altura / 2;
    return;
  }

  // A folga de baixo e maior: e' onde ficam as duas linhas de rotulo de cada no'.
  const maiorRaio = Math.max(...raios);
  const folgaLateral = maiorRaio + 16;
  const folgaVertical = maiorRaio + 22;

  const utilX = Math.max(20, largura - folgaLateral * 2);
  const utilY = Math.max(20, altura - folgaVertical * 2);

  const minX = Math.min(...x);
  const maxX = Math.max(...x);
  const minY = Math.min(...y);
  const maxY = Math.max(...y);

  const extensaoX = Math.max(1, maxX - minX);
  const extensaoY = Math.max(1, maxY - minY);
  const escala = Math.min(utilX / extensaoX, utilY / extensaoY, 1.6);

  const deslocamentoX = (largura - extensaoX * escala) / 2 - minX * escala;
  const deslocamentoY = (altura - extensaoY * escala) / 2 - minY * escala;

  for (let i = 0; i < x.length; i++) {
    x[i] = x[i] * escala + deslocamentoX;
    y[i] = y[i] * escala + deslocamentoY;
  }
}
