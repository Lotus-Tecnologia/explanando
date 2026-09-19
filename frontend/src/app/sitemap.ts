import type { MetadataRoute } from "next";
import { coletarCodigosDoEstado, coletarIdsParaPreRender } from "@/lib/api";
import { rotasDoSitemap } from "@/lib/site";

/**
 * O sitemap, gerado no build a partir dos mesmos ids que viram páginas estáticas.
 *
 * Reaproveitar `coletarIdsParaPreRender` não é economia de código: é o que garante que o
 * sitemap e as páginas geradas nunca divirjam. Duas listas construídas separadamente
 * acabariam anunciando ao buscador uma página que o build não produziu.
 */
export const revalidate = 86400;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const [ids, unidades] = await Promise.all([
    coletarIdsParaPreRender(),
    coletarCodigosDoEstado(),
  ]);

  return rotasDoSitemap(ids, new Date(), unidades);
}
