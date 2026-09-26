"use client";

import { notaDaCobertura, porcentagemDaCobertura } from "@/lib/cobertura";
import { useMemo, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import type { ItemDoRanking } from "@/lib/contrato";

/**
 * O ranking de uma casa com data-grid denso, filtros multi-critério,
 * busca instantânea, ordenação completa e exportação CSV.
 */

const POR_PAGINA = 25;

type ChaveDeOrdem = "posicao" | "nome" | "volume" | "densidade" | "altas" | "despesas";
type Direcao = "asc" | "desc";

interface Ordem {
  chave: ChaveDeOrdem;
  direcao: Direcao;
}

/** Dobra acento e caixa para busca insensível a diacríticos. */
function dobrar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase();
}

interface Linhado {
  item: ItemDoRanking;
  posicao: number;
}

/**
 * O valor de uma linha na chave pedida. `null` significa não medido.
 */
function valorDe(linha: Linhado, chave: ChaveDeOrdem): number | string | null {
  switch (chave) {
    case "posicao":
      return linha.posicao;
    case "nome":
      return dobrar(linha.item.nomeParlamentar);
    case "volume":
      return linha.item.volumeTotalGasto;
    case "densidade":
      return linha.item.indiceDeRisco;
    case "altas":
      return linha.item.flagsAltas;
    case "despesas":
      return linha.item.despesasAvaliadas;
  }
}

function ordenar(linhas: Linhado[], ordem: Ordem): Linhado[] {
  const sinal = ordem.direcao === "asc" ? 1 : -1;

  return [...linhas].sort((a, b) => {
    const va = valorDe(a, ordem.chave);
    const vb = valorDe(b, ordem.chave);

    // Nulo por último SEMPRE (ADR-0009)
    if (va === null && vb === null) return a.posicao - b.posicao;
    if (va === null) return 1;
    if (vb === null) return -1;

    if (typeof va === "string" || typeof vb === "string") {
      return sinal * String(va).localeCompare(String(vb), "pt-BR");
    }

    return va === vb ? a.posicao - b.posicao : sinal * (va - vb);
  });
}

function Retrato({ item }: { item: ItemDoRanking }) {
  const url = retratoSeguro(item.fotoUrl);

  if (!url) {
    return (
      <span
        aria-hidden="true"
        className="numeros flex size-9 shrink-0 items-center justify-center rounded-selo bg-realce text-[11px] font-bold text-titulo"
      >
        {iniciais(item.nomeParlamentar)}
      </span>
    );
  }

  return (
    <Image
      src={url}
      alt=""
      width={36}
      height={36}
      unoptimized
      className="size-9 shrink-0 rounded-selo bg-surface object-cover border border-borda-suave"
    />
  );
}

