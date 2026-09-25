"use client";

import { useSyncExternalStore } from "react";

/**
 * O botão que troca claro e escuro.
 *
 * **Três estados, não dois.** Quem nunca escolheu segue `prefers-color-scheme` e não tem
 * nada marcado no `<html>`; quem escolheu carrega `data-theme` e vence a preferência do
 * sistema nos dois sentidos. Tratar como booleano quebraria o caso mais comum — sistema no
 * escuro, pessoa que quer claro.
 *
 * O tema é lido com `useSyncExternalStore`, e não com `useState` dentro de um `useEffect`.
 * O motivo não é estilo: o tema mora **fora do React** — no atributo do `<html>` e na
 * preferência do sistema operacional —, e é para exatamente essa situação que este gancho
 * existe. Ele também resolve a hidratação sozinho, porque aceita um instantâneo de servidor
 * separado do de cliente.
 */

const CHAVE = "explanando-tema";

/** Quem precisa ser avisado quando o clique muda o atributo — o sistema avisa sozinho. */
const ouvintes = new Set<() => void>();

function assinar(aoMudar: () => void): () => void {
  ouvintes.add(aoMudar);

  const consulta = window.matchMedia?.("(prefers-color-scheme: dark)");
  consulta?.addEventListener("change", aoMudar);

  return () => {
    ouvintes.delete(aoMudar);
    consulta?.removeEventListener("change", aoMudar);
  };
}

function lerDoNavegador(): boolean {
  const marcado = document.documentElement.getAttribute("data-theme");

  if (marcado === "dark") return true;
  if (marcado === "light") return false;

  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
}

/**
 * No servidor não há `<html>` montado nem preferência de sistema para consultar.
 *
 * Claro é o padrão do produto, então é ele que vai no HTML inicial. Se a pessoa escolheu
 * escuro, o script do `<head>` já pintou o fundo certo antes da hidratação — o que pode
 * piscar é o rótulo do botão, por um quadro, e não a página.
 */
function lerNoServidor(): boolean {
  return false;
}

export function AlternadorDeTema() {
  const escuro = useSyncExternalStore(assinar, lerDoNavegador, lerNoServidor);

  function alternar() {
    const proximo = escuro ? "light" : "dark";

    document.documentElement.setAttribute("data-theme", proximo);

    try {
      localStorage.setItem(CHAVE, proximo);
    } catch {
      // Janela privada ou armazenamento bloqueado: a escolha vale só nesta visita, e isso
      // é melhor que não deixar escolher.
    }

    // O atributo não dispara evento; o aviso é nosso.
    for (const ouvinte of ouvintes) ouvinte();
  }

  return (
    <button
      type="button"
      onClick={alternar}
      aria-pressed={escuro}
      className="inline-flex shrink-0 items-center gap-2 border border-borda-suave bg-surface px-3 py-1.5 text-xs font-semibold text-texto hover:border-borda-estrutural focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
    >
      <span aria-hidden="true" className="text-sm leading-none">
        {escuro ? "☀" : "☾"}
      </span>
      {/*
        No celular, só o ícone: com o texto, o botão caía numa terceira linha do cabeçalho,
        abaixo do logo e dos links. O nome continua lá para leitor de tela.
      */}
      <span className="sr-only sm:not-sr-only">{escuro ? "Modo claro" : "Modo escuro"}</span>
    </button>
  );
}

/**
 * O script que aplica o tema **antes da primeira pintura**.
 *
 * Sem ele, quem escolheu escuro recebe um lampejo branco em toda navegação: o React só
 * hidrata depois que o navegador já pintou. Fica inline no `<head>` de propósito — um
 * arquivo externo chegaria tarde demais.
 *
 * Falha em silêncio se o armazenamento estiver bloqueado, e aí a página segue a preferência
 * do sistema, que é o padrão correto.
 */
export const SCRIPT_DE_TEMA = `
try {
  var t = localStorage.getItem(${JSON.stringify(CHAVE)});
  if (t === "dark" || t === "light") document.documentElement.setAttribute("data-theme", t);
} catch (e) {}
`.trim();
