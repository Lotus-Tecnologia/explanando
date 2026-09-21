"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { formatarNumero } from "@/lib/formato";
import { TagDeGravidade } from "./TagDeGravidade";
import { Comprovante, ValoresDaDespesa } from "./Comprovante";
import type { Gravidade, ItemDeDespesa } from "@/lib/contrato";

interface DespesasComIndicadoresProps {
  despesas: ItemDeDespesa[];
  casa: string;
  idDoParlamentar: string;
  totalComIndicador: number;
  totalAvaliadas: number;
  limiteDestaque: number;
}

type FiltroGravidade = "todas" | Gravidade;

export function DespesasComIndicadores({
  despesas,
  casa,
  idDoParlamentar,
  totalComIndicador,
  totalAvaliadas,
  limiteDestaque,
}: DespesasComIndicadoresProps) {
  const [filtro, setFiltro] = useState<FiltroGravidade>("todas");

  const despesasFiltradas = useMemo(() => {
    if (filtro === "todas") return despesas;
    return despesas.filter((d) => d.flags.some((f) => f.gravidade === filtro));
  }, [despesas, filtro]);

  const contagemAlta = useMemo(
    () => despesas.filter((d) => d.flags.some((f) => f.gravidade === "Alta")).length,
    [despesas],
  );

  const contagemMedia = useMemo(
    () => despesas.filter((d) => d.flags.some((f) => f.gravidade === "Media")).length,
    [despesas],
  );

  const contagemBaixa = useMemo(
    () => despesas.filter((d) => d.flags.some((f) => f.gravidade === "Baixa")).length,
    [despesas],
  );

  return (
    <div className="flex flex-col gap-4">
      {/* Barra superior de controles e filtros por severidade */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-borda-suave/80 pb-3">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs font-bold uppercase tracking-wider text-texto-suave mr-1">
            Filtrar achados:
          </span>

          <button
            type="button"
            onClick={() => setFiltro("todas")}
            className={`rounded-selo px-3 py-1 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-titulo ${
              filtro === "todas"
                ? "bg-titulo text-white shadow-xs"
                : "border border-borda-suave bg-surface text-texto hover:border-titulo"
            }`}
          >
            Todas ({despesas.length})
          </button>

          {contagemAlta > 0 && (
            <button
              type="button"
              onClick={() => setFiltro("Alta")}
              className={`inline-flex items-center gap-1.5 rounded-selo px-3 py-1 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-titulo ${
                filtro === "Alta"
                  ? "bg-alta-texto text-white shadow-xs"
                  : "border border-alta-texto/20 bg-alta-fundo text-alta-texto hover:border-alta-texto"
              }`}
            >
              <span
                aria-hidden="true"
                className="size-2 bg-current [clip-path:polygon(30%_0,70%_0,100%_30%,100%_70%,70%_100%,30%_100%,0_70%,0_30%)]"
              />
              Alta ({contagemAlta})
            </button>
          )}

          {contagemMedia > 0 && (
            <button
              type="button"
              onClick={() => setFiltro("Media")}
              className={`inline-flex items-center gap-1.5 rounded-selo px-3 py-1 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-titulo ${
                filtro === "Media"
                  ? "bg-media-texto text-white shadow-xs"
                  : "border border-media-texto/20 bg-media-fundo text-media-texto hover:border-media-texto"
              }`}
            >
              <span
                aria-hidden="true"
                className="size-2 bg-current [clip-path:polygon(50%_0,100%_100%,0_100%)]"
              />
              Média ({contagemMedia})
            </button>
          )}

          {contagemBaixa > 0 && (
            <button
              type="button"
              onClick={() => setFiltro("Baixa")}
              className={`inline-flex items-center gap-1.5 rounded-selo px-3 py-1 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-titulo ${
                filtro === "Baixa"
                  ? "bg-texto text-white shadow-xs"
                  : "border border-borda-suave bg-surface text-texto hover:border-titulo"
              }`}
            >
              <span aria-hidden="true" className="size-2 rounded-full bg-current" />
              Baixa ({contagemBaixa})
            </button>
          )}
        </div>

        <Link
          href={`/parlamentares/${idDoParlamentar}/despesas`}
          className="text-xs font-semibold text-titulo underline underline-offset-2 hover:text-titulo-hover"
        >
          Ver todas as {formatarNumero(totalAvaliadas)} despesas do mandato →
        </Link>
      </div>

      {/* Lista de cartões de despesa */}
      {despesasFiltradas.length > 0 ? (
        <ul className="flex flex-col gap-3">
          {despesasFiltradas.map((despesa) => (
            <li
              key={despesa.id}
              className="rounded-cartao border border-borda-suave bg-fundo p-4 sm:p-5 shadow-xs transition-shadow hover:shadow-cartao"
            >
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-selo bg-surface border border-borda-suave px-2 py-0.5 text-[11px] font-semibold text-texto-suave">
                      {despesa.tipoDespesa}
                    </span>
                    <span className="numeros text-xs text-texto-suave">
                      {new Date(despesa.dataDocumento).toLocaleDateString("pt-BR")} · competência{" "}
                      {String(despesa.mes).padStart(2, "0")}/{despesa.ano}
                    </span>
                  </div>

                  <h3 className="mt-2 text-base font-bold text-titulo">
                    {despesa.nomeFornecedor ?? "Fornecedor não identificado"}
                  </h3>

                  {despesa.documentoFornecedor && (
                    <p className="numeros mt-0.5 text-xs text-texto-suave">
                      CNPJ/CPF: <strong className="text-texto font-medium">{despesa.documentoFornecedor}</strong>
                    </p>
                  )}
                </div>

                <div className="text-right">
                  <ValoresDaDespesa despesa={despesa} />
                </div>
              </div>

              {/* Achados / Flags com a taxonomia oficial do ADR-0013 */}
              {despesa.flags.length > 0 && (
                <div className="mt-4 rounded-campo border border-borda-suave/80 bg-surface/70 p-3">
                  <p className="text-[11px] font-bold uppercase tracking-wider text-texto-suave mb-2">
                    Inconsistências identificadas pelo motor:
                  </p>
                  <ul className="space-y-2">
                    {despesa.flags.map((flag) => (
                      <li key={flag.codigoRegra} className="flex flex-wrap items-start gap-2.5">
                        <TagDeGravidade gravidade={flag.gravidade} />
                        <p className="flex-1 text-xs text-texto leading-relaxed">
                          {flag.descricao}
                        </p>
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {/* Ações: Ver nota fiscal e Ver detalhes da despesa */}
              <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-borda-suave/50 pt-3">
                <Comprovante despesa={despesa} casa={casa} />

                <Link
                  href={`/despesas/${despesa.id}`}
                  className="text-xs font-semibold text-titulo underline underline-offset-2 hover:text-titulo-hover"
                >
                  Ver dossiê desta despesa →
                </Link>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <div className="rounded-cartao border border-borda-suave bg-surface p-6 text-center text-sm text-texto-suave">
          Nenhuma despesa recente corresponde ao filtro de severidade selecionado.
        </div>
      )}

      {/* Rodapé da seção */}
      {totalComIndicador > limiteDestaque && (
        <div className="mt-2 text-center">
          <Link
            href={`/parlamentares/${idDoParlamentar}/despesas?indicador=com`}
            className="inline-block rounded-campo border border-borda-suave bg-surface px-4 py-2 text-xs font-semibold text-titulo hover:border-titulo hover:bg-fundo"
          >
            Ver todas as {formatarNumero(totalComIndicador)} despesas com indicador deste parlamentar →
          </Link>
        </div>
      )}
    </div>
  );
}
