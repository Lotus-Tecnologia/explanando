"use client";

import { useMemo } from "react";


import type { ItemDoRanking } from "@/lib/contrato";
import type { Casa } from "@/lib/casas";
import type { Ordenacao } from "@/lib/ordenacoes";
import {
  formatarDensidade,
  formatarNumero,
  formatarPorcentagem,
  formatarReais,
} from "@/lib/formato";
import { ContagensDeGravidade } from "./CartaoDePessoa";

interface DashboardDaCasaProps {
  casa: Casa;
  itens: readonly ItemDoRanking[];

  /**
   * A casa inteira, para os totais. As ordenações por densidade deixam de fora quem não tem
   * amostra — e somar o volume só de quem ficou fazia a Câmara "gastar" R$ 799,9 milhões numa
   * aba e R$ 802,4 milhões na outra. O pódio segue a ordenação; os totais, não.
   */
  base?: readonly ItemDoRanking[];
  ordenacao?: Ordenacao;
  filtroPartidoAtivo?: string;
  filtroUfAtivo?: string;
  onFiltrarPartido?: (partido: string) => void;
  onFiltrarUf?: (uf: string) => void;
}

export function DashboardDaCasa({
  casa,
  itens,
  base,
  ordenacao,
  filtroPartidoAtivo,
  filtroUfAtivo,
  onFiltrarPartido,
  onFiltrarUf,
}: DashboardDaCasaProps) {
  // Cálculos consolidados da casa
  const estatisticas = useMemo(() => {
    if (itens.length === 0) return null;

    const todos = base && base.length > 0 ? base : itens;

    const volumeTotal = todos.reduce((soma, i) => soma + i.volumeTotalGasto, 0);
    const despesasTotal = todos.reduce((soma, i) => soma + i.despesasAvaliadas, 0);
    const flagsAltas = todos.reduce((soma, i) => soma + i.flagsAltas, 0);
    const flagsMedias = todos.reduce((soma, i) => soma + i.flagsMedias, 0);
    const flagsBaixas = todos.reduce((soma, i) => soma + i.flagsBaixas, 0);
    const totalIndicadores = flagsAltas + flagsMedias + flagsBaixas;

    const mediaPorParlamentar = volumeTotal / todos.length;
    const taxaIndicadores = despesasTotal > 0 ? (totalIndicadores / despesasTotal) * 100 : 0;

    // Cálculo da Mediana
    const volumesOrdenados = [...todos]
      .map((i) => i.volumeTotalGasto)
      .sort((a, b) => a - b);
    const meio = Math.floor(volumesOrdenados.length / 2);
    const mediana =
      volumesOrdenados.length % 2 === 1
        ? volumesOrdenados[meio]
        : (volumesOrdenados[meio - 1] + volumesOrdenados[meio]) / 2;

    // Agrupamento por Partido
    const mapaPartidos = new Map<string, { total: number; qtd: number }>();
    for (const item of todos) {
      const p = item.siglaPartido.trim() || "Sem Partido";
      const atual = mapaPartidos.get(p) ?? { total: 0, qtd: 0 };
      mapaPartidos.set(p, {
        total: atual.total + item.volumeTotalGasto,
        qtd: atual.qtd + 1,
      });
    }
    const topPartidos = Array.from(mapaPartidos.entries())
      .map(([sigla, dados]) => ({
        sigla,
        total: dados.total,
        qtd: dados.qtd,
        media: dados.total / dados.qtd,
      }))
      .sort((a, b) => b.total - a.total)
      .slice(0, 5);

    // Agrupamento por UF
    const mapaUfs = new Map<string, { total: number; qtd: number }>();
    for (const item of todos) {
      const uf = item.uf.trim() || "BR";
      const atual = mapaUfs.get(uf) ?? { total: 0, qtd: 0 };
      mapaUfs.set(uf, {
        total: atual.total + item.volumeTotalGasto,
        qtd: atual.qtd + 1,
      });
    }
    const topUfs = Array.from(mapaUfs.entries())
      .map(([uf, dados]) => ({
        uf,
        total: dados.total,
        qtd: dados.qtd,
      }))
      .sort((a, b) => b.total - a.total)
      .slice(0, 5);


    return {
      volumeTotal,
      despesasTotal,
      mediaPorParlamentar,
      mediana,
      flagsAltas,
      flagsMedias,
      flagsBaixas,
      totalIndicadores,
      taxaIndicadores,
      topPartidos,
      topUfs,
  
    };
  }, [itens, base]);

  if (!estatisticas) {
    return (
      <div className="rounded-cartao border border-dashed border-borda-suave bg-surface p-6 text-center text-texto-suave">
        <p className="font-semibold text-titulo">Base de dados em sincronização</p>
        <p className="mt-1 text-xs">
          Os registros desta casa estão sendo consolidados. Os dashboards analíticos serão
          apresentados assim que a carga for concluída.
        </p>
      </div>
    );
  }

  const {
    volumeTotal,
    despesasTotal,
    mediaPorParlamentar,
    mediana,
    flagsAltas,
    flagsMedias,
    flagsBaixas,
    totalIndicadores,
    taxaIndicadores,
    topPartidos,
    topUfs,

  } = estatisticas;

  const maxVolumePartido = Math.max(...topPartidos.map((p) => p.total), 1);
  const maxVolumeUf = Math.max(...topUfs.map((u) => u.total), 1);

  return (
    <section
      aria-label={`Painel — ${casa.nomeOficial}`}
      className="mt-6 grid gap-4 lg:grid-cols-2"
    >
      <div className="rounded-cartao border border-borda-suave bg-surface p-5">
        <h2 className="font-semibold text-texto-suave">Gasto da cota na casa</h2>
        <p className="numeros mt-1 font-display text-[1.875rem] leading-tight font-bold whitespace-nowrap text-titulo">
          {formatarReais(volumeTotal)}
        </p>
        <p className="mt-2 text-texto-suave">
          Em <strong className="numeros text-texto">{formatarNumero(despesasTotal)}</strong>{" "}
          despesas. Média de{" "}
          <strong className="numeros text-texto">{formatarReais(mediaPorParlamentar)}</strong> por
          parlamentar; metade gastou menos de{" "}
          <strong className="numeros text-texto">{formatarReais(mediana)}</strong>.
        </p>
      </div>

      <div className="rounded-cartao border border-borda-suave bg-surface p-5">
        <h2 className="font-semibold text-texto-suave">O que o motor encontrou</h2>
        {totalIndicadores > 0 ? (
          <>
            <div className="mt-2">
              <ContagensDeGravidade item={{ flagsAltas, flagsMedias, flagsBaixas }} />
            </div>
            <div
              className="mt-3 flex h-2.5 gap-0.5 overflow-hidden rounded-full"
              role="img"
              aria-label={`Distribuição: ${formatarNumero(flagsAltas)} altas, ${formatarNumero(
                flagsMedias,
              )} médias, ${formatarNumero(flagsBaixas)} baixas.`}
            >
              {flagsAltas > 0 && (
                <span className="bg-alta-texto" style={{ width: `${(flagsAltas / totalIndicadores) * 100}%` }} />
              )}
              {flagsMedias > 0 && (
                <span className="bg-media-texto" style={{ width: `${(flagsMedias / totalIndicadores) * 100}%` }} />
              )}
              {flagsBaixas > 0 && (
                <span className="bg-baixa-texto" style={{ width: `${(flagsBaixas / totalIndicadores) * 100}%` }} />
              )}
            </div>
            {/*
              Calculado, e não escrito: o "3,3%" fixo era da Câmara e aparecia igual no Senado.
              E é a parte dos ACHADOS, não das despesas.
            */}
            <p className="mt-3 text-texto-suave">
              <strong className="numeros text-texto">{formatarNumero(totalIndicadores)}</strong>{" "}
              achados, {formatarDensidade(taxaIndicadores)} a cada 100 despesas.{" "}
              <strong className="numeros text-texto">
                {formatarPorcentagem(flagsAltas / totalIndicadores)}
              </strong>{" "}
              deles são de gravidade alta.
            </p>
          </>
        ) : (
          <p className="mt-2 text-texto-suave">Nenhum achado nesta casa.</p>
        )}
      </div>

      <BarrasDeFiltro
        titulo="Maiores bancadas por gasto"
        rotuloDoGrupo="Filtrar a lista por partido"
        itens={topPartidos.map((p) => ({
          chave: p.sigla,
          detalhe: `${p.qtd} ${p.qtd === 1 ? "parlamentar" : "parlamentares"}`,
          total: p.total,
        }))}
        maximo={maxVolumePartido}
        ativo={filtroPartidoAtivo}
        aoEscolher={onFiltrarPartido}
      />

      {/*
        Pessoas com despesa, e não cadeiras: titular e suplente que se revezaram contam os
        dois. Chamar isso de "vagas" punha Roraima com 5 no Senado (auditoria de 25/09).
      */}
      <BarrasDeFiltro
        titulo="Estados que mais gastaram"
        rotuloDoGrupo="Filtrar a lista por estado"
        itens={topUfs.map((u) => ({ chave: u.uf, detalhe: `${u.qtd} com despesa`, total: u.total }))}
        maximo={maxVolumeUf}
        ativo={filtroUfAtivo}
        aoEscolher={onFiltrarUf}
      />

      {/* O pódio saiu: a lista logo abaixo já começa pelos três primeiros da ordenação. */}
      <p className="sr-only">
        {ordenacao ? `Ordenação atual: ${ordenacao.rotulo}.` : null}
      </p>
    </section>
  );
}

