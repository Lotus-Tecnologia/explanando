"use client";

import { useId } from "react";
import type { ItemDoRanking } from "@/lib/contrato";
import { caminhoDaComparacao } from "@/lib/comparacao";
import { BuscaRapidaHome } from "./BuscaRapidaHome";

/**
 * "Comparar com…", no perfil. A mesma busca da página inicial, com outro destino.
 *
 * Só oferece colegas da **mesma casa** — a lista vem do ranking dela —, então o par misto,
 * que o comparador recusa, nem chega a ser proposto aqui.
 */
export function CompararCom({
  id,
  nome,
  colegas,
  integrante,
}: {
  id: string;
  nome: string;
  colegas: readonly ItemDoRanking[];
  integrante: string;
}) {
  const idDoCampo = useId();

  return (
    <div className="flex flex-col gap-2 border border-borda-suave bg-surface p-3 sm:flex-row sm:items-center sm:gap-4">
      <label htmlFor={idDoCampo} className="shrink-0 text-sm font-semibold text-titulo">
        Comparar {nome} com outro {integrante}
      </label>
      <BuscaRapidaHome
        parlamentares={colegas}
        excluirId={id}
        idDoCampo={idDoCampo}
        caminhoDe={(colega) => caminhoDaComparacao(id, colega.id)}
        placeholder="Nome, partido ou UF"
      />
    </div>
  );
}
