"use client";

import { useId, useMemo, useRef, useState, useEffect } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ItemDoRanking } from "@/lib/contrato";
import { formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { buscarParlamentares, normalizar } from "@/lib/busca";

interface BuscaRapidaHomeProps {
  parlamentares: readonly ItemDoRanking[];

  /**
   * Para onde a escolha leva. Na home, o perfil; no perfil, a comparação com quem já está na
   * página. A busca é a mesma — o que muda é o destino.
   */
  caminhoDe?: (parlamentar: ItemDoRanking) => string;

  placeholder?: string;

  /** Quem não deve aparecer na lista — no comparador, o próprio parlamentar da página. */
  excluirId?: string;

  /** O nome acessível do campo, quando não há um `<label>` apontando para ele. */
  rotulo?: string;

  /** O `id` do campo, para um `<label htmlFor>` de fora apontar para ele. */
  idDoCampo?: string;
}

const PARA_O_PERFIL = (parlamentar: ItemDoRanking) => `/parlamentares/${parlamentar.id}`;

export function BuscaRapidaHome({
  parlamentares,
  caminhoDe = PARA_O_PERFIL,
  placeholder = "Digite um nome, partido ou estado",
  excluirId,
  rotulo,
  idDoCampo,
}: BuscaRapidaHomeProps) {
  const [consulta, setConsulta] = useState("");
  const [aberto, setAberto] = useState(false);
  const [indiceFoco, setIndiceFoco] = useState(-1);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listboxId = useId();
  const router = useRouter();

  const termoNormalizado = normalizar(consulta);

  // Ordem e corte explicados em lib/busca: exatos antes de pedaço do nome, casas alternadas.
  const resultados = useMemo(
    () => buscarParlamentares(parlamentares, termoNormalizado, 7, excluirId),
  [parlamentares, termoNormalizado, excluirId],
  );

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
      // Sem item destacado, Enter leva ao primeiro — é o que o rodapé do menu promete, e o
      // que quem digitou um nome e apertou Enter espera.
      const alvo = indiceFoco >= 0 && indiceFoco < resultados.length ? indiceFoco : 0;

      if (resultados[alvo]) {
        e.preventDefault();
        const escolhido = resultados[alvo];
        router.push(caminhoDe(escolhido));
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
          id={idDoCampo}
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
          placeholder={placeholder}
          aria-label={rotulo}
          className="min-h-13 w-full rounded-campo border-[1.5px] border-borda-suave bg-surface py-3 pr-12 pl-12 text-base text-texto transition-colors placeholder:text-texto-suave hover:border-borda-estrutural focus:border-acento focus:outline-none [&::-webkit-search-cancel-button]:appearance-none"
        />

        {consulta.length > 0 && (
          <button
            type="button"
            onClick={limpar}
            aria-label="Limpar busca"
            className="absolute right-1 flex size-11 items-center justify-center rounded-full text-texto-suave hover:bg-surface hover:text-titulo focus-visible:outline-2 focus-visible:outline-titulo"
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
          className="absolute top-full z-40 mt-2 w-full overflow-hidden rounded-cartao border border-borda-estrutural bg-surface p-1.5"
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
                      href={caminhoDe(item)}
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