/** Barras proporcionais que também filtram a lista; o botão ativo fica marcado e anunciado. */
function BarrasDeFiltro({
  titulo,
  rotuloDoGrupo,
  itens,
  maximo,
  ativo,
  aoEscolher,
}: {
  titulo: string;
  rotuloDoGrupo: string;
  itens: { chave: string; detalhe: string; total: number }[];
  maximo: number;
  ativo?: string;
  aoEscolher?: (chave: string) => void;
}) {
  return (
    <div className="rounded-cartao border border-borda-suave bg-surface p-5">
      <h2 className="font-semibold text-texto-suave">{titulo}</h2>
      <p className="mt-0.5 text-sm text-texto-suave">Toque numa barra para filtrar a lista.</p>

      <div role="group" aria-label={rotuloDoGrupo} className="mt-3 flex flex-col gap-1">
        {itens.map((item) => {
          const marcado = ativo === item.chave;

          return (
            <button
              type="button"
              key={item.chave}
              aria-pressed={marcado}
              onClick={() => aoEscolher?.(marcado ? "" : item.chave)}
              className={`flex min-h-11 flex-col justify-center gap-1.5 rounded-campo px-2 py-1.5 text-left ${
                marcado ? "bg-realce" : "hover:bg-fundo"
              }`}
            >
              <span className="flex w-full items-baseline justify-between gap-3">
                <span>
                  <span className="font-semibold text-texto">{item.chave}</span>{" "}
                  <span className="text-sm text-texto-suave">{item.detalhe}</span>
                </span>
                <span className="numeros font-semibold whitespace-nowrap text-texto">
                  {formatarReais(item.total)}
                </span>
              </span>
              <span aria-hidden="true" className="block h-1.5 w-full rounded-full bg-realce">
                <span
                  className={`block h-full rounded-full ${marcado ? "bg-titulo" : "bg-acento"}`}
                  style={{ width: `${Math.max((item.total / maximo) * 100, 4)}%` }}
                />
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
