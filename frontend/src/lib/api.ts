import { CASAS, type Casa } from "./casas";
import type {
  DespesaDetalhada,
  MapaDaRede,
  MapaDoEstado,
  MudancaDoEstado,
  NoDoEstado,
  PontoDaSerie,
  ItemDeDespesa,
  ItemDoRanking,
  OrdenacaoDoRanking,
  Pagina,
  PerfilDoParlamentar,
} from "./contrato";

const BASE =
  process.env.NEXT_PUBLIC_API ?? process.env.EXPLANANDO_API ?? "http://localhost:5199";

/**
 * Quanto tempo uma página fica válida antes de o Next revalidar.
 *
 * Cinco minutos, o mesmo do cache do ranking na API. O dado muda uma vez por varredura, não
 * de hora em hora — e é a CDN, não o Redis, que absorve o pico viral (ADR-0001). Servir uma
 * página com minutos de atraso custa zero; derrubar o banco num pico custa a página inteira.
 */
export const SEGUNDOS_DE_REVALIDACAO = 300;

class ErroDaApi extends Error {
  constructor(
    readonly status: number,
    readonly caminho: string,
  ) {
    super(`A API respondeu ${status} para ${caminho}.`);
    this.name = "ErroDaApi";
  }
}

/**
 * Como o nosso renderizador se identifica para a API.
 *
 * A API pública tem teto por origem, e **este processo é o cliente mais pesado que existe
 * por IP**: o build busca as três páginas de ranking e os 513 perfis de um endereço só, com
 * onze processos em paralelo. Sem se identificar, o primeiro deploy esbarra no próprio
 * limite — e a falha apareceria como build quebrado, longe da causa.
 *
 * A variável **não** leva o prefixo `NEXT_PUBLIC_`, e isso é o ponto: o Next embute as que
 * levam dentro do pacote que vai para o navegador, e uma chave embutida num pacote público
 * deixa de ser chave. Toda chamada daqui roda no servidor.
 *
 * Lida a cada chamada, e não uma vez na importação, para que continue sendo variável de
 * ambiente de verdade — congelá-la na importação é meio caminho para embuti-la.
 */
function cabecalhos(): HeadersInit {
  const chave = process.env.EXPLANANDO_CHAVE_INTERNA;

  return chave
    ? { Accept: "application/json", "X-Explanando-Interno": chave }
    : { Accept: "application/json" };
}

async function buscar<T>(caminho: string): Promise<T | null> {
  const resposta = await fetch(`${BASE}${caminho}`, {
    next: { revalidate: SEGUNDOS_DE_REVALIDACAO },
    headers: cabecalhos(),
  });

  // 404 é resposta legítima — parlamentar que não existe —, e não falha. Confundir as duas
  // faria a página de erro aparecer no lugar do "não encontrado".
  if (resposta.status === 404) {
    return null;
  }

  if (!resposta.ok) {
    throw new ErroDaApi(resposta.status, caminho);
  }

  return (await resposta.json()) as T;
}

/**
 * Uma página do ranking de **uma** casa.
 *
 * `casa` não tem padrão, aqui nem na API — omitir lá devolve 400. São cotas distintas, com
 * regras e valores próprios por UF, e um padrão silencioso faria quem esquecesse o parâmetro
 * receber uma casa só achando que recebeu as duas.
 */
export async function ranquear(
  casa: Casa,
  ordenacao: OrdenacaoDoRanking = "volume",
  pagina = 1,
  itens = 50,
): Promise<Pagina<ItemDoRanking>> {
  const resultado = await buscar<Pagina<ItemDoRanking>>(
    `/api/v1/parlamentares?casa=${casa.parametro}&ordenar=${ordenacao}` +
      `&pagina=${pagina}&itens=${itens}`,
  );

  // O ranking nunca é 404: ou responde, ou é falha de verdade.
  if (!resultado) {
    throw new ErroDaApi(404, "/api/v1/parlamentares");
  }

  return resultado;
}

/**
 * O teto que o servidor aplica, replicado aqui de propósito.
 *
 * `ConsultasPublicasEfCore.Normalizar` faz `Math.Clamp(itens, 1, 200)`. Quem pede 513 recebe
 * **200 e uma resposta 200 OK** — sem erro, sem aviso. Pedir a base inteira numa chamada só
 * pré-renderizaria 200 dos 513 parlamentares, e o build passaria verde relatando sucesso.
 */
