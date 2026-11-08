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

/** Siglas que aparecem nos tipos de despesa e continuam em maiúsculas. */
const SIGLAS = new Set(["CEAP", "CEAPS", "SIGEPA", "RPA", "CNPJ", "TV", "ISS", "PJ", "PF"]);

/**
 * O tipo de despesa em caixa de frase.
 *
 * A Câmara manda "COMBUSTÍVEIS E LUBRIFICANTES." em caixa alta, e caixa alta num título é
 * lida como ênfase. O Senado já manda em caixa mista, e esse texto passa intacto.
 */
export function caixaDeFrase(texto: string): string {
  const limpo = texto.trim().replace(/\.$/, "");
  if (limpo !== limpo.toUpperCase()) return limpo;

  const minusculo = limpo
    .split(" ")
    .map((palavra) => (SIGLAS.has(palavra) ? palavra : palavra.toLocaleLowerCase("pt-BR")))
    .join(" ");

  return SIGLAS.has(minusculo.split(" ")[0])
    ? minusculo
    : minusculo.charAt(0).toLocaleUpperCase("pt-BR") + minusculo.slice(1);
}

/**
 * O documento do fornecedor que pode ir para a tela: só CNPJ, que é de pessoa jurídica e
 * público. CPF não aparece nem mascarado — fornecedor pessoa física é gente que não é agente
 * público, e um CPF parcial ainda ajuda a identificar alguém.
 */
export function documentoPublicavel(documento: string): string {
  return documento.replace(/\D/g, "").length === 14 && !documento.includes("*")
    ? documento
    : "Pessoa física (documento não exibido)";
}
