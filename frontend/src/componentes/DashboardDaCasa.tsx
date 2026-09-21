"use client";

import { useMemo } from "react";
import Image from "next/image";
import Link from "next/link";
import type { ItemDoRanking } from "@/lib/contrato";
import type { Casa } from "@/lib/casas";
import type { Ordenacao } from "@/lib/ordenacoes";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";

interface DashboardDaCasaProps {
  casa: Casa;
  itens: readonly ItemDoRanking[];
  ordenacao?: Ordenacao;
  filtroPartidoAtivo?: string;
  filtroUfAtivo?: string;
  onFiltrarPartido?: (partido: string) => void;
  onFiltrarUf?: (uf: string) => void;
}

export function DashboardDaCasa({
  casa,
  itens,
  ordenacao,
  filtroPartidoAtivo,
  filtroUfAtivo,
  onFiltrarPartido,
  onFiltrarUf,
}: DashboardDaCasaProps) {
  // Cálculos consolidados da casa
  const estatisticas = useMemo(() => {
    if (itens.length === 0) return null;

    const volumeTotal = itens.reduce((soma, i) => soma + i.volumeTotalGasto, 0);
    const despesasTotal = itens.reduce((soma, i) => soma + i.despesasAvaliadas, 0);
    const flagsAltas = itens.reduce((soma, i) => soma + i.flagsAltas, 0);
    const flagsMedias = itens.reduce((soma, i) => soma + i.flagsMedias, 0);
    const flagsBaixas = itens.reduce((soma, i) => soma + i.flagsBaixas, 0);
    const totalIndicadores = flagsAltas + flagsMedias + flagsBaixas;

    const mediaPorParlamentar = volumeTotal / itens.length;
    const taxaIndicadores = despesasTotal > 0 ? (totalIndicadores / despesasTotal) * 100 : 0;

    // Cálculo da Mediana
    const volumesOrdenados = [...itens]
      .map((i) => i.volumeTotalGasto)
      .sort((a, b) => a - b);
    const meio = Math.floor(volumesOrdenados.length / 2);
    const mediana =
      volumesOrdenados.length % 2 === 1
        ? volumesOrdenados[meio]
        : (volumesOrdenados[meio - 1] + volumesOrdenados[meio]) / 2;

    // Agrupamento por Partido
    const mapaPartidos = new Map<string, { total: number; qtd: number }>();
    for (const item of itens) {
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
    for (const item of itens) {
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

    // Pódio dos 3 primeiros da ordenação atual
    const podio = itens.slice(0, 3);

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
      podio,
    };
  }, [itens]);

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
    podio,
  } = estatisticas;

  const maxVolumePartido = Math.max(...topPartidos.map((p) => p.total), 1);
  const maxVolumeUf = Math.max(...topUfs.map((u) => u.total), 1);

  return (
    <section aria-label={`Painel Analítico — ${casa.nomeOficial}`} className="mt-6 flex flex-col gap-6">
      {/* ---------------------------------------------------------------- */}
      {/*  LINHA 1: KPIs EXECUTIVOS                                        */}
      {/* ---------------------------------------------------------------- */}
      <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {/* KPI 1: Volume Total */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-xs transition-all hover:border-borda-estrutural">
          <div>
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              Volume total gasto
            </dt>
            <dd className="numeros mt-2 font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl">
              {formatarReais(volumeTotal)}
            </dd>
          </div>
          <p className="mt-2 text-xs text-texto-suave border-t border-borda-suave/50 pt-2">
            Média de <strong className="numeros text-texto">{formatarReais(mediaPorParlamentar)}</strong> por parlamentar
          </p>
        </div>

        {/* KPI 2: Despesas Auditadas */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-xs transition-all hover:border-borda-estrutural">
          <div>
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              Despesas auditadas
            </dt>
            <dd className="numeros mt-2 font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl">
              {formatarNumero(despesasTotal)}
            </dd>
          </div>
          <p className="mt-2 text-xs text-texto-suave border-t border-borda-suave/50 pt-2">
            Notas fiscais, bilhetes aéreos e recibos
          </p>
        </div>

        {/* KPI 3: Gasto Mediano */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-xs transition-all hover:border-borda-estrutural">
          <div>
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              Gasto mediano
            </dt>
            <dd className="numeros mt-2 font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl">
              {formatarReais(mediana)}
            </dd>
          </div>
          <p className="mt-2 text-xs text-texto-suave border-t border-borda-suave/50 pt-2">
            50% dos parlamentares gastaram menos que este valor
          </p>
        </div>

        {/* KPI 4: Taxa de Indicadores */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-xs transition-all hover:border-borda-estrutural">
          <div>
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              Taxa de anomalias
            </dt>
            <dd className="numeros mt-2 font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl">
              {formatarDensidade(taxaIndicadores)}%
            </dd>
          </div>
          <p className="mt-2 text-xs text-texto-suave border-t border-borda-suave/50 pt-2">
            Achados por 100 despesas analisadas
          </p>
        </div>
      </dl>

      {/* ---------------------------------------------------------------- */}
      {/*  LINHA 2: GRÁFICOS ANALÍTICOS (DISTRIBUIÇÃO, BANCADAS, UFS, PÓDIO)*/}
      {/* ---------------------------------------------------------------- */}
      <div className="grid gap-6 lg:grid-cols-2">
        {/* Card A: Matriz de Distribuição de Risco (ADR-0013) */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-cartao">
          <div>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-titulo">Distribuição de indicadores por peso</h3>
              <span className="text-xs text-texto-suave numeros font-medium">
                {formatarNumero(totalIndicadores)} achados
              </span>
            </div>
            <p className="mt-1 text-xs text-texto-suave">
              Apenas <strong className="text-alta-texto">3,3%</strong> das despesas auditadas
              recebem classificação de gravidade alta.
            </p>

            {/* Barra empilhada proporcional */}
            {totalIndicadores > 0 && (
              <div
                className="mt-4 flex h-3.5 gap-0.5 rounded-selo overflow-hidden"
                role="img"
                aria-label={`Distribuição: ${formatarNumero(flagsAltas)} altas, ${formatarNumero(
                  flagsMedias,
                )} médias, ${formatarNumero(flagsBaixas)} baixas.`}
              >
                {flagsAltas > 0 && (
                  <span
                    className="bg-alta-texto"
                    style={{ width: `${(flagsAltas / totalIndicadores) * 100}%` }}
                    title={`Alta relevância: ${formatarNumero(flagsAltas)}`}
                  />
                )}
                {flagsMedias > 0 && (
                  <span
                    className="bg-media-texto"
                    style={{ width: `${(flagsMedias / totalIndicadores) * 100}%` }}
                    title={`Média relevância: ${formatarNumero(flagsMedias)}`}
                  />
                )}
                {flagsBaixas > 0 && (
                  <span
                    className="bg-baixa-texto"
                    style={{ width: `${(flagsBaixas / totalIndicadores) * 100}%` }}
                    title={`Baixa relevância: ${formatarNumero(flagsBaixas)}`}
                  />
                )}
              </div>
            )}

            {/* Legenda detalhada */}
            <dl className="mt-4 grid grid-cols-3 gap-2 border-t border-borda-suave/50 pt-3">
              <div className="flex flex-col">
                <dt className="flex items-center gap-1.5 text-xs text-texto-suave">
                  <span
                    aria-hidden="true"
                    className="size-2.5 bg-alta-texto [clip-path:polygon(30%_0,70%_0,100%_30%,100%_70%,70%_100%,30%_100%,0_70%,0_30%)]"
                  />
                  Alta
                </dt>
                <dd className="numeros mt-1 text-sm font-bold text-alta-texto">
                  {formatarNumero(flagsAltas)}
                  <span className="ml-1 text-[11px] font-normal text-texto-suave">
                    ({totalIndicadores > 0 ? formatarDensidade((flagsAltas / totalIndicadores) * 100) : "0"}%)
                  </span>
                </dd>
              </div>

              <div className="flex flex-col">
                <dt className="flex items-center gap-1.5 text-xs text-texto-suave">
                  <span
                    aria-hidden="true"
                    className="size-2.5 bg-media-texto [clip-path:polygon(50%_0,100%_100%,0_100%)]"
                  />
                  Média
                </dt>
                <dd className="numeros mt-1 text-sm font-bold text-media-texto">
                  {formatarNumero(flagsMedias)}
                  <span className="ml-1 text-[11px] font-normal text-texto-suave">
                    ({totalIndicadores > 0 ? formatarDensidade((flagsMedias / totalIndicadores) * 100) : "0"}%)
                  </span>
                </dd>
              </div>

              <div className="flex flex-col">
                <dt className="flex items-center gap-1.5 text-xs text-texto-suave">
                  <span aria-hidden="true" className="size-2.5 rounded-full bg-baixa-texto" />
                  Baixa
                </dt>
                <dd className="numeros mt-1 text-sm font-bold text-texto">
                  {formatarNumero(flagsBaixas)}
                  <span className="ml-1 text-[11px] font-normal text-texto-suave">
                    ({totalIndicadores > 0 ? formatarDensidade((flagsBaixas / totalIndicadores) * 100) : "0"}%)
                  </span>
                </dd>
              </div>
            </dl>
          </div>

          <p className="mt-3 text-[11px] text-texto-suave">
            A gravidade alta aponta inconsistência severa com regras explícitas da cota. A baixa
            registra ocorrências rotineiras.
          </p>
        </div>

        {/* Card B: Pódio de Líderes do Ranking Atual */}
        <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-cartao">
          <div>
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-titulo">
                Destaques da ordenação atual
              </h3>
              <span className="text-xs text-texto-suave font-semibold">
                {ordenacao?.rotulo ?? "Volume gasto"}
              </span>
            </div>
            <p className="mt-1 text-xs text-texto-suave">
              Os três primeiros parlamentares com maiores índices nesta visualização.
            </p>

            <ol className="mt-3.5 flex flex-col gap-2">
              {podio.map((item, idx) => {
                const retrato = retratoSeguro(item.fotoUrl);
                const posicao = idx + 1;

                return (
                  <li key={item.id}>
                    <Link
                      href={`/parlamentares/${item.id}`}
                      className="group flex items-center justify-between gap-3 rounded-campo border border-borda-suave/60 bg-surface/50 p-2.5 transition-colors hover:border-titulo hover:bg-surface"
                    >
                      <div className="flex items-center gap-2.5 min-w-0">
                        <span
                          className={`numeros flex size-6 shrink-0 items-center justify-center rounded-selo text-xs font-bold ${
                            posicao === 1
                              ? "bg-amber-100 text-amber-900 border border-amber-300 dark:bg-amber-950 dark:text-amber-300"
                              : posicao === 2
                                ? "bg-slate-100 text-slate-800 border border-slate-300 dark:bg-slate-800 dark:text-slate-200"
                                : "bg-orange-50 text-orange-800 border border-orange-200 dark:bg-orange-950 dark:text-orange-300"
                          }`}
                        >
                          {posicao}º
                        </span>

                        {retrato ? (
                          <Image
                            src={retrato}
                            alt=""
                            width={32}
                            height={32}
                            unoptimized
                            className="size-8 shrink-0 rounded-selo bg-surface object-cover border border-borda-suave"
                          />
                        ) : (
                          <span
                            aria-hidden="true"
                            className="numeros flex size-8 shrink-0 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo"
                          >
                            {iniciais(item.nomeParlamentar)}
                          </span>
                        )}

                        <div className="min-w-0 flex-1">
                          <span className="block truncate text-xs font-semibold text-titulo group-hover:underline">
                            {item.nomeParlamentar}
                          </span>
                          <span className="block text-[11px] text-texto-suave">
                            {item.siglaPartido} · {item.uf}
                          </span>
                        </div>
                      </div>

                      <div className="text-right shrink-0">
                        <span className="numeros block text-xs font-bold text-texto">
                          {formatarReais(item.volumeTotalGasto)}
                        </span>
                        {item.indiceDeRisco !== null && (
                          <span className="numeros block text-[10px] text-texto-suave">
                            {formatarDensidade(item.indiceDeRisco)} ind/100
                          </span>
                        )}
                      </div>
                    </Link>
                  </li>
                );
              })}
            </ol>
          </div>

          <p className="mt-2 text-[11px] text-texto-suave text-right">
            Clique no nome para abrir a auditoria detalhada do mandato →
          </p>
        </div>

        {/* Card C: Maiores Bancadas / Partidos */}
        <div className="rounded-cartao border border-borda-suave bg-fundo p-5 shadow-cartao">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-titulo">Maiores bancadas por volume</h3>
            <span className="text-[11px] text-texto-suave">Clique para filtrar</span>
          </div>
          <p className="mt-1 text-xs text-texto-suave">
            Volume financeiro consolidado pelos 5 maiores partidos na casa.
          </p>

          <div className="mt-4 flex flex-col gap-2.5">
            {topPartidos.map((partido) => {
              const larguraPct = Math.max((partido.total / maxVolumePartido) * 100, 8);
              const ativo = filtroPartidoAtivo === partido.sigla;

              return (
                <button
                  type="button"
                  key={partido.sigla}
                  onClick={() => onFiltrarPartido?.(ativo ? "" : partido.sigla)}
                  className={`group flex flex-col gap-1 rounded-campo p-1.5 text-left transition-colors ${
                    ativo ? "bg-realce/50 ring-1 ring-titulo" : "hover:bg-surface"
                  }`}
                >
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-bold text-titulo flex items-center gap-1.5">
                      <span className="rounded-selo bg-surface border border-borda-suave px-1.5 py-0.2 text-[10px] font-semibold">
                        {partido.sigla}
                      </span>
                      <span className="text-texto-suave font-normal text-[11px]">
                        ({partido.qtd} {partido.qtd === 1 ? "parlamentar" : "parlamentares"})
                      </span>
                    </span>
                    <span className="numeros font-bold text-texto">
                      {formatarReais(partido.total)}
                    </span>
                  </div>

                  <div className="h-2 w-full rounded-selo bg-surface overflow-hidden border border-borda-suave/40">
                    <div
                      className={`h-full rounded-selo transition-all ${
                        ativo ? "bg-titulo" : "bg-acento group-hover:bg-titulo"
                      }`}
                      style={{ width: `${larguraPct}%` }}
                    />
                  </div>
                </button>
              );
            })}
          </div>
        </div>

        {/* Card D: Maiores UFs por Volume */}
        <div className="rounded-cartao border border-borda-suave bg-fundo p-5 shadow-cartao">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-titulo">Concentração regional (Top UFs)</h3>
            <span className="text-[11px] text-texto-suave">Clique para filtrar</span>
          </div>
          <p className="mt-1 text-xs text-texto-suave">
            Os 5 estados com maior consumo agregado de cota parlamentar.
          </p>

          <div className="mt-4 flex flex-col gap-2.5">
            {topUfs.map((ufData) => {
              const larguraPct = Math.max((ufData.total / maxVolumeUf) * 100, 8);
              const ativo = filtroUfAtivo === ufData.uf;

              return (
                <button
                  type="button"
                  key={ufData.uf}
                  onClick={() => onFiltrarUf?.(ativo ? "" : ufData.uf)}
                  className={`group flex flex-col gap-1 rounded-campo p-1.5 text-left transition-colors ${
                    ativo ? "bg-realce/50 ring-1 ring-titulo" : "hover:bg-surface"
                  }`}
                >
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-bold text-titulo flex items-center gap-1.5">
                      <span className="rounded-selo bg-surface border border-borda-suave px-2 py-0.2 text-[10px] font-bold">
                        {ufData.uf}
                      </span>
                      <span className="text-texto-suave font-normal text-[11px]">
                        ({ufData.qtd} {ufData.qtd === 1 ? "vaga" : "vagas"})
                      </span>
                    </span>
                    <span className="numeros font-bold text-texto">
                      {formatarReais(ufData.total)}
                    </span>
                  </div>

                  <div className="h-2 w-full rounded-selo bg-surface overflow-hidden border border-borda-suave/40">
                    <div
                      className={`h-full rounded-selo transition-all ${
                        ativo ? "bg-titulo" : "bg-acento group-hover:bg-titulo"
                      }`}
                      style={{ width: `${larguraPct}%` }}
                    />
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      </div>
    </section>
  );
}