export const MAXIMO_DE_ITENS_POR_PAGINA = 200;

/**
 * O ranking inteiro, página a página, na ordem que o servidor decidiu.
 *
 * A ordenação é **regra de negócio** e fica no servidor: `indice` e `gravidade` excluem quem
 * está abaixo do piso amostral do ADR-0009, e reordenar no navegador duplicaria essa regra
 * onde ela sairia de sincronia sem ninguém perceber.
 *
 * Devolve `null` quando a fonte não responde. Estourar derrubaria o build inteiro por uma
 * indisponibilidade momentânea da API — e a página sabe dizer que o dado não veio, o que é
 * melhor que não existir página nenhuma.
 */
export async function ranquearTudo(
  casa: Casa,
  ordenacao: OrdenacaoDoRanking,
): Promise<ItemDoRanking[] | null> {
  try {
    const itens: ItemDoRanking[] = [];
    let numero = 1;
    let totalDePaginas = 1;

    do {
      const pagina = await ranquear(casa, ordenacao, numero, MAXIMO_DE_ITENS_POR_PAGINA);

      if (pagina.itens.length === 0) {
        break;
      }

      itens.push(...pagina.itens);
      totalDePaginas = pagina.totalDePaginas;
      numero += 1;
    } while (numero <= totalDePaginas);

    return itens;
  } catch (erro) {
    console.warn(
      `[explanando] Ranking '${ordenacao}' de ${casa.rotulo} indisponível: ` +
        `${erro instanceof Error ? erro.message : erro}`,
    );

    return null;
  }
}

/**
 * Todos os ids de parlamentar, para o Next gerar as páginas estáticas no build.
 *
 * É o que decide se a premissa de custo do ADR-0001 se sustenta: **é a CDN, não o Redis, que
 * absorve o pico viral**, e a CDN só serve o que já existe como arquivo. Página dinâmica num
 * pico vira uma consulta ao Postgres por acesso.
 *
 * Devolve lista vazia — e não estoura — quando a API não responde: o build precisa passar em
 * CI sem backend no ar. As páginas ausentes continuam sendo renderizadas sob demanda, porque
 * `dynamicParams` segue no padrão. Pré-renderizar é otimização, não muro.
 */
export async function coletarIdsParaPreRender(): Promise<string[]> {
  // 'volume' de propósito: é a única ordenação que inclui todo mundo. As outras duas excluem
  // quem está abaixo do piso amostral, e essas pessoas também precisam de página.
  //
  // AS DUAS CASAS, somadas. O ranking é separado porque comparar as cotas seria errado; a
  // pré-renderização não compara nada — ela só precisa da lista completa de quem tem perfil.
  // Buscar uma casa só deixaria a outra inteira fora do sitemap e das páginas estáticas.
  const porCasa = await Promise.all(CASAS.map((casa) => ranquearTudo(casa, "volume")));

  if (porCasa.every((itens) => itens === null)) {
    console.warn(
      "[explanando] Sem ranking para a pré-renderização: os perfis ficam dinâmicos neste build.",
    );

    return [];
  }

  return porCasa.flatMap((itens) => itens ?? []).map((item) => item.id);
}

export function obterPerfil(id: string): Promise<PerfilDoParlamentar | null> {
  return buscar<PerfilDoParlamentar>(`/api/v1/parlamentares/${id}`);
}

export interface FiltroDeDespesas {
  comFlag?: boolean;
  ano?: number;
  mes?: number;
  tipo?: string;
  pagina?: number;
  itens?: number;
}

export async function listarDespesas(
  id: string,
  parametros: FiltroDeDespesas = {},
): Promise<Pagina<ItemDeDespesa> | null> {
  const consulta = new URLSearchParams();

  if (parametros.comFlag !== undefined) {
    consulta.set("comFlag", String(parametros.comFlag));
  }

  if (parametros.ano !== undefined) {
    consulta.set("ano", String(parametros.ano));
  }

  // Mês sem ano é 400 na API, de propósito — "julho" de que ano? O filtro daqui só monta o
  // par completo, então a recusa nunca chega a acontecer por descuido nosso.
  if (parametros.mes !== undefined && parametros.ano !== undefined) {
    consulta.set("mes", String(parametros.mes));
  }

  if (parametros.tipo !== undefined && parametros.tipo !== "") {
    consulta.set("tipo", parametros.tipo);
  }

  consulta.set("pagina", String(parametros.pagina ?? 1));
  consulta.set("itens", String(parametros.itens ?? 25));

  return buscar<Pagina<ItemDeDespesa>>(
    `/api/v1/parlamentares/${id}/despesas?${consulta.toString()}`,
  );
}

