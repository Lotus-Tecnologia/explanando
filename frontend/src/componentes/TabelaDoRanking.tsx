"use client";

import { notaDaCobertura, porcentagemDaCobertura } from "@/lib/cobertura";
import { useMemo, useState } from "react";
import { ContagensDeGravidade, Retrato } from "./CartaoDePessoa";
import Link from "next/link";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";

import type { ItemDoRanking } from "@/lib/contrato";

/**
 * O ranking de uma casa: busca, filtros, ordenação por coluna e planilha CSV.
 * No computador é tabela; no celular cada linha vira cartão (ver `Linha`).
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

/**
 * Uma linha do ranking. No computador é linha de tabela; abaixo de `md` vira cartão de
 * pessoa pela própria classe (grade de duas colunas), sem duplicar o conteúdo num segundo
 * componente e sem rolar de lado. A posição continua na primeira célula, só para leitor de
 * tela no celular.
 */
function Linha({ item, posicao }: Linhado) {
  return (
    <tr className="relative max-md:grid max-md:grid-cols-[minmax(0,1fr)_auto] max-md:items-center max-md:gap-x-3 max-md:gap-y-2 max-md:rounded-cartao max-md:border max-md:border-borda-suave max-md:bg-surface max-md:px-3.5 max-md:py-3 md:border-b md:border-borda-suave md:last:border-b-0 hover:bg-fundo">
      <td className="numeros px-4 py-3 font-semibold text-texto-suave max-md:sr-only">{posicao}</td>

      <td className="px-4 py-3 max-md:p-0">
        <div className="flex items-center gap-3">
          <Retrato nome={item.nomeParlamentar} fotoUrl={item.fotoUrl} tamanho={40} />

          <span className="min-w-0 flex-1">
            <Link
              href={`/parlamentares/${item.id}`}
              title={item.nomeParlamentar}
              className="block max-w-72 truncate font-semibold text-texto hover:underline md:after:hidden max-md:after:absolute max-md:after:inset-0 max-md:after:rounded-cartao"
            >
              {item.nomeParlamentar}
            </Link>
            <span className="block text-sm text-texto-suave">
              {item.siglaPartido}, {item.uf}
            </span>
          </span>
        </div>
      </td>

      <td className="numeros px-4 py-3 text-right font-display font-bold whitespace-nowrap text-titulo max-md:p-0 max-md:text-[1.0625rem]">
        {formatarReais(item.volumeTotalGasto)}
      </td>

      <td className="numeros px-4 py-3 text-right max-md:hidden">
        {item.indiceDeRisco === null ? (
          <span
            className="text-sm text-texto-suave"
            title="Menos de 100 despesas analisadas: a densidade não é publicável."
          >
            amostra insuficiente
          </span>
        ) : (
          <span className="flex flex-col items-end">
            <span className="font-semibold text-texto">{formatarDensidade(item.indiceDeRisco)}</span>
            {/*
              Quanto do gasto o índice descreve (decisão de 26/09/2026). Abaixo da metade o
              parlamentar não disputa posição por índice, e aqui a fração aparece para ninguém
              comparar os dois números como se medissem a mesma coisa.
            */}
            {item.coberturaDeFornecedor !== null ? (
              <span
                className="text-xs text-texto-suave"
                title={notaDaCobertura(item.coberturaDeFornecedor, item.coberturaSuficiente) ?? undefined}
              >
                {porcentagemDaCobertura(item.coberturaDeFornecedor)} c/ fornecedor
              </span>
            ) : null}
          </span>
        )}
      </td>

      <td className="px-4 py-3 max-md:col-span-2 max-md:p-0 max-md:pl-[52px]">
        <span className="flex md:justify-end">
          <ContagensDeGravidade item={item} />
        </span>
      </td>

      <td className="numeros px-4 py-3 text-right text-texto-suave max-md:hidden">
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
  padraoDescendente = true,
}: {
  children: React.ReactNode;
  chave: ChaveDeOrdem;
  ordem: Ordem;
  aoOrdenar: (chave: ChaveDeOrdem, padraoDescendente: boolean) => void;
  alinhamento?: "left" | "right";
  padraoDescendente?: boolean;
}) {
  const ativa = ordem.chave === chave;

  return (
    <th
      scope="col"
      aria-sort={ativa ? (ordem.direcao === "asc" ? "ascending" : "descending") : "none"}
      className="p-0"
    >
      <button
        type="button"
        onClick={() => aoOrdenar(chave, padraoDescendente)}
        className={`flex min-h-11 w-full items-center gap-1 px-4 text-sm font-semibold whitespace-nowrap hover:text-texto ${
          alinhamento === "right" ? "justify-end" : "justify-start"
        } ${ativa ? "text-texto" : "text-texto-suave"}`}
      >
        {children}
        <span aria-hidden="true" className={ativa ? "text-texto" : "text-borda-estrutural"}>
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

  const classeDoSeletor =
    "mt-1.5 min-h-11 w-full rounded-campo border-[1.5px] border-borda-suave bg-surface px-3 text-[0.9375rem] text-texto";
  const classeDoRotulo = "block text-sm font-semibold text-texto";

  return (
    <div className="mt-6 flex flex-col gap-4">
      <div className="rounded-cartao border border-borda-suave bg-surface p-4">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <label htmlFor="busca" className={classeDoRotulo}>
              Procurar parlamentar
            </label>
            <div className="mt-1.5 flex min-h-11 items-center gap-2 rounded-campo border-[1.5px] border-borda-suave bg-surface px-3 focus-within:border-acento">
              <svg aria-hidden="true" viewBox="0 0 20 20" className="size-4 shrink-0 text-texto-suave">
                <circle cx="8.5" cy="8.5" r="6" fill="none" stroke="currentColor" strokeWidth="2" />
                <path d="m13 13 5 5" stroke="currentColor" strokeWidth="2" />
              </svg>
              <input
                id="busca"
                type="search"
                value={busca}
                onChange={(evento) => procurar(evento.target.value)}
                placeholder="nome, partido ou UF"
                autoComplete="off"
                className="min-w-0 flex-1 bg-transparent py-2 text-[0.9375rem] text-texto placeholder:text-texto-suave focus:outline-none"
              />
            </div>
          </div>

          <div>
            <label htmlFor="filtro-partido" className={classeDoRotulo}>
              Partido
            </label>
            <select
              id="filtro-partido"
              value={partidoAtivo}
              onChange={(e) => mudarPartido(e.target.value)}
              className={classeDoSeletor}
            >
              <option value="">Todos os partidos ({listaPartidos.length})</option>
              {listaPartidos.map(([sigla, qtd]) => (
                <option key={sigla} value={sigla}>
                  {sigla} ({qtd})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filtro-uf" className={classeDoRotulo}>
              Estado
            </label>
            <select
              id="filtro-uf"
              value={ufAtiva}
              onChange={(e) => mudarUf(e.target.value)}
              className={classeDoSeletor}
            >
              <option value="">Todos os estados ({listaUfs.length})</option>
              {listaUfs.map(([uf, qtd]) => (
                <option key={uf} value={uf}>
                  {uf} ({qtd})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filtro-severidade" className={classeDoRotulo}>
              Mostrar
            </label>
            <select
              id="filtro-severidade"
              value={filtroSeveridade}
              onChange={(e) => {
                setFiltroSeveridade(e.target.value as "todos" | "altas" | "amostra");
                setPagina(1);
              }}
              className={classeDoSeletor}
            >
              <option value="todos">Todos os parlamentares</option>
              <option value="altas">Só quem tem achado de gravidade alta</option>
              <option value="amostra">Só quem tem 100 despesas ou mais</option>
            </select>
          </div>
        </div>

        <div className="mt-3 flex flex-wrap items-center justify-between gap-3 border-t border-borda-suave pt-3">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-texto-suave" role="status" aria-live="polite">
              <strong className="numeros font-semibold text-texto">{formatarNumero(encontrados.length)}</strong>{" "}
              {encontrados.length === 1 ? "parlamentar" : "parlamentares"}
              {temFiltroAtivo ? ` de ${formatarNumero(comPosicao.length)}` : ""}
            </p>

            {/* Filtros ativos são interativos: cada um se remove sozinho. */}
            {partidoAtivo && (
              <FiltroAtivo rotulo={`Partido: ${partidoAtivo}`} aoRemover={() => mudarPartido("")} />
            )}
            {ufAtiva && <FiltroAtivo rotulo={`Estado: ${ufAtiva}`} aoRemover={() => mudarUf("")} />}
            {filtroSeveridade !== "todos" && (
              <FiltroAtivo
                rotulo={filtroSeveridade === "altas" ? "Só gravidade alta" : "100 despesas ou mais"}
                aoRemover={() => setFiltroSeveridade("todos")}
              />
            )}

            {temFiltroAtivo && (
              <button
                type="button"
                onClick={limparTodosFiltros}
                className="min-h-11 px-1 font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
              >
                Limpar todos
              </button>
            )}
          </div>

          <button
            type="button"
            onClick={exportarCsv}
            className="inline-flex min-h-11 items-center gap-1.5 rounded-campo border border-borda-suave bg-surface px-4 text-sm font-semibold text-titulo hover:border-borda-estrutural"
          >
            Baixar planilha (CSV, {formatarNumero(encontrados.length)})
          </button>
        </div>
      </div>

      <div className="md:overflow-x-auto md:rounded-cartao md:border md:border-borda-suave md:bg-surface">
        <table className="w-full border-collapse max-md:block">
          <caption className="sr-only">
            Ranking de parlamentares por {rotulo.toLowerCase()}. Os cabeçalhos reordenam a lista.
          </caption>

          <thead className="max-md:hidden">
            <tr className="border-b border-borda-estrutural">
              <CabecalhoOrdenavel chave="posicao" ordem={ordem} aoOrdenar={alternarOrdem} padraoDescendente={false}>
                #
              </CabecalhoOrdenavel>
              <CabecalhoOrdenavel chave="nome" ordem={ordem} aoOrdenar={alternarOrdem} padraoDescendente={false}>
                Parlamentar
              </CabecalhoOrdenavel>
              <CabecalhoOrdenavel chave="volume" ordem={ordem} aoOrdenar={alternarOrdem} alinhamento="right">
                Gasto
              </CabecalhoOrdenavel>
              <CabecalhoOrdenavel chave="densidade" ordem={ordem} aoOrdenar={alternarOrdem} alinhamento="right">
                Densidade
              </CabecalhoOrdenavel>
              <CabecalhoOrdenavel chave="altas" ordem={ordem} aoOrdenar={alternarOrdem} alinhamento="right">
                Achados
              </CabecalhoOrdenavel>
              <CabecalhoOrdenavel chave="despesas" ordem={ordem} aoOrdenar={alternarOrdem} alinhamento="right">
                Despesas
              </CabecalhoOrdenavel>
            </tr>
          </thead>

          <tbody className="max-md:grid max-md:gap-2">
            {visiveis.map(({ item, posicao }) => (
              <Linha key={item.id} item={item} posicao={posicao} />
            ))}
          </tbody>
        </table>

        {encontrados.length === 0 ? (
          <div className="px-4 py-12 text-center max-md:rounded-cartao max-md:border max-md:border-borda-suave max-md:bg-surface">
            <p className="font-semibold text-texto">Nenhum parlamentar encontrado</p>
            <p className="mt-1 text-texto-suave">
              A busca e os filtros não deixaram ninguém. Isso não diz nada sobre o gasto.
            </p>
            <button
              type="button"
              onClick={limparTodosFiltros}
              className="mt-3 inline-flex min-h-11 items-center rounded-campo border border-borda-suave bg-surface px-4 font-semibold text-titulo hover:border-borda-estrutural"
            >
              Limpar os filtros
            </button>
          </div>
        ) : null}
      </div>

      {totalDePaginas > 1 ? (
        <nav aria-label="Páginas do ranking" className="flex flex-wrap items-center justify-between gap-3">
          <p className="numeros text-sm text-texto-suave">
            {formatarNumero(inicio + 1)}–{formatarNumero(inicio + visiveis.length)} de{" "}
            {formatarNumero(encontrados.length)}
          </p>

          <div className="flex items-center gap-2">
            <BotaoDePagina aoClicar={() => setPagina(paginaAtual - 1)} desabilitado={paginaAtual <= 1}>
              ← Anterior
            </BotaoDePagina>
            <span className="numeros px-1 text-sm text-texto-suave">
              {paginaAtual} de {totalDePaginas}
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
  );
}

function FiltroAtivo({ rotulo, aoRemover }: { rotulo: string; aoRemover: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-campo bg-realce py-0.5 pr-0.5 pl-3 text-sm font-semibold text-texto">
      {rotulo}
      <button
        type="button"
        onClick={aoRemover}
        aria-label={`Remover filtro ${rotulo}`}
        className="inline-flex size-9 items-center justify-center rounded-campo hover:bg-surface"
      >
        <span aria-hidden="true">×</span>
      </button>
    </span>
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
      className="inline-flex min-h-11 items-center rounded-campo border border-borda-suave bg-surface px-4 text-sm font-semibold text-titulo enabled:hover:border-borda-estrutural disabled:cursor-not-allowed disabled:text-texto-suave"
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
