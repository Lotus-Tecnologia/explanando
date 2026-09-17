/**
 * As duas casas, e por que elas nunca compartilham um ranking.
 *
 * Deputado e senador disputam **cotas distintas**: a CEAP e a CEAPS têm regras próprias e
 * valores próprios por UF. Pôr os dois no mesmo eixo compara números que não medem a mesma
 * coisa — e o ranking é justamente a tela onde a comparação é o conteúdo.
 *
 * Por isso a casa é parte da rota, e não um filtro dentro da página. Rota separada é
 * endereço compartilhável separado, página estática separada e — o que mais importa — não
 * há estado em que alguém veja as duas misturadas por engano.
 *
 * A assimetria de tamanho é real e não vira assimetria de estrutura: são 513 deputados
 * contra 118 senadores, mas `/camara` e `/senado` são irmãs. A raiz `/` não pertence a
 * nenhuma das duas.
 */
export type ChaveDaCasa = "camara" | "senado";

export interface Casa {
  chave: ChaveDaCasa;

  /** O que a API espera em `?casa=`. */
  parametro: "deputados" | "senadores";

  /** O nome que o contrato devolve em `ItemDoRanking.casa`. */
  nomeOficial: string;

  /** Curto, para abas e migalhas. */
  rotulo: string;

  /** Como se chama uma pessoa desta casa, no singular e no plural. */
  integrante: string;
  integrantes: string;

  /** A sigla da cota, que aparece nas explicações. */
  cota: string;

  /** A raiz das rotas da casa: também o ranking por volume dela. */
  caminho: string;
}

export const CASAS: readonly Casa[] = [
  {
    chave: "camara",
    parametro: "deputados",
    nomeOficial: "Câmara dos Deputados",
    rotulo: "Câmara",
    integrante: "deputado",
    integrantes: "deputados",
    cota: "CEAP",
    caminho: "/camara",
  },
  {
    chave: "senado",
    parametro: "senadores",
    nomeOficial: "Senado Federal",
    rotulo: "Senado",
    integrante: "senador",
    integrantes: "senadores",
    cota: "CEAPS",
    caminho: "/senado",
  },
];

/**
 * A casa de uma chave, ou `null`.
 *
 * Nulo, e não uma casa padrão: a API devolve `400` para casa ausente justamente para não
 * deixar ninguém achar que viu as duas quando viu uma. Cair na Câmara em silêncio aqui
 * refaria o erro do outro lado.
 */
export function casaPorChave(chave: string | undefined): Casa | null {
  return CASAS.find((casa) => casa.chave === chave) ?? null;
}
