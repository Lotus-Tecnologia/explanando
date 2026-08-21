import type { Metadata } from "next";
import { PaginaDoRanking } from "@/componentes/PaginaDoRanking";
import { ordenacaoPorChave } from "@/lib/ordenacoes";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

const VOLUME = ordenacaoPorChave("volume")!;

export const metadata: Metadata = {
  title: VOLUME.titulo,
  description: VOLUME.descricao,
  alternates: { canonical: VOLUME.caminho },
};

/**
 * A home é o ranking por volume.
 *
 * Volume, e não densidade, por decisão do ADR-0009: é o número mais seguro juridicamente do
 * produto inteiro — gastar a cota é legal, e publicar quanto se gastou não imputa conduta —
 * e é o que funciona como moeda social. "R$ 1,2 milhão" se compartilha; "índice 3,21" não
 * significa nada para quem chega de um link.
 *
 * Estática: era dinâmica enquanto lia `?ordenar=` da query string, e a home é exatamente
 * onde o pico viral bate.
 */
export default function Home() {
  return <PaginaDoRanking ordenacao={VOLUME} />;
}
