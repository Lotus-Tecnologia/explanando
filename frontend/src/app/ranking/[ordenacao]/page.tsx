import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PaginaDoRanking } from "@/componentes/PaginaDoRanking";
import { ORDENACOES, ordenacaoPorChave } from "@/lib/ordenacoes";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

/**
 * Só as ordenações que não são a home — o volume vive em `/`, para o endereço mais
 * compartilhável do site ser o mais curto.
 */
export function generateStaticParams() {
  return ORDENACOES.filter((ordenacao) => ordenacao.caminho !== "/").map((ordenacao) => ({
    ordenacao: ordenacao.chave,
  }));
}

/**
 * Chave desconhecida é 404, nunca o ranking por volume.
 *
 * É a mesma escolha que a API faz ao devolver 400 para ordenação inventada: entregar outra
 * coisa em silêncio faria o leitor achar que estava vendo o ranking que pediu.
 */
export const dynamicParams = false;

export async function generateMetadata({
  params,
}: {
  params: Promise<{ ordenacao: string }>;
}): Promise<Metadata> {
  const { ordenacao: chave } = await params;
  const ordenacao = ordenacaoPorChave(chave);

  if (!ordenacao) {
    return { title: "Ordenação desconhecida" };
  }

  return {
    title: ordenacao.titulo,
    description: ordenacao.descricao,
    alternates: { canonical: ordenacao.caminho },
  };
}

export default async function Ranking({
  params,
}: {
  params: Promise<{ ordenacao: string }>;
}) {
  const { ordenacao: chave } = await params;
  const ordenacao = ordenacaoPorChave(chave);

  if (!ordenacao) {
    notFound();
  }

  return <PaginaDoRanking ordenacao={ordenacao} />;
}