function Linha({ item, posicao }: Linhado) {
  return (
    <tr className="transition-colors hover:bg-surface/80">
      <td className="numeros px-3 py-3 text-sm text-texto-suave font-semibold">{posicao}</td>

      <td className="px-3 py-3">
        <div className="flex items-center gap-3">
          <Retrato item={item} />

          <span className="min-w-0 flex-1">
            <Link
              href={`/parlamentares/${item.id}`}
              className="block truncate text-sm font-semibold text-titulo underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
            >
              {item.nomeParlamentar}
            </Link>
            <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-texto-suave">
              <span className="rounded-selo bg-surface border border-borda-suave px-1.5 py-0.2 font-semibold text-texto">
                {item.siglaPartido}
              </span>
              <span>·</span>
              <span className="font-semibold text-texto-suave">{item.uf}</span>

              {/* Volume no celular */}
              <span className="sm:hidden font-semibold text-texto">
                · {formatarReais(item.volumeTotalGasto)}
              </span>
            </div>
          </span>
        </div>
      </td>

      <td className="numeros hidden px-3 py-3 text-right text-sm font-bold whitespace-nowrap text-titulo sm:table-cell">
        {formatarReais(item.volumeTotalGasto)}
      </td>

      <td className="numeros hidden px-3 py-3 text-right text-sm sm:table-cell">
        {item.indiceDeRisco === null ? (
          <span
            className="text-xs text-abstencao-texto"
            title="Menos de 100 despesas analisadas: a densidade não é publicável."
          >
            amostra insuficiente
          </span>
        ) : (
          <span className="flex flex-col items-end">
            <span className="font-semibold text-texto">
              {formatarDensidade(item.indiceDeRisco)}
            </span>
            {/*
              Quanto do gasto o índice descreve (decisão de 26/09/2026). Abaixo da metade o
              parlamentar não disputa posição por índice — a API já o tira dessa ordenação —,
              e aqui, no ranking de volume, a fração aparece para ninguém comparar os dois
              números como se medissem a mesma coisa.
            */}
            {item.coberturaDeFornecedor !== null ? (
              <span
                className={`text-[11px] font-normal ${
                  item.coberturaSuficiente ? "text-texto-suave" : "text-abstencao-texto"
                }`}
                title={notaDaCobertura(item.coberturaDeFornecedor, item.coberturaSuficiente) ?? undefined}
              >
                {porcentagemDaCobertura(item.coberturaDeFornecedor)} c/ fornecedor
              </span>
            ) : null}
          </span>
        )}
      </td>

      <td className="numeros px-3 py-3 text-right text-sm whitespace-nowrap">
        <div className="inline-flex items-center justify-end gap-1.5">
          <span
            className="inline-flex items-center gap-0.5 rounded-sm bg-alta-fundo px-1 py-0.5 text-xs font-bold text-alta-texto border border-alta-texto/20"
            title={`${item.flagsAltas} achados de alta relevância`}
          >
            <span
              aria-hidden="true"
              className="size-1.5 bg-alta-texto [clip-path:polygon(30%_0,70%_0,100%_30%,100%_70%,70%_100%,30%_100%,0_70%,0_30%)]"
            />
            {item.flagsAltas}
          </span>
          <span
            className="inline-flex items-center gap-0.5 rounded-sm bg-media-fundo px-1 py-0.5 text-xs font-semibold text-media-texto border border-media-texto/20"
            title={`${item.flagsMedias} achados de média relevância`}
          >
            <span
              aria-hidden="true"
              className="size-1.5 bg-media-texto [clip-path:polygon(50%_0,100%_100%,0_100%)]"
            />
            {item.flagsMedias}
          </span>
          <span
            className="inline-flex items-center gap-0.5 rounded-sm bg-surface px-1 py-0.5 text-xs font-medium text-texto-suave border border-borda-suave"
            title={`${item.flagsBaixas} achados de baixa relevância`}
          >
            <span aria-hidden="true" className="size-1.5 rounded-full bg-baixa-texto" />
            {item.flagsBaixas}
          </span>
        </div>
      </td>

      <td className="numeros hidden px-3 py-3 text-right text-sm text-texto-suave sm:table-cell">
        {formatarNumero(item.despesasAvaliadas)}
      </td>
    </tr>
  );
}

function CabecalhoOrdenavel({
  children,
  chave,
  ordem,
  aoOrdenar,
  alinhamento = "left",
  apenasLargas = false,
  padraoDescendente = true,
}: {
  children: React.ReactNode;
  chave: ChaveDeOrdem;
  ordem: Ordem;
  aoOrdenar: (chave: ChaveDeOrdem, padraoDescendente: boolean) => void;
  alinhamento?: "left" | "right";
  apenasLargas?: boolean;
  padraoDescendente?: boolean;
}) {
  const ativa = ordem.chave === chave;

  return (
    <th
      scope="col"
      aria-sort={ativa ? (ordem.direcao === "asc" ? "ascending" : "descending") : "none"}
      className={`bg-surface p-0 ${apenasLargas ? "hidden sm:table-cell" : ""}`}
    >
      <button
        type="button"
        onClick={() => aoOrdenar(chave, padraoDescendente)}
        className={`flex w-full items-center gap-1 px-3 py-2.5 text-[11px] font-bold tracking-wider uppercase hover:text-titulo focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo ${
          alinhamento === "right" ? "justify-end" : "justify-start"
        } ${ativa ? "text-titulo" : "text-texto-suave"}`}
      >
        {children}

        <span aria-hidden="true" className={ativa ? "text-acento" : "text-borda-estrutural"}>
          {ativa ? (ordem.direcao === "asc" ? "↑" : "↓") : "↕"}
        </span>
      </button>
    </th>
  );
}

