"use client";

import { useState } from "react";
import type { ItemDoRanking } from "@/lib/contrato";
import type { Casa } from "@/lib/casas";
import type { Ordenacao } from "@/lib/ordenacoes";
import { DashboardDaCasa } from "./DashboardDaCasa";
import { TabelaDoRanking } from "./TabelaDoRanking";

interface ExploradorDaCasaProps {
  casa: Casa;
  itens: ItemDoRanking[];

  /** A casa inteira, para os totais do painel. Ver `DashboardDaCasa`. */
  base?: ItemDoRanking[];
  ordenacao: Ordenacao;
}

export function ExploradorDaCasa({
  casa,
  itens,
  base,
  ordenacao,
}: ExploradorDaCasaProps) {
  const [filtroPartido, setFiltroPartido] = useState("");
  const [filtroUf, setFiltroUf] = useState("");

  function tratarFiltrarPartido(partido: string) {
    setFiltroPartido(partido);
    rolarParaTabela();
  }

  function tratarFiltrarUf(uf: string) {
    setFiltroUf(uf);
    rolarParaTabela();
  }

  function rolarParaTabela() {
    // Pequeno atraso para a renderização do estado se propagar
    setTimeout(() => {
      const elemento = document.getElementById("listagem-tabela");
      if (elemento) {
        elemento.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    }, 50);
  }

  return (
    <div className="flex flex-col gap-10">
      {/* 1. Dashboards analíticos no topo */}
      <DashboardDaCasa
        casa={casa}
        itens={itens}
        base={base}
        ordenacao={ordenacao}
        filtroPartidoAtivo={filtroPartido}
        filtroUfAtivo={filtroUf}
        onFiltrarPartido={tratarFiltrarPartido}
        onFiltrarUf={tratarFiltrarUf}
      />

      {/*
        2. Área da Tabela e Auditoria Individual.

        Só com gente para listar. Sem ninguém, a busca e os filtros com "(0)" em cada opção
        viravam um formulário que não filtra nada — o painel acima já diz que a base está em
        sincronização.
      */}
      {itens.length === 0 ? null : (
        <section
          id="listagem-tabela"
          aria-labelledby="titulo-listagem"
          className="border-t border-borda-suave pt-8 scroll-mt-20"
        >
          <div className="flex flex-col gap-1">
            <h2
              id="titulo-listagem"
              className="font-display text-xl sm:text-2xl font-bold tracking-tight text-titulo"
            >
              Listagem e Auditoria Individual
            </h2>
            <p className="text-sm text-texto-suave max-w-3xl">
              Explore cada parlamentar da base, filtre por partido ou unidade da
              federação, reordene as colunas e consulte as notas fiscais
              associadas.
            </p>
          </div>

          <TabelaDoRanking
            itens={itens}
            rotulo={ordenacao.rotulo}
            filtroPartidoExterno={filtroPartido}
            onMudarFiltroPartido={setFiltroPartido}
            filtroUfExterno={filtroUf}
            onMudarFiltroUf={setFiltroUf}
          />
        </section>
      )}
    </div>
  );
}
