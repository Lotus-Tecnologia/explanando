import { listarAchadosRecentes } from "@/lib/api";
import { montarFeed, respostaDoFeed } from "@/lib/rss";

/* Literal pelo mesmo motivo das páginas: o Next lê este export estaticamente. */
export const revalidate = 300;

/**
 * O feed geral: as despesas com achado que entraram na base por último, de todos os
 * parlamentares. Para quem acompanha a cota como assunto, e não um nome.
 */
export async function GET() {
  const achados = await listarAchadosRecentes(undefined, 50);

  return respostaDoFeed(
    montarFeed(
      {
        titulo: "Explanando — novos indicadores na cota parlamentar",
        descricao:
          "Despesas da cota parlamentar que entraram na base com ao menos um indicador do " +
          "motor de regras. Indicador descreve inconsistência entre registros públicos; não é " +
          "acusação.",
        caminhoDaPagina: "/",
        caminhoDoFeed: "/feed.xml",
      },
      achados ?? [],
    ),
  );
}
