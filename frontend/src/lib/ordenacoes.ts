import type { OrdenacaoDoRanking } from "./contrato";

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
  caminho: string;
  rotulo: string;
  titulo: string;
  descricao: string;
  explicacao: string;
}

export const ORDENACOES: readonly Ordenacao[] = [
  {
    chave: "volume",
    caminho: "/",
    rotulo: "Volume gasto",
    titulo: "Gastos da cota parlamentar — ranking por volume",
    descricao:
      "Quanto cada parlamentar consumiu da cota, em reais, com estornos abatidos. Dados públicos da Câmara dos Deputados.",
    explicacao:
      "Soma em reais, com estornos abatidos. Inclui todos os parlamentares — gasto não depende de amostra.",
  },
  {
    chave: "indice",
    caminho: "/ranking/indice",
    rotulo: "Densidade de indicadores",
    titulo: "Densidade de indicadores por 100 despesas — ranking",
    descricao:
      "Indicadores de um motor de regras determinístico, medidos por 100 despesas analisadas. Descreve inconsistências; não imputa conduta.",
    explicacao:
      "Indicadores por 100 despesas. Exclui quem tem menos de 100 despesas analisadas, onde a densidade seria ruído — essas pessoas continuam no ranking de volume.",
  },
  {
    chave: "gravidade",
    caminho: "/ranking/gravidade",
    rotulo: "Indicadores de alta relevância",
    titulo: "Indicadores de alta relevância — ranking",
    descricao:
      "Ordenado apenas pelos achados de maior peso, que são 3,3% do total. Descreve inconsistências; não imputa conduta.",
    explicacao:
      "Ordena apenas pelos achados de maior peso — pelo total, quem tem muitos achados graves fica abaixo de quem tem zero. Exclui quem tem menos de 100 despesas analisadas.",
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
