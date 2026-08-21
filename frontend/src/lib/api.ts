import type {
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

async function buscar<T>(caminho: string): Promise<T | null> {
  const resposta = await fetch(`${BASE}${caminho}`, {
    next: { revalidate: SEGUNDOS_DE_REVALIDACAO },
    headers: { Accept: "application/json" },
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

export async function ranquear(
  ordenacao: OrdenacaoDoRanking = "volume",
  pagina = 1,
  itens = 50,
): Promise<Pagina<ItemDoRanking>> {
  const resultado = await buscar<Pagina<ItemDoRanking>>(
    `/api/v1/parlamentares?ordenar=${ordenacao}&pagina=${pagina}&itens=${itens}`,
  );

  // O ranking nunca é 404: ou responde, ou é falha de verdade.
  if (!resultado) {
    throw new ErroDaApi(404, "/api/v1/parlamentares");
  }

  return resultado;
}

export function obterPerfil(id: string): Promise<PerfilDoParlamentar | null> {
  return buscar<PerfilDoParlamentar>(`/api/v1/parlamentares/${id}`);
}

export async function listarDespesas(
  id: string,
  parametros: { comFlag?: boolean; ano?: number; pagina?: number; itens?: number } = {},
): Promise<Pagina<ItemDeDespesa> | null> {
  const consulta = new URLSearchParams();

  if (parametros.comFlag !== undefined) {
    consulta.set("comFlag", String(parametros.comFlag));
  }

  if (parametros.ano !== undefined) {
    consulta.set("ano", String(parametros.ano));
  }

  consulta.set("pagina", String(parametros.pagina ?? 1));
  consulta.set("itens", String(parametros.itens ?? 25));

  return buscar<Pagina<ItemDeDespesa>>(
    `/api/v1/parlamentares/${id}/despesas?${consulta.toString()}`,
  );
}
