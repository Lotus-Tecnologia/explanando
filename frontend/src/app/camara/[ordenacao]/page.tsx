import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PaginaDoRanking } from "@/componentes/PaginaDoRanking";
import { casaPorChave } from "@/lib/casas";
import { ORDENACOES, caminhoDoRanking, ordenacaoPorChave } from "@/lib/ordenacoes";

/* Ver a nota em ../page.tsx: precisa ser literal. */
export const revalidate = 300;

const CASA = casaPorChave("camara")!;

/** Só as ordenações que não são a raiz — o volume mora em `/camara`. */
export function generateStaticParams() {
  return ORDENACOES.filter((ordenacao) => ordenacao.sufixo !== "").map((ordenacao) => ({
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
    return {};
  }

  return {
    title: `${ordenacao.titulo} — ${CASA.nomeOficial}`,
    description: `${ordenacao.descricao} Cota ${CASA.cota}.`,
    alternates: { canonical: caminhoDoRanking(CASA, ordenacao) },
  };
}

export default async function Pagina({
  params,
}: {
  params: Promise<{ ordenacao: string }>;
}) {
  const { ordenacao: chave } = await params;
  const ordenacao = ordenacaoPorChave(chave);

  if (!ordenacao || ordenacao.sufixo === "") {
    notFound();
  }

  return <PaginaDoRanking casa={CASA} ordenacao={ordenacao} />;
}
