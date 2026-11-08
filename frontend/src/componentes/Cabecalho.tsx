"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { CASAS, casaPorChave } from "@/lib/casas";
import { ORDENACOES, caminhoDoRanking } from "@/lib/ordenacoes";
import { AlternadorDeTema } from "./AlternadorDeTema";

/**
 * O cabeçalho do site, em dois níveis.
 *
 * **Nível 1** são as casas. Elas ficam no topo, sempre, lado a lado e do mesmo tamanho — a
 * mesma regra que governa a home e o sitemap: 513 contra 118 é desproporção real, e ela não
 * vira hierarquia na navegação.
 *
 * **Nível 2** são as três ordenações da casa em que se está, e ele só aparece dentro de uma
 * casa. Era uma fileira de pílulas no corpo da página; virou submenu.
 *
 * <para>**Por que segunda linha e não menu suspenso.** As três ordenações são a ação
 * principal do site — é por elas que se escolhe o que olhar. Menu suspenso as esconderia
 * atrás de um gesto, e em toque esse gesto nem existe como o do mouse: precisaria de
 * abertura por clique, fechamento por Escape, fechamento por clique fora e devolução de
 * foco. Muito mecanismo para esconder o que deveria estar à vista.</para>
 *
 * Fica grudado no topo porque a tabela é longa: quem rolou até o centésimo deputado precisa
 * trocar de casa ou de ordenação sem subir de volta.
 */
export function Cabecalho() {
  const caminho = usePathname();
  const [aberto, setAberto] = useState(false);

  // `startsWith` e não igualdade: `/camara/indice` também é a Câmara, e comparar exato
  // deixaria a aba apagada nas duas ordenações internas.
  const casaAtual = CASAS.find((casa) => caminho.startsWith(casa.caminho)) ?? null;

  const abas = (
    <>
      {CASAS.map((casa) => (
        <Aba
          key={casa.chave}
          href={casa.caminho}
          rotulo={casa.rotulo}
          ativa={casa.chave === casaAtual?.chave}
          aoEscolher={() => setAberto(false)}
        />
      ))}
      {/*
        O Estado NÃO é uma terceira casa, e o separador diz isso antes de qualquer palavra:
        Câmara e Senado respondem "quanto se gastou"; o mapa responde "que órgãos existem".
      */}
      <span aria-hidden="true" className="mx-1 hidden h-5 w-px bg-borda-suave sm:block" />
      <Aba
        href="/estado"
        rotulo="Estado"
        ativa={caminho.startsWith("/estado")}
        aoEscolher={() => setAberto(false)}
      />
    </>
  );

  return (
    <header className="sticky top-0 z-30 border-b border-borda-suave bg-fundo">
      <div className="mx-auto flex max-w-6xl items-center gap-x-6 px-4 py-2.5">
        <Link
          href="/"
          className="flex min-h-11 items-center font-display text-xl font-bold tracking-tight text-titulo"
        >
          Explanando
        </Link>

        <nav aria-label="Seções" className="hidden items-center gap-1 sm:flex">
          {abas}
        </nav>

        <div className="ms-auto flex items-center gap-2">
          <AlternadorDeTema />
          {/* No celular as seções vão para trás de um botão: com elas à vista o cabeçalho
              ocupava três linhas antes do primeiro dado. */}
          <button
            type="button"
            aria-expanded={aberto}
            aria-controls="menu-do-celular"
            onClick={() => setAberto((v) => !v)}
            className="inline-flex size-11 items-center justify-center rounded-campo border border-borda-suave bg-surface text-texto sm:hidden"
          >
            <span className="sr-only">{aberto ? "Fechar menu" : "Abrir menu"}</span>
            <svg aria-hidden="true" viewBox="0 0 20 20" className="size-5">
              {aberto ? (
                <path d="m5 5 10 10M15 5 5 15" stroke="currentColor" strokeWidth="2" />
              ) : (
                <path d="M3 5h14M3 10h14M3 15h14" stroke="currentColor" strokeWidth="2" />
              )}
            </svg>
          </button>
        </div>
      </div>

      {aberto ? (
        <nav
          id="menu-do-celular"
          aria-label="Seções"
          className="flex flex-col gap-1 border-t border-borda-suave px-4 py-3 sm:hidden"
        >
          {abas}
        </nav>
      ) : null}

      {/*
        O submenu só existe dentro de uma casa. Na home ele não teria a que se referir — e
        mostrá-lo cinzento, sem destino, seria pior que não mostrar.
      */}
      {casaAtual ? <SubmenuDaCasa caminho={caminho} chaveDaCasa={casaAtual.chave} /> : null}
    </header>
  );
}

/** Uma aba do nível de cima. Existe para as três não divergirem de estilo com o tempo. */
function Aba({
  href,
  rotulo,
  ativa,
  aoEscolher,
}: {
  href: string;
  rotulo: string;
  ativa: boolean;
  aoEscolher: () => void;
}) {
  return (
    <Link
      href={href}
      onClick={aoEscolher}
      aria-current={ativa ? "page" : undefined}
      className={
        ativa
          ? "flex min-h-11 items-center rounded-campo bg-realce px-3 font-semibold text-texto"
          : "flex min-h-11 items-center rounded-campo px-3 font-semibold text-texto-suave hover:bg-surface hover:text-texto"
      }
    >
      {rotulo}
    </Link>
  );
}

/**
 * As três ordenações, num seletor segmentado. É a ação principal da casa, então fica à vista
 * e cabe em 375px sem rolar de lado: os rótulos curtos vêm de `rotuloCurto` quando existe.
 */
function SubmenuDaCasa({
  caminho,
  chaveDaCasa,
}: {
  caminho: string;
  chaveDaCasa: string;
}) {
  const casa = casaPorChave(chaveDaCasa);

  if (!casa) return null;

  return (
    <nav aria-label={`Ordenações — ${casa.nomeOficial}`} className="mx-auto max-w-6xl px-4 pb-2.5">
      <ul className="flex gap-1 rounded-campo border border-borda-suave bg-surface p-1 sm:inline-flex">
        {ORDENACOES.map((ordenacao) => {
          const destino = caminhoDoRanking(casa, ordenacao);

          /*
            Igualdade exata aqui, ao contrário do nível de cima: a raiz da casa É o ranking
            por volume, então `startsWith` marcaria o primeiro como ativo em `/camara/indice`.
          */
          const ativa = caminho === destino;

          return (
            <li key={ordenacao.chave} className="flex-1 sm:flex-none">
              <Link
                href={destino}
                aria-current={ativa ? "page" : undefined}
                className={`flex min-h-11 items-center justify-center rounded-[0.625rem] px-3 text-center text-sm leading-tight font-semibold ${
                  ativa ? "bg-realce text-texto" : "text-texto-suave hover:text-texto"
                }`}
              >
                {ordenacao.rotulo}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