export interface TabelaDoRankingProps {
  itens: ItemDoRanking[];
  rotulo: string;
  filtroPartidoExterno?: string;
  onMudarFiltroPartido?: (partido: string) => void;
  filtroUfExterno?: string;
  onMudarFiltroUf?: (uf: string) => void;
}

export function TabelaDoRanking({
  itens,
  rotulo,
  filtroPartidoExterno,
  onMudarFiltroPartido,
  filtroUfExterno,
  onMudarFiltroUf,
}: TabelaDoRankingProps) {
  const [busca, setBusca] = useState("");
  const [pagina, setPagina] = useState(1);
  const [filtroPartidoInterno, setFiltroPartidoInterno] = useState("");
  const [filtroUfInterno, setFiltroUfInterno] = useState("");
  const [filtroSeveridade, setFiltroSeveridade] = useState<"todos" | "altas" | "amostra">("todos");

  const [ordem, setOrdem] = useState<Ordem>({ chave: "posicao", direcao: "asc" });

  const partidoAtivo = filtroPartidoExterno !== undefined ? filtroPartidoExterno : filtroPartidoInterno;
  const ufAtiva = filtroUfExterno !== undefined ? filtroUfExterno : filtroUfInterno;

  const comPosicao = useMemo(
    () => itens.map((item, indice) => ({ item, posicao: indice + 1 })),
    [itens],
  );

  // Lista de partidos únicos com quantidade
  const listaPartidos = useMemo(() => {
    const mapa = new Map<string, number>();
    for (const { item } of comPosicao) {
      const p = item.siglaPartido.trim();
      if (p) mapa.set(p, (mapa.get(p) ?? 0) + 1);
    }
    return Array.from(mapa.entries()).sort((a, b) => b[1] - a[1]);
  }, [comPosicao]);

  // Lista de UFs únicas com quantidade
  const listaUfs = useMemo(() => {
    const mapa = new Map<string, number>();
    for (const { item } of comPosicao) {
      const u = item.uf.trim();
      if (u) mapa.set(u, (mapa.get(u) ?? 0) + 1);
    }
    return Array.from(mapa.entries()).sort((a, b) => a[0].localeCompare(b[0]));
  }, [comPosicao]);

  const encontrados = useMemo(() => {
    const procurado = dobrar(busca.trim());

    const filtrados = comPosicao.filter(({ item }) => {
      // 1. Busca por texto
      if (procurado !== "") {
        const textoCompleto = dobrar(`${item.nomeParlamentar} ${item.siglaPartido} ${item.uf}`);
        if (!textoCompleto.includes(procurado)) return false;
      }

      // 2. Filtro de Partido
      if (partidoAtivo !== "" && item.siglaPartido !== partidoAtivo) {
        return false;
      }

      // 3. Filtro de UF
      if (ufAtiva !== "" && item.uf !== ufAtiva) {
        return false;
      }

      // 4. Filtro de Severidade
      if (filtroSeveridade === "altas" && item.flagsAltas <= 0) {
        return false;
      }
      if (filtroSeveridade === "amostra" && item.indiceDeRisco === null) {
        return false;
      }

      return true;
    });

    return ordenar(filtrados, ordem);
  }, [comPosicao, busca, partidoAtivo, ufAtiva, filtroSeveridade, ordem]);

  const totalDePaginas = Math.max(1, Math.ceil(encontrados.length / POR_PAGINA));
  const paginaAtual = Math.min(pagina, totalDePaginas);
  const inicio = (paginaAtual - 1) * POR_PAGINA;
  const visiveis = encontrados.slice(inicio, inicio + POR_PAGINA);

  function procurar(valor: string) {
    setBusca(valor);
    setPagina(1);
  }

  function mudarPartido(novo: string) {
    if (onMudarFiltroPartido) {
      onMudarFiltroPartido(novo);
    } else {
      setFiltroPartidoInterno(novo);
    }
    setPagina(1);
  }

  function mudarUf(nova: string) {
    if (onMudarFiltroUf) {
      onMudarFiltroUf(nova);
    } else {
      setFiltroUfInterno(nova);
    }
    setPagina(1);
  }

  function limparTodosFiltros() {
    setBusca("");
    mudarPartido("");
    mudarUf("");
    setFiltroSeveridade("todos");
    setPagina(1);
  }

  function alternarOrdem(chave: ChaveDeOrdem, padraoDescendente: boolean) {
    setOrdem((atual) =>
      atual.chave === chave
        ? { chave, direcao: atual.direcao === "asc" ? "desc" : "asc" }
        : { chave, direcao: padraoDescendente ? "desc" : "asc" },
    );
    setPagina(1);
  }

  function exportarCsv() {
    const cabecalho = [
      "Posicao",
      "Nome",
      "Partido",
      "UF",
      "Casa",
      "VolumeGasto",
      "DespesasAvaliadas",
      "IndiceDeRisco",
      "FlagsAltas",
      "FlagsMedias",
      "FlagsBaixas",
    ];

    const linhasCsv = encontrados.map(({ item, posicao }) => [
      posicao,
      `"${item.nomeParlamentar.replace(/"/g, '""')}"`,
      `"${item.siglaPartido}"`,
      `"${item.uf}"`,
      `"${item.casa}"`,
      decimalBr(item.volumeTotalGasto),
      item.despesasAvaliadas,
      item.indiceDeRisco !== null ? decimalBr(item.indiceDeRisco) : "",
      item.flagsAltas,
      item.flagsMedias,
      item.flagsBaixas,
    ]);

    const csvContent =
      "\uFEFF" + [cabecalho.join(";"), ...linhasCsv.map((l) => l.join(";"))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", `ranking-${rotulo.toLowerCase().replace(/\s+/g, "-")}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  const temFiltroAtivo =
    busca.trim() !== "" ||
    partidoAtivo !== "" ||
    ufAtiva !== "" ||
    filtroSeveridade !== "todos";

  return (
    <div className="mt-8 flex flex-col gap-4">
      {/* ---------------------------------------------------------------- */}
      {/*  BARRA DE FERRAMENTAS & FILTROS MULTI-CRITÉRIO                   */}
      {/* ---------------------------------------------------------------- */}
      <div className="rounded-cartao border border-borda-suave bg-fundo p-4 shadow-xs">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {/* 1. Busca por nome */}
          <div>
            <label htmlFor="busca" className="block text-xs font-semibold text-texto-suave">
              Procurar parlamentar
            </label>
            <div className="relative mt-1">
              <span
                aria-hidden="true"
                className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-texto-suave"
              >
                ⌕
              </span>
              <input
                id="busca"
                type="search"
                value={busca}
                onChange={(evento) => procurar(evento.target.value)}
                placeholder="nome, partido ou UF"
                autoComplete="off"
                className="w-full rounded-campo border border-borda-suave bg-fundo py-2 pr-3 pl-8 text-sm text-texto placeholder:text-texto-suave/70 focus-visible:border-titulo focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-titulo"
              />
            </div>
          </div>

          {/* 2. Filtro por Partido */}
          <div>
            <label htmlFor="filtro-partido" className="block text-xs font-semibold text-texto-suave">
              Filtrar por partido
            </label>
            <select
              id="filtro-partido"
              value={partidoAtivo}
              onChange={(e) => mudarPartido(e.target.value)}
              className="mt-1 w-full rounded-campo border border-borda-suave bg-fundo px-3 py-2 text-sm text-texto focus-visible:border-titulo focus-visible:outline-2 focus-visible:outline-titulo"
            >
              <option value="">Todos os partidos ({listaPartidos.length})</option>
              {listaPartidos.map(([sigla, qtd]) => (
                <option key={sigla} value={sigla}>
                  {sigla} ({qtd})
                </option>
              ))}
            </select>
          </div>

          {/* 3. Filtro por UF */}
          <div>
            <label htmlFor="filtro-uf" className="block text-xs font-semibold text-texto-suave">
              Filtrar por estado (UF)
            </label>
            <select
              id="filtro-uf"
              value={ufAtiva}
              onChange={(e) => mudarUf(e.target.value)}
              className="mt-1 w-full rounded-campo border border-borda-suave bg-fundo px-3 py-2 text-sm text-texto focus-visible:border-titulo focus-visible:outline-2 focus-visible:outline-titulo"
            >
              <option value="">Todas as UFs ({listaUfs.length})</option>
              {listaUfs.map(([uf, qtd]) => (
                <option key={uf} value={uf}>
                  {uf} ({qtd})
                </option>
              ))}
            </select>
          </div>

          {/* 4. Filtro de Severidade / Amostra */}
          <div>
            <label htmlFor="filtro-severidade" className="block text-xs font-semibold text-texto-suave">
              Critério de risco
            </label>
            <select
              id="filtro-severidade"
              value={filtroSeveridade}
              onChange={(e) => {
                setFiltroSeveridade(e.target.value as "todos" | "altas" | "amostra");
                setPagina(1);
              }}
              className="mt-1 w-full rounded-campo border border-borda-suave bg-fundo px-3 py-2 text-sm text-texto focus-visible:border-titulo focus-visible:outline-2 focus-visible:outline-titulo"
            >
              <option value="todos">Todos os parlamentares</option>
              <option value="altas">Com achados de alta relevância (altas &gt; 0)</option>
              <option value="amostra">Apenas com amostra completa (≥ 100 despesas)</option>
            </select>
          </div>
        </div>

        {/* Linha de status, filtros ativos e exportação */}
        <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-borda-suave/60 pt-3">
          <div className="flex flex-wrap items-center gap-2">
            {/* Região viva acessível */}
            <p className="text-sm text-texto-suave" role="status" aria-live="polite">
              <strong className="numeros text-titulo font-bold">{formatarNumero(encontrados.length)}</strong>{" "}
              {encontrados.length === 1 ? "parlamentar" : "parlamentares"}
              {temFiltroAtivo ? ` de ${formatarNumero(comPosicao.length)}` : ""}
            </p>

            {/* Badges de filtros ativos */}
            {partidoAtivo && (
              <span className="inline-flex items-center gap-1 rounded-selo bg-realce px-2 py-0.5 text-xs font-semibold text-titulo">
                Partido: {partidoAtivo}
                <button
                  type="button"
                  onClick={() => mudarPartido("")}
                  className="hover:opacity-75 focus-visible:outline-none"
                  aria-label={`Remover filtro de partido ${partidoAtivo}`}
                >
                  ×
                </button>
              </span>
            )}

            {ufAtiva && (
              <span className="inline-flex items-center gap-1 rounded-selo bg-realce px-2 py-0.5 text-xs font-semibold text-titulo">
                UF: {ufAtiva}
                <button
                  type="button"
                  onClick={() => mudarUf("")}
                  className="hover:opacity-75 focus-visible:outline-none"
                  aria-label={`Remover filtro de UF ${ufAtiva}`}
                >
                  ×
                </button>
              </span>
            )}

            {filtroSeveridade !== "todos" && (
              <span className="inline-flex items-center gap-1 rounded-selo bg-realce px-2 py-0.5 text-xs font-semibold text-titulo">
                {filtroSeveridade === "altas" ? "Só altas" : "Amostra completa"}
                <button
                  type="button"
                  onClick={() => setFiltroSeveridade("todos")}
                  className="hover:opacity-75 focus-visible:outline-none"
                  aria-label="Remover filtro de severidade"
                >
                  ×
                </button>
              </span>
            )}

            {temFiltroAtivo && (
              <button
                type="button"
                onClick={limparTodosFiltros}
                className="text-xs font-semibold text-titulo underline underline-offset-2 hover:text-titulo-hover ml-1"
              >
                Limpar todos
              </button>
            )}
          </div>

          {/* Botão de Exportação CSV */}
          <button
            type="button"
            onClick={exportarCsv}
            title="Baixar dados filtrados em formato CSV (planilha)"
            className="inline-flex items-center gap-1.5 rounded-campo border border-borda-suave bg-surface px-3 py-1.5 text-xs font-semibold text-texto hover:border-titulo hover:text-titulo focus-visible:outline-2 focus-visible:outline-titulo"
          >
            <span aria-hidden="true">↓</span>
            <span>Exportar CSV ({encontrados.length})</span>
          </button>
        </div>
      </div>

      {/* ---------------------------------------------------------------- */}
      {/*  TABELA DATA-GRID                                                */}
      {/* ---------------------------------------------------------------- */}
      <div className="overflow-hidden rounded-cartao border border-borda-suave shadow-cartao bg-fundo">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <caption className="sr-only">
              Ranking de parlamentares por {rotulo.toLowerCase()}. Clique num cabeçalho para
              reordenar.
            </caption>

            <thead>
              <tr className="border-b-2 border-borda-estrutural">
                <CabecalhoOrdenavel
                  chave="posicao"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  padraoDescendente={false}
                >
                  #
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="nome"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  padraoDescendente={false}
                >
                  Parlamentar
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="volume"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Volume gasto
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="densidade"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Densidade
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="altas"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                >
                  Alta · Média · Baixa
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="despesas"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Despesas
                </CabecalhoOrdenavel>
              </tr>
            </thead>

            <tbody className="divide-y divide-borda-suave">
              {visiveis.map(({ item, posicao }) => (
                <Linha key={item.id} item={item} posicao={posicao} />
              ))}
            </tbody>
          </table>
        </div>

        {/* Mensagem quando nada é encontrado */}
        {encontrados.length === 0 ? (
          <div className="px-4 py-12 text-center text-sm text-texto-suave">
            <p className="font-semibold text-titulo">Nenhum parlamentar encontrado</p>
            <p className="mt-1 text-xs">
              Nenhum registro corresponde aos filtros selecionados. Tente ajustar os termos de busca
              ou limpar os filtros.
            </p>
            <button
              type="button"
              onClick={limparTodosFiltros}
              className="mt-3 inline-block rounded-campo border border-borda-suave bg-surface px-3 py-1.5 text-xs font-semibold text-titulo hover:border-titulo"
            >
              Restaurar listagem completa
            </button>
          </div>
        ) : null}

        {/* Navegação de Paginação */}
        {totalDePaginas > 1 ? (
          <nav
            aria-label="Páginas do ranking"
            className="flex flex-wrap items-center justify-between gap-3 border-t border-borda-suave bg-surface px-4 py-3"
          >
            <p className="numeros text-xs text-texto-suave">
              {formatarNumero(inicio + 1)}–{formatarNumero(inicio + visiveis.length)} de{" "}
              {formatarNumero(encontrados.length)}
            </p>

            <div className="flex items-center gap-2">
              <BotaoDePagina
                aoClicar={() => setPagina(paginaAtual - 1)}
                desabilitado={paginaAtual <= 1}
              >
                ← Anterior
              </BotaoDePagina>

              <span className="numeros px-1 text-xs text-texto-suave">
                {paginaAtual} / {totalDePaginas}
              </span>

              <BotaoDePagina
                aoClicar={() => setPagina(paginaAtual + 1)}
                desabilitado={paginaAtual >= totalDePaginas}
              >
                Próxima →
              </BotaoDePagina>
            </div>
          </nav>
        ) : null}
      </div>
    </div>
  );
}

function BotaoDePagina({
  children,
  aoClicar,
  desabilitado,
}: {
  children: React.ReactNode;
  aoClicar: () => void;
  desabilitado: boolean;
}) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      disabled={desabilitado}
      className="rounded-campo border border-borda-suave bg-fundo px-3 py-1.5 text-xs font-semibold text-titulo enabled:hover:border-titulo disabled:cursor-not-allowed disabled:text-texto-suave disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
    >
      {children}
    </button>
  );
}

/**
 * Decimal com vírgula, para o CSV abrir certo no Excel em português.
 *
 * O separador de campo já é o ponto e vírgula do Excel brasileiro; com ele, "2287735.08" é
 * lido como texto — ou, pior, como 228.773.508. A vírgula é o que o mesmo Excel espera.
 */
function decimalBr(valor: number): string {
  return valor.toFixed(2).replace(".", ",");
}
