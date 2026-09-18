/**
 * O retrato oficial, num endereço que uma página segura consegue carregar.
 *
 * **O Senado publica a foto em `http://`.** Verificado na base: os 118 senadores têm
 * `http://www.senado.leg.br/senadores/img/fotos-oficiais/senadorNNNN.jpg`, enquanto a Câmara
 * já entrega `https://`. Numa página servida por HTTPS, o navegador bloqueia a imagem como
 * conteúdo misto — e o retrato some sem erro visível, que é a pior forma de falhar.
 *
 * Elevar o esquema é seguro aqui porque o destino é o mesmo recurso: o endereço seguro
 * responde 301 para `legis.senado.leg.br/senadores/fotos-oficiais/NNNN`, que devolve a
 * imagem. Medido, não suposto.
 *
 * A correção existe em dois lugares de propósito. O cliente do Senado passou a gravar
 * `https`, o que conserta as coletas futuras; esta função conserta as 118 linhas que já
 * estão na base sem exigir uma recoleta só por causa do esquema de uma URL.
 */

/** Só domínios do poder público. Elevar o esquema de um host arbitrário não é nossa conta. */
const HOSTS_OFICIAIS = ["www.senado.leg.br", "legis.senado.leg.br", "www.camara.leg.br"];

export function retratoSeguro(url: string | null): string | null {
  if (!url) return null;

  if (!url.startsWith("http://")) return url;

  const semEsquema = url.slice("http://".length);
  const host = semEsquema.split("/")[0];

  return HOSTS_OFICIAIS.includes(host) ? `https://${semEsquema}` : null;
}

/**
 * As iniciais, para quando não há retrato.
 *
 * O buraco precisa ser preenchido com alguma coisa: uma célula vazia faz a linha parecer
 * quebrada, e um ícone genérico de pessoa repetido dezenas de vezes vira ruído. As iniciais
 * são únicas por pessoa e legíveis em 32 pixels.
 */
export function iniciais(nome: string): string {
  const partes = nome
    .trim()
    .split(/\s+/)
    .filter((parte) => parte.length > 2);

  if (partes.length === 0) return nome.slice(0, 2).toUpperCase();
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();

  return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
}
