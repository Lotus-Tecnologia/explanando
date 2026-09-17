/**
 * As constantes do site que precisam existir num lugar só.
 *
 * O endereço absoluto importa porque o sitemap e as tags de compartilhamento não aceitam
 * caminho relativo — e um sitemap com URL errada é pior que sitemap nenhum: o buscador
 * tenta indexar endereços que não existem.
 */
export const URL_BASE = (
  process.env.NEXT_PUBLIC_SITE_URL ?? "https://explanando.com.br"
).replace(/\/+$/, "");

/**
 * O Encarregado de Dados, exigido pelo ADR-0010 §2 no rodapé.
 *
 * A LGPD art. 41 §1 pede **identidade e** contato divulgados publicamente — só o e-mail não
 * cumpre. É por isso que o nome está aqui e não só o endereço.
 */
export const ENCARREGADO = {
  nome: "Gustavo Lira Criado",
  email: "privacidade@explanando.com.br",
} as const;

/**
 * Se o site pode ser indexado.
 *
 * **O padrão é NÃO**, e a inversão é deliberada. A indexação é o canal de aquisição principal
 * do produto (ADR-0001), e é justamente por isso que ela não pode começar cedo: um site
 * incompleto — a home dizendo que o ranking não carregou, o sitemap com 4 endereços em vez de
 * 517 — gasta a primeira impressão do buscador, e desfazer isso custa muito mais do que
 * esperar.
 *
 * Os dois erros possíveis não são simétricos. Esquecer de **ligar** é visível: você abre o
 * Search Console e não há nada. Indexar cedo é invisível até o estrago estar feito.
 *
 * Só a grafia exata `true` liga. "1", "sim", "yes" não ligam — uma única forma aceita torna a
 * intenção inequívoca, e evita que um valor herdado de outro lugar acione isso por acidente.
 *
 * Lida a cada chamada, e não uma vez na importação, para que quem decida seja o build.
 */
export function ehIndexavel(): boolean {
  return process.env.NEXT_PUBLIC_INDEXAVEL === "true";
}

/** As rotas que existem independentemente de a API responder no build. */
const ROTAS_FIXAS = [
  { caminho: "/", prioridade: 1 },

  // Seis rotas de ranking: tres ordenacoes VEZES duas casas. A raiz de cada casa e' o
  // ranking por volume dela, e as duas tem a mesma prioridade — o tamanho desigual das
  // casas nao vira hierarquia no sitemap.
  { caminho: "/camara", prioridade: 0.9 },
  { caminho: "/camara/indice", prioridade: 0.8 },
  { caminho: "/camara/gravidade", prioridade: 0.8 },
  { caminho: "/senado", prioridade: 0.9 },
  { caminho: "/senado/indice", prioridade: 0.8 },
  { caminho: "/senado/gravidade", prioridade: 0.8 },

  // A política é página pública e precisa ser encontrável — inclusive por quem quer exercer
  // um direito e não sabe por onde começar.
  { caminho: "/privacidade", prioridade: 0.3 },
] as const;

export interface RotaDoSitemap {
  url: string;
  lastModified: Date;
  changeFrequency: "monthly";
  priority: number;
}

/**
 * O sitemap: as rotas fixas mais uma por parlamentar.
 *
 * `changeFrequency` é mensal porque é essa a cadência real — o ADR-0010 §4 define uma
 * varredura global mensal de todos os 513, e mentir para o buscador sobre a frequência
 * gasta orçamento de rastreamento em páginas que não mudaram.
 *
 * Sem ids — API fora no build — as rotas fixas continuam saindo. Um sitemap vazio pede ao
 * buscador que desindexe o site inteiro, e indexação é o motor de crescimento do produto
 * (ADR-0001).
 */
export function rotasDoSitemap(ids: readonly string[], agora: Date): RotaDoSitemap[] {
  const rotas: RotaDoSitemap[] = ROTAS_FIXAS.map((rota) => ({
    url: `${URL_BASE}${rota.caminho}`,
    lastModified: agora,
    changeFrequency: "monthly",
    priority: rota.prioridade,
  }));

  for (const id of new Set(ids)) {
    rotas.push({
      url: `${URL_BASE}/parlamentares/${id}`,
      lastModified: agora,
      changeFrequency: "monthly",
      priority: 0.6,
    });
  }

  return rotas;
}
