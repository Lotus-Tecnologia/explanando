import type { Metadata } from "next";
import { PaginaDoRanking } from "@/componentes/PaginaDoRanking";
import { casaPorChave } from "@/lib/casas";
import { caminhoDoRanking, ordenacaoPorChave } from "@/lib/ordenacoes";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export".
*/
export const revalidate = 300;

const CASA = casaPorChave("senado")!;
const VOLUME = ordenacaoPorChave("volume")!;

export const metadata: Metadata = {
  title: `${VOLUME.titulo} — ${CASA.nomeOficial}`,
  description: `${VOLUME.descricao} Cota ${CASA.cota}, dos senadores que exerceram mandato na legislatura.`,
  alternates: { canonical: caminhoDoRanking(CASA, VOLUME) },
};

/**
 * A raiz da casa é o ranking por volume dela.
 *
 * Volume, e não densidade, por decisão do ADR-0009: é o número mais seguro juridicamente do
 * produto inteiro — gastar a cota é legal, e publicar quanto se gastou não imputa conduta —
 * e é o que funciona como moeda social. "R$ 1,2 milhão" se compartilha; "índice 3,21" não
 * significa nada para quem chega de um link.
 */
export default function Pagina() {
  return <PaginaDoRanking casa={CASA} ordenacao={VOLUME} />;
}
