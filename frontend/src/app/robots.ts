import type { MetadataRoute } from "next";
import { URL_BASE } from "@/lib/site";

/**
 * O que o rastreador pode ler.
 *
 * Tudo é público e tudo pode ser indexado — indexação é o motor de crescimento declarado no
 * Business Model Canvas, e o produto não tem área logada nem endereço que precise ficar fora
 * do índice.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/" },
    sitemap: `${URL_BASE}/sitemap.xml`,
  };
}
