import { formatarNumero } from "./formato";

/*
  Frases com número, que precisam concordar com ele.

  Ficam aqui, e não no JSX, porque o singular muda a frase inteira e não só o substantivo:
  medido em 25/09 no perfil de Tasso Jereissati, "Os 1 achado existe e estão abaixo" e
  "Os 1 maiores de 1 fornecedores".
*/

/** A frase do perfil sem índice publicado, para os achados que existem mesmo assim. */
export function fraseDosAchadosAbaixo(achados: number): string {
  return achados === 1
    ? "O único achado existe e está abaixo, com a sua regra."
    : `Os ${formatarNumero(achados)} achados existem e estão abaixo, regra a regra.`;
}

/** O subtítulo da escada: quantos fornecedores a lista mostra, de quantos. */
export function fraseDosMaioresFornecedores(mostrados: number, total: number): string {
  if (mostrados >= total) {
    return total === 1
      ? "O único fornecedor, pelo total pago."
      : `Os ${formatarNumero(total)} fornecedores, pelo total pago.`;
  }

  return mostrados === 1
    ? `O maior de ${formatarNumero(total)} fornecedores, pelo total pago.`
    : `Os ${formatarNumero(mostrados)} maiores de ${formatarNumero(total)} fornecedores, pelo total pago.`;
}
