import { listarAchadosRecentes, obterPerfil } from "@/lib/api";
import { montarFeed, respostaDoFeed } from "@/lib/rss";

/* Literal pelo mesmo motivo das páginas: o Next lê este export estaticamente. */
export const revalidate = 300;

/**
 * O feed de um parlamentar: as despesas dele com achado, das que entraram por último.
 *
 * É o primeiro degrau da recorrência — quem quer acompanhar alguém acompanha sem cadastro,
 * sem e-mail e sem deixar dado pessoal nenhum com a gente.
 */
export async function GET(_pedido: Request, contexto: { params: Promise<{ id: string }> }) {
  const { id } = await contexto.params;
  const [perfil, achados] = await Promise.all([obterPerfil(id), listarAchadosRecentes(id, 50)]);

  if (!perfil || !achados) {
    return new Response("Parlamentar não encontrado.", { status: 404 });
  }

  return respostaDoFeed(
    montarFeed(
      {
        titulo: `Explanando — ${perfil.nomeParlamentar} (${perfil.siglaPartido}-${perfil.uf})`,
        descricao:
          `Despesas da cota parlamentar de ${perfil.nomeParlamentar} que entraram na base com ao ` +
          "menos um indicador do motor de regras. Indicador descreve inconsistência entre " +
          "registros públicos; não é acusação.",
        caminhoDaPagina: `/parlamentares/${id}`,
        caminhoDoFeed: `/parlamentares/${id}/feed.xml`,
      },
      achados,
    ),
  );
}
