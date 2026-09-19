"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import type { NoDoEstado } from "@/lib/contrato";
import { contem, dobrar, ondeCasa } from "@/lib/texto";
import { corDoPoder } from "@/lib/grafo";
import { formatarNumero } from "@/lib/formato";

/**
 * A busca dentro do grafo.
 *
 * **Procura no cliente, porque os 336 nós já estão na página.** Ir ao servidor para
 * filtrar uma lista que o navegador tem na mão acrescentaria latência a cada tecla e uma
 * rota a manter, sem trazer nada.
 *
 * Insensível a acento e a caixa: quem digita "orgao" está procurando "órgão", e uma busca
 * que não acha é indistinguível de um cadastro que não tem.
 */
export function BuscaNoGrafo({
  nos,
  aoEscolher,
}: {
  nos: readonly NoDoEstado[];
  aoEscolher: (codigo: number) => void;
}) {
  const [aberta, setAberta] = useState(false);
  const [termo, setTermo] = useState("");
  const [destaque, setDestaque] = useState(0);
  const campo = useRef<HTMLInputElement>(null);

  const MAXIMO = 12;

  const achados = useMemo(() => {
    if (termo.trim().length < 2) return [];

    const casam = nos.filter(
      (no) => contem(no.nome, termo) || (no.sigla !== null && contem(no.sigla, termo)),
    );

    // O que começa com o termo vem primeiro: quem digita "min" quer os ministérios, não
    // uma entidade qualquer que tenha "min" no meio do nome.
    const alvo = dobrar(termo);

    return casam
      .sort((a, b) => {
        const comeca = (no: NoDoEstado) =>
          dobrar(no.nome).startsWith(alvo) || dobrar(no.sigla ?? "").startsWith(alvo)
            ? 0
            : 1;

        return comeca(a) - comeca(b) || b.unidadesSubordinadas - a.unidadesSubordinadas;
      })
      .slice(0, MAXIMO);
  }, [nos, termo]);

  // A barra `/` abre a busca, como em toda ferramenta de leitura. Só quando o foco não
  // está num campo — senão digitar uma barra num formulário abriria a busca.
  useEffect(() => {
    function aoTeclar(evento: KeyboardEvent) {
      const alvo = evento.target as HTMLElement | null;
      const digitando =
        alvo?.tagName === "INPUT" ||
        alvo?.tagName === "TEXTAREA" ||
        alvo?.isContentEditable === true;

      if (evento.key === "/" && !digitando) {
        evento.preventDefault();
        setAberta(true);
      }

      if (evento.key === "Escape") setAberta(false);
    }

    window.addEventListener("keydown", aoTeclar);

    return () => window.removeEventListener("keydown", aoTeclar);
  }, []);

  useEffect(() => {
    if (aberta) campo.current?.focus();
  }, [aberta]);

  // O destaque preso a lista: ela encolhe a cada tecla, e um indice velho apontaria
  // para fora dela.
  const escolhido = Math.min(destaque, Math.max(0, achados.length - 1));

  function escolher(codigo: number) {
    aoEscolher(codigo);
    setAberta(false);
    setTermo("");
  }

  if (!aberta) {
    return (
      <button
        type="button"
        onClick={() => setAberta(true)}
        className="flex items-center gap-2 rounded-campo border border-borda-suave bg-surface px-3 py-2 text-sm text-texto-suave hover:border-borda-estrutural hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      >
        <span aria-hidden="true">⌕</span>
        Buscar um órgão
        <kbd className="rounded border border-borda-suave px-1 text-xs">/</kbd>
      </button>
    );
  }

  return (
    <div className="rounded-cartao border border-borda-estrutural bg-surface p-3 shadow-flutuante">
      <div className="flex items-center gap-2">
        <span aria-hidden="true" className="text-texto-suave">
          ⌕
        </span>

        <input
          ref={campo}
          type="search"
          value={termo}
          onChange={(evento) => {
            setTermo(evento.target.value);

            // O destaque volta ao topo junto com o termo. Aqui, e nao num efeito:
            // mudar estado dentro de efeito faz o React renderizar duas vezes por
            // tecla, e a regra do lint existe por isso.
            setDestaque(0);
          }}
          onKeyDown={(evento) => {
            if (evento.key === "ArrowDown") {
              evento.preventDefault();
              setDestaque(Math.min(escolhido + 1, achados.length - 1));
            }

            if (evento.key === "ArrowUp") {
              evento.preventDefault();
              setDestaque(Math.max(escolhido - 1, 0));
            }

            if (evento.key === "Enter" && achados[escolhido]) {
              evento.preventDefault();
              escolher(achados[escolhido].codigo);
            }
          }}
          placeholder="Nome ou sigla do órgão"
          aria-label="Buscar um órgão no mapa"
          className="min-w-0 flex-1 bg-transparent text-sm text-texto outline-none placeholder:text-texto-suave"
        />

        <button
          type="button"
          onClick={() => setAberta(false)}
          className="rounded-selo px-2 py-1 text-xs font-semibold text-texto-suave hover:bg-realce hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Fechar
        </button>
      </div>

      {termo.trim().length >= 2 ? (
        achados.length > 0 ? (
          <ul className="mt-2 space-y-px">
            {achados.map((no, i) => (
              <li key={no.codigo}>
                <button
                  type="button"
                  onClick={() => escolher(no.codigo)}
                  onMouseEnter={() => setDestaque(i)}
                  className={`flex w-full items-baseline gap-2 rounded-campo px-2 py-1.5 text-left text-sm ${
                    i === escolhido ? "bg-realce" : ""
                  }`}
                >
                  <span
                    aria-hidden="true"
                    className="size-2.5 shrink-0 translate-y-1 rounded-[2px]"
                    style={{ background: corDoPoder(no.chaveDoPoder) }}
                  />

                  <span className="min-w-0 flex-1 truncate text-texto">
                    <Realce texto={no.nome} termo={termo} />
                    {no.sigla ? (
                      <span className="ms-1.5 text-xs text-texto-suave">{no.sigla}</span>
                    ) : null}
                  </span>

                  <span className="numeros shrink-0 text-xs text-texto-suave">
                    {formatarNumero(no.unidadesSubordinadas)}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mt-2 px-2 py-1.5 text-sm text-texto-suave">
            {/*
              "Nenhum orgao com esse nome NO MAPA" — e nao "nao encontrado". O mapa mostra
              entes, orgaos e entidades; as 94 mil unidades administrativas nao estao aqui,
              e quem procura uma delas precisa saber que a ausencia e' do recorte.
            */}
            Nenhum órgão ou entidade com esse nome <strong>no mapa</strong>. As unidades
            administrativas não aparecem no desenho — procure pela página do órgão a que
            ela pertence.
          </p>
        )
      ) : (
        <p className="mt-2 px-2 py-1.5 text-xs text-texto-suave">
          Digite ao menos duas letras. {formatarNumero(nos.length)} órgãos e entidades no
          mapa.
        </p>
      )}
    </div>
  );
}

/** Marca o trecho que casou, para o olho achar onde bateu. */
function Realce({ texto, termo }: { texto: string; termo: string }) {
  const faixa = ondeCasa(texto, termo);

  if (!faixa) return <>{texto}</>;

  const [inicio, fim] = faixa;

  return (
    <>
      {texto.slice(0, inicio)}
      <mark className="bg-transparent font-semibold text-titulo">
        {texto.slice(inicio, fim)}
      </mark>
      {texto.slice(fim)}
    </>
  );
}
