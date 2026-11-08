import { listarAchadosRecentes } from "@/lib/api";
import { montarFeed, respostaDoFeed } from "@/lib/rss";

/* Literal pelo mesmo motivo das páginas: o Next lê este export estaticamente. */
export const revalidate = 300;

/**
 * O feed geral: as despesas com achado que entraram na base por último, de todos os
 * parlamentares. Para quem acompanha a cota como assunto, e não um nome.
 */
export async function GET() {
  let achados;

  try {
    achados = await listarAchadosRecentes(undefined, 50);
  } catch (erro) {
    // A API fora não pode virar um feed vazio: ele ficaria cinco minutos no cache, e o leitor
    // de RSS concluiria que os indicadores sumiram. 503 diz "volte depois", e também deixa o
    // build passar sem backend, como as páginas já passam.
    console.warn(
      `[explanando] Feed geral indisponível: ${erro instanceof Error ? erro.message : String(erro)}`,
    );

    return new Response("Feed temporariamente indisponível.", {
      status: 503,
      headers: { "Retry-After": "300", "Cache-Control": "no-store" },
    });
  }

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
