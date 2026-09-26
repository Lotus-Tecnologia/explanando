import type { ItemDoRanking } from "./contrato";

export interface DistribuicaoDeGravidade {
  alta: number;
  media: number;
  baixa: number;
  total: number;
}

/**
 * Que parte dos achados é de cada gravidade, somando as casas recebidas.
 *
 * **Calculada, e não escrita.** A home trazia 43,3% / 53,4% / 3,3% fixos; a base, na
 * auditoria de 25/09, dava 41,9% / 55,1% / 3,0% — e o 3,3% era da Câmara, repetido no Senado.
 *
 * Nulo quando falta uma das casas (a parte de uma casa não é a da base) ou quando não há
 * achado nenhum: aí não existe distribuição, e "0%" seria uma afirmação.
 */
export function distribuicaoDeGravidade(
  listas: (ItemDoRanking[] | null)[],
): DistribuicaoDeGravidade | null {
  if (listas.some((lista) => lista === null)) return null;

  const itens = listas.flatMap((lista) => lista ?? []);
  const soma = (campo: "flagsAltas" | "flagsMedias" | "flagsBaixas") =>
    itens.reduce((total, item) => total + item[campo], 0);

  const alta = soma("flagsAltas");
  const media = soma("flagsMedias");
  const baixa = soma("flagsBaixas");
  const total = alta + media + baixa;

  return total === 0
    ? null
    : { alta: alta / total, media: media / total, baixa: baixa / total, total };
}
