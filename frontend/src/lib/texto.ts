/**
 * Comparação de texto para busca.
 *
 * Fica aqui, e não dentro de um componente, porque mais de uma tela busca por nome e
 * **duas implementações de dobra divergem**: uma passa a aceitar "orgao" por "órgão" e a
 * outra não, e ninguém descobre até alguém reclamar que a busca "não acha".
 */

/**
 * Dobra acento e caixa.
 *
 * `NFD` separa a letra da marca de acento e `\p{M}` descarta as marcas — "Órgão" vira
 * "orgao". Aqui isso funciona: é o motor do navegador, não o .NET com
 * `InvariantGlobalization`, onde `Normalize` é inerte (ver `docs/contratos/pessoal.md`).
 */
export function dobrar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase();
}

/**
 * Se o termo aparece no texto, ignorando acento e caixa.
 *
 * Termo vazio casa com tudo — quem chama decide se isso é lista cheia ou lista vazia.
 */
export function contem(texto: string, termo: string): boolean {
  return dobrar(texto).includes(dobrar(termo));
}

/**
 * Onde o termo casa, para o trecho poder ser destacado.
 *
 * Devolve índices sobre o texto **original**, e é por isso que a dobra precisa preservar o
 * comprimento: `NFD` alonga a string, então a posição é calculada sobre a forma dobrada
 * **sem** as marcas e reprojetada. Como cada caractere original vira exatamente um
 * caractere dobrado depois do descarte das marcas, os índices coincidem.
 */
export function ondeCasa(texto: string, termo: string): [number, number] | null {
  if (termo.trim().length === 0) return null;

  const inicio = dobrar(texto).indexOf(dobrar(termo));

  return inicio < 0 ? null : [inicio, inicio + dobrar(termo).length];
}
