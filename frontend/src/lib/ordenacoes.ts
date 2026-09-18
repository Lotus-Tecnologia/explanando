import type { OrdenacaoDoRanking } from "./contrato";
import type { Casa } from "./casas";

/**
 * As três ordenações, cada uma com rota própria.
 *
 * Eram três estados de uma página só, decididos por `?ordenar=`. O problema não era estético:
 * uma página que lê a query string é renderizada sob demanda, e a home é justamente onde um
 * link viral bate — o lugar em que o ADR-0001 aposta a CDN. Com rota própria, as três viram
 * arquivo estático, e de quebra viram **três páginas indexáveis** em vez de uma.
 *
 * A explicação de cada uma fica aqui, ao lado do rótulo, porque a diferença entre elas é o
 * que impede a leitura errada: "densidade" que exclui 15 pessoas sem dizer por quê faria o
 * leitor concluir que aquelas 15 não têm indicador nenhum.
 */
export interface Ordenacao {
  chave: OrdenacaoDoRanking;

  /**
   * O trecho que segue a casa na rota. Vazio para `volume`, que mora na raiz da casa —
   * o endereço mais compartilhável de cada ranking é o mais curto.
   */
  sufixo: string;

  rotulo: string;
  titulo: string;
  descricao: string;

  /**
   * A explicação longa, que vive ao lado da tabela. É ela que impede a leitura errada:
   * "densidade" que exclui gente sem dizer por quê faz o leitor concluir que essas pessoas
   * não têm indicador nenhum.
   */
  explicacao: string;

  /**
   * A versão curta, para a home.
   *
   * A home existe para encaminhar, não para ensinar. Repetir ali a explicação inteira põe
   * seis blocos de texto denso entre quem chegou e o primeiro clique — e a explicação longa
   * continua esperando na página de destino, que é onde ela é necessária.
   */
  resumo: string;
}

export const ORDENACOES: readonly Ordenacao[] = [
  {
    chave: "volume",
    sufixo: "",
    rotulo: "Volume gasto",
    titulo: "Gastos da cota parlamentar — ranking por volume",
    descricao:
      "Quanto cada parlamentar consumiu da cota, em reais, com estornos abatidos. Dados públicos da Câmara dos Deputados.",
    explicacao:
      "Soma em reais, com estornos abatidos. Inclui todos os parlamentares — gasto não depende de amostra.",
    resumo: "Quanto se gastou, em reais.",
  },
  {
    chave: "indice",
    sufixo: "/indice",
    rotulo: "Densidade de indicadores",
    titulo: "Densidade de indicadores por 100 despesas — ranking",
    descricao:
      "Indicadores de um motor de regras determinístico, medidos por 100 despesas analisadas. Descreve inconsistências; não imputa conduta.",
    explicacao:
      "Indicadores por 100 despesas. Exclui quem tem menos de 100 despesas analisadas, onde a densidade seria ruído — essas pessoas continuam no ranking de volume.",
    resumo: "Achados por 100 despesas analisadas.",
  },
  {
    chave: "gravidade",
    sufixo: "/gravidade",
    rotulo: "Indicadores de alta relevância",
    titulo: "Indicadores de alta relevância — ranking",
    descricao:
      "Ordenado apenas pelos achados de maior peso, que são 3,3% do total. Descreve inconsistências; não imputa conduta.",
    explicacao:
      "Ordena apenas pelos achados de maior peso — pelo total, quem tem muitos achados graves fica abaixo de quem tem zero. Exclui quem tem menos de 100 despesas analisadas.",
    resumo: "Só os achados de maior peso.",
  },
];

/**
 * A ordenação de uma chave, ou `null`.
 *
 * Nulo, e não o volume por padrão: a API devolve `400` para ordenação desconhecida
 * justamente para não deixar o consumidor achar que estava ordenando por risco quando não
 * estava. Cair no volume em silêncio aqui refaria o erro do outro lado.
 */
export function ordenacaoPorChave(chave: string | undefined): Ordenacao | null {
  return ORDENACOES.find((ordenacao) => ordenacao.chave === chave) ?? null;
}

/**
 * A rota de um ranking: a casa manda, a ordenação sufixa.
 *
 * Existe para que nenhuma tela monte esse endereço à mão. Caminho escrito literalmente em
 * seis lugares é onde `/senado/indice` vira `/senado/índice` num deles, e o link quebra só
 * numa aba.
 */
export function caminhoDoRanking(casa: Casa, ordenacao: Ordenacao): string {
  return `${casa.caminho}${ordenacao.sufixo}`;
}
