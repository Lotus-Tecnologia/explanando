"use client";

import { useId, useMemo, useRef, useState, useEffect } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ItemDoRanking } from "@/lib/contrato";
import { formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";

/** Dobra acentos e caixa alta para buscas insensíveis a diacríticos. */
function normalizar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase()
    .trim();
}

interface BuscaRapidaHomeProps {
  parlamentares: readonly ItemDoRanking[];
}

export function BuscaRapidaHome({ parlamentares }: BuscaRapidaHomeProps) {
  const [consulta, setConsulta] = useState("");
  const [aberto, setAberto] = useState(false);
  const [indiceFoco, setIndiceFoco] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listboxId = useId();
  const router = useRouter();

  const termoNormalizado = normalizar(consulta);

  const resultados = useMemo(() => {
    if (termoNormalizado.length < 2) return [];

    const termos = termoNormalizado.split(/\s+/).filter(Boolean);

    return parlamentares
      .filter((p) => {
        const nome = normalizar(p.nomeParlamentar);
        const partido = normalizar(p.siglaPartido);
        const uf = normalizar(p.uf);
        const casa = normalizar(p.casa);

        // Todos os termos digitados precisam bater em algum campo
        return termos.every(
          (t) =>
            nome.includes(t) ||
            partido === t ||
            partido.includes(t) ||
            uf === t ||
            casa.includes(t),
        );
      })
      .slice(0, 7);
  }, [parlamentares, termoNormalizado]);

  // Fecha o menu ao clicar fora
  useEffect(() => {
    function tratarCliqueFora(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setAberto(false);
      }
    }
    document.addEventListener("mousedown", tratarCliqueFora);
    return () => document.removeEventListener("mousedown", tratarCliqueFora);
  }, []);

  function aoTeclar(e: React.KeyboardEvent<HTMLInputElement>) {
    if (!aberto || resultados.length === 0) {
      if (e.key === "ArrowDown" && resultados.length > 0) {
        setAberto(true);
        setIndiceFoco(0);
        e.preventDefault();
      }
      return;
    }

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setIndiceFoco((antigo) => (antigo + 1 >= resultados.length ? 0 : antigo + 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setIndiceFoco((antigo) => (antigo <= 0 ? resultados.length - 1 : antigo - 1));
    } else if (e.key === "Enter") {
      if (indiceFoco >= 0 && indiceFoco < resultados.length) {
        e.preventDefault();
        const escolhido = resultados[indiceFoco];
        router.push(`/parlamentares/${escolhido.id}`);
        setAberto(false);
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      setAberto(false);
      setIndiceFoco(-1);
    }
  }

  function limpar() {
    setConsulta("");
    setAberto(false);
    setIndiceFoco(-1);
    inputRef.current?.focus();
  }

  const temResultados = resultados.length > 0;
  const mostrarMensagemVazia = aberto && termoNormalizado.length >= 2 && !temResultados;

  return (
    <div ref={containerRef} className="relative w-full max-w-2xl">
      <div className="relative flex items-center">
        {/* Ícone de lupa */}
        <span
          aria-hidden="true"
          className="pointer-events-none absolute left-4 text-texto-suave"
        >
          <svg
            className="size-5"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth="2"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z"
            />
          </svg>
        </span>

        <input
          ref={inputRef}
          type="search"
          role="combobox"
          aria-expanded={aberto && (temResultados || mostrarMensagemVazia)}
          aria-controls={listboxId}
          aria-activedescendant={
            indiceFoco >= 0 && resultados[indiceFoco]
              ? `resultado-${resultados[indiceFoco].id}`
              : undefined
          }
          aria-autocomplete="list"
          value={consulta}
          onChange={(e) => {
            setConsulta(e.target.value);
            setAberto(true);
            setIndiceFoco(-1);
          }}
          onFocus={() => {
            if (termoNormalizado.length >= 2) setAberto(true);
          }}
          onKeyDown={aoTeclar}
          placeholder="Buscar por nome, partido ou UF (ex: Tabata, SP, PL, Romário)..."
          className="w-full rounded-campo border-2 border-borda-suave bg-fundo py-3.5 pr-11 pl-12 text-sm text-texto shadow-cartao transition-colors placeholder:text-texto-suave/70 hover:border-borda-estrutural focus:border-titulo focus:ring-2 focus:ring-acento/20 focus:outline-none"
        />

        {consulta.length > 0 && (
          <button
            type="button"
            onClick={limpar}
            aria-label="Limpar busca"
            className="absolute right-3.5 flex size-6 items-center justify-center rounded-full text-texto-suave hover:bg-surface hover:text-titulo focus-visible:outline-2 focus-visible:outline-titulo"
          >
            <span aria-hidden="true" className="text-sm font-bold">
              ×
            </span>
          </button>
        )}
      </div>

      {/* Dropdown de resultados */}
      {aberto && (temResultados || mostrarMensagemVazia) && (
        <div
          id={listboxId}
          role="listbox"
          aria-label="Sugestões de parlamentares"
          className="absolute top-full z-40 mt-2 w-full overflow-hidden rounded-cartao border border-borda-suave bg-fundo p-1.5 shadow-flutuante backdrop-blur"
        >
          {temResultados && (
            <ul className="flex flex-col gap-0.5">
              {resultados.map((item, idx) => {
                const retrato = retratoSeguro(item.fotoUrl);
                const selecionado = idx === indiceFoco;
                const ehCamara = item.casa.toLowerCase().includes("câmara") || item.casa.toLowerCase().includes("deputad");

                return (
                  <li
                    key={item.id}
                    id={`resultado-${item.id}`}
                    role="option"
                    aria-selected={selecionado}
                  >
                    <Link
                      href={`/parlamentares/${item.id}`}
                      onClick={() => setAberto(false)}
                      className={`flex items-center gap-3 rounded-campo px-3 py-2 text-left transition-colors ${
                        selecionado
                          ? "bg-realce/60 text-titulo"
                          : "hover:bg-surface text-texto"
                      }`}
                    >
                      {retrato ? (
                        <Image
                          src={retrato}
                          alt=""
                          width={36}
                          height={36}
                          unoptimized
                          className="size-9 shrink-0 rounded-selo bg-surface object-cover border border-borda-suave"
                        />
                      ) : (
                        <span
                          aria-hidden="true"
                          className="flex size-9 shrink-0 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo"
                        >
                          {iniciais(item.nomeParlamentar)}
                        </span>
                      )}

                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2">
                          <span className="truncate text-sm font-semibold text-titulo">
                            {item.nomeParlamentar}
                          </span>
                          <span
                            className={`rounded-selo px-1.5 py-0.5 text-[10px] font-semibold ${
                              ehCamara
                                ? "bg-surface text-texto-suave border border-borda-suave"
                                : "bg-realce text-titulo"
                            }`}
                          >
                            {ehCamara ? "Câmara" : "Senado"}
                          </span>
                        </div>

                        <div className="mt-0.5 flex items-center gap-1.5 text-xs text-texto-suave">
                          <span className="font-semibold text-texto">{item.siglaPartido}</span>
                          <span>·</span>
                          <span>{item.uf}</span>
                          <span>·</span>
                          <span className="numeros font-medium text-texto">
                            {formatarReais(item.volumeTotalGasto)}
                          </span>
                        </div>
                      </div>

                      <span aria-hidden="true" className="text-sm text-texto-suave shrink-0">
                        →
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}

          {mostrarMensagemVazia && (
            <div className="p-4 text-center text-sm text-texto-suave">
              Nenhum parlamentar encontrado para{" "}
              <strong className="text-texto">“{consulta}”</strong>.
            </div>
          )}

          <div className="border-t border-borda-suave px-3 py-1.5 text-[11px] text-texto-suave flex items-center justify-between">
            <span>Pressione Enter para selecionar</span>
            <span>Esc para fechar</span>
          </div>
        </div>
      )}
    </div>
  );
}
