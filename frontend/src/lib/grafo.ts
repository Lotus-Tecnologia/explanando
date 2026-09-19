/**
 * As cores do grafo do Estado.
 *
 * **Este arquivo já foi o layout inteiro.** Ele desenhava a estrutura como anéis
 * concêntricos — um *sunburst* —, e isso foi substituído por um grafo de nós e arestas em
 * `grafoRadial.ts`, que separa duas dimensões que o anel confundia: o ângulo passou a ser
 * o poder e o raio, a natureza jurídica. O que sobreviveu foi a paleta.
 */

/**
 * A cor de um poder.
 *
 * São variáveis CSS, e não valores fixos: o site tem tema claro e escuro, e uma cor
 * literal ficaria ilegível num dos dois. Os cinco tons passaram pelo cálculo de contraste
 * que o ADR-0013 exige, nos dois fundos, e **nenhum deles é o vermelho de gravidade alta**.
 *
 * **O poder nulo tem cor própria, e não a do Executivo.** A raiz e os entes federativos
 * chegam sem classificação porque a fonte não os classifica — pintá-los como Executivo
 * inventaria uma informação que o Estado não deu sobre si mesmo.
 *
 * Aceita a **chave estável** (`executivo`) e o rótulo de tela (`Executivo`). A chave é a
 * forma certa e é o que o grafo passa; o rótulo continua aceito porque os painéis de
 * distribuição recebem contagens já traduzidas, sem a chave ao lado.
 */
export function corDoPoder(poder: string | null): string {
  switch (poder) {
    // A chave estavel, que o desenho deve usar.
    case "executivo":
    case "Executivo":
      return "var(--color-poder-executivo)";
    case "legislativo":
    case "Legislativo":
      return "var(--color-poder-legislativo)";
    case "judiciario":
    case "Judiciário":
      return "var(--color-poder-judiciario)";
    case "essenciais":
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