/** Uma despesa e o parlamentar dela. É o endereço que um link compartilhado abre. */
export function obterDespesa(id: string): Promise<DespesaDetalhada | null> {
  return buscar<DespesaDetalhada>(`/api/v1/despesas/${id}`);
}

// ---------------------------------------------------------------------------
//  O grafo do Estado
// ---------------------------------------------------------------------------

/**
 * O mapa da estrutura do Estado, no nível de entes, órgãos e entidades.
 *
 * Devolve `null` quando a fonte não responde, pela mesma razão de `ranquearTudo`: uma
 * indisponibilidade da API não pode derrubar o build inteiro, e a página sabe dizer que o
 * dado não veio.
 */
/**
 * O mapa de fornecedores compartilhados.
 *
 * Nulo quando a API não responde — e a página **precisa** distinguir isso de um mapa
 * vazio, que significaria "ninguém divide fornecedor", uma afirmação bem diferente.
 */
export async function obterMapaDaRede(): Promise<MapaDaRede | null> {
  try {
    return await buscar<MapaDaRede>("/api/v1/rede");
  } catch {
    return null;
  }
}

export async function obterMapaDoEstado(): Promise<MapaDoEstado | null> {
  try {
    return await buscar<MapaDoEstado>("/api/v1/estado");
  } catch (erro) {
    console.warn(
      `[explanando] Mapa do Estado indisponível: ` +
        `${erro instanceof Error ? erro.message : erro}`,
    );

    return null;
  }
}

export async function listarMudancasDoEstado(
  quantidade = 30,
): Promise<MudancaDoEstado[] | null> {
  try {
    return await buscar<MudancaDoEstado[]>(`/api/v1/estado/mudancas?quantidade=${quantidade}`);
  } catch (erro) {
    console.warn(
      `[explanando] Mudanças do Estado indisponíveis: ` +
        `${erro instanceof Error ? erro.message : erro}`,
    );

    return null;
  }
}

/** Os filhos diretos de uma unidade. Um nível por vez — ver a nota do endpoint. */
export async function obterUnidadesDoEstado(codigo: number): Promise<NoDoEstado[] | null> {
  try {
    return await buscar<NoDoEstado[]>(`/api/v1/estado/${codigo}/unidades`);
  } catch {
    return null;
  }
}

export function obterUnidadeDoEstado(codigo: number): Promise<NoDoEstado | null> {
  return buscar<NoDoEstado>(`/api/v1/estado/${codigo}`);
}

/**
 * A serie mensal do quadro de pessoal — do Estado inteiro, ou do ramo de uma unidade.
 *
 * Lista vazia quando a fonte nao responde: um grafico ausente e' melhor que uma pagina
 * ausente, e o componente sabe nao se desenhar sem pontos suficientes.
 */
export async function obterSerieDePessoal(codigo?: number): Promise<PontoDaSerie[]> {
  try {
    const caminho =
      codigo === undefined ? "/api/v1/estado/pessoal" : `/api/v1/estado/${codigo}/pessoal`;

    return (await buscar<PontoDaSerie[]>(caminho)) ?? [];
  } catch {
    return [];
  }
}

/**
 * Os códigos das unidades que o mapa mostra, para o Next gerar as páginas estáticas.
 *
 * Mesmo raciocínio de `coletarIdsParaPreRender`: é a CDN, não o banco, que absorve o pico
 * (ADR-0001), e a CDN só serve o que já existe como arquivo. São ~336 páginas — os entes,
 * órgãos e entidades —, e não as 96 mil unidades: essas continuam sob demanda, porque
 * ninguém compartilha o link de uma coordenação de terceiro nível.
 *
 * Lista vazia quando a API não responde: o build precisa passar em CI sem backend no ar.
 */
export async function coletarCodigosDoEstado(): Promise<number[]> {
  const mapa = await obterMapaDoEstado();

  return mapa?.nos.map((no) => no.codigo) ?? [];
}
