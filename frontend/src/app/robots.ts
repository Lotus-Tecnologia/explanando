import type { MetadataRoute } from "next";
import { URL_BASE, ehIndexavel } from "@/lib/site";

/**
 * O que o rastreador pode ler.
 *
 * Quando o site está completo, tudo é público e tudo pode ser indexado — indexação é o canal
 * de aquisição declarado no Business Model Canvas, e o produto não tem área logada nem
 * endereço que precise ficar fora do índice.
 *
 * **Enquanto não está**, nada entra. O build só produz as 513 páginas de perfil se a API
 * responder; sem ela, o que existe é uma home dizendo que o ranking não carregou e um sitemap
 * com quatro endereços. Deixar o buscador conhecer o site nesse estado gasta a primeira
 * impressão — e primeira impressão de índice é cara de refazer.
 *
 * A trava é `NEXT_PUBLIC_INDEXAVEL=true`, e o padrão é não indexar.
 */
export default function robots(): MetadataRoute.Robots {
  if (!ehIndexavel()) {
    return { rules: { userAgent: "*", disallow: "/" } };
  }

  return {
    rules: { userAgent: "*", allow: "/" },
    sitemap: `${URL_BASE}/sitemap.xml`,
  };
}
