import type { ItemDoRanking } from "./contrato";
import { dobrar } from "./texto";

/** A dobra de `lib/texto`, sem as bordas: duas implementações de dobra divergem. */
export function normalizar(texto: string): string {
  return dobrar(texto).trim();
}

/**
 * Quem casa com o que foi digitado, até `limite`, na ordem em que a busca rápida mostra.
 *
 * **Todos os termos precisam casar** com nome (por pedaço), partido, UF ou casa.
 *
 * **Partido ou UF exatos vêm antes de pedaço do nome.** Medido em 25/09: "sp" trazia Dilceu
 * Sperafico, do PR, entre os primeiros, porque "sp" é pedaço do sobrenome.
 *
 * **As casas se alternam.** A lista chega com a Câmara na frente, e o corte dos 7 primeiros
 * fazia "PT" devolver só deputados — nenhum dos senadores do partido aparecia. A ordem dentro
 * de cada casa é a que chegou.
 */
export function buscarParlamentares(
  parlamentares: readonly ItemDoRanking[],
  consulta: string,
  limite: number,
  excluirId?: string,
): ItemDoRanking[] {
  const termo = normalizar(consulta);
  if (termo.length < 2) return [];

  const termos = termo.split(/\s+/).filter(Boolean);
  const exatos = new Map<ItemDoRanking, number>();

  for (const p of parlamentares) {
    if (p.id === excluirId) continue;

    const nome = normalizar(p.nomeParlamentar);
    const partido = normalizar(p.siglaPartido);
    const uf = normalizar(p.uf);
    const casa = normalizar(p.casa);

    const casaTodos = termos.every(
      (t) => nome.includes(t) || partido.includes(t) || uf === t || casa.includes(t),
    );

    if (casaTodos) exatos.set(p, termos.filter((t) => partido === t || uf === t).length);
  }

  // Estável: dentro do mesmo número de casamentos exatos, a ordem de chegada fica.
  const ordenados = [...exatos.keys()].sort((a, b) => exatos.get(b)! - exatos.get(a)!);

  const porCasa = new Map<string, ItemDoRanking[]>();
  for (const p of ordenados) porCasa.set(p.casa, [...(porCasa.get(p.casa) ?? []), p]);

  const filas = [...porCasa.values()];
  const resultado: ItemDoRanking[] = [];

  for (let i = 0; resultado.length < limite && filas.some((fila) => i < fila.length); i++) {
    for (const fila of filas) {
      if (i < fila.length && resultado.length < limite) resultado.push(fila[i]);
    }
  }

  return resultado;
}
