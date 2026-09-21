"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
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

  // `startsWith` e não igualdade: `/camara/indice` também é a Câmara, e comparar exato
  // deixaria a aba apagada nas duas ordenações internas.
  const casaAtual = CASAS.find((casa) => caminho.startsWith(casa.caminho)) ?? null;

  return (
    <header className="sticky top-0 z-30 border-b border-borda-suave bg-fundo/90 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
        <Link
          href="/"
          className="flex items-center gap-2.5 font-display text-lg font-bold tracking-tight text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          <span
            aria-hidden="true"
            className="flex size-7 items-center justify-center rounded-campo bg-titulo text-white shadow-xs"
          >
            <svg
              className="size-4"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <circle cx="12" cy="12" r="3" />
              <path d="M3 12h6m6 0h6M12 3v6m0 6v6" />
            </svg>
          </span>
          <span>Explanando</span>
        </Link>

        <nav aria-label="Seções" className="flex items-center gap-1">
          {CASAS.map((casa) => (
            <Aba
              key={casa.chave}
              href={casa.caminho}
              rotulo={casa.rotulo}
              ativa={casa.chave === casaAtual?.chave}
            />
          ))}

          {/*
            O Estado NÃO é uma terceira casa, e o separador diz isso antes de qualquer
            palavra: Câmara e Senado respondem "quanto se gastou"; o mapa responde "que
            órgãos existem". Pô-lo na mesma fileira sem marca faria parecer que há três
            casas legislativas.
          */}
          <span aria-hidden="true" className="mx-1 h-5 w-px bg-borda-suave" />

          {/*
            As duas vistas do Estado — instituicoes e quem comanda — moram DENTRO de
            /estado, e trocam no pe' do desenho. Nao ha' aba propria: sao a mesma pergunta
            vista de dois angulos, e separa-las no menu as fazia parecer secoes distintas.
          */}
          <Aba href="/estado" rotulo="Estado" ativa={caminho.startsWith("/estado")} />
        </nav>

        <div className="ms-auto">
          <AlternadorDeTema />
        </div>
      </div>

      {/*
        O submenu só existe dentro de uma casa. Na home ele não teria a que se referir — e
        mostrá-lo cinzento, sem destino, seria pior que não mostrar.
      */}
      {casaAtual ? <SubmenuDaCasa caminho={caminho} chaveDaCasa={casaAtual.chave} /> : null}
    </header>
  );
}

/** Uma aba do nível de cima. Existe para as três não divergirem de estilo com o tempo. */
function Aba({ href, rotulo, ativa }: { href: string; rotulo: string; ativa: boolean }) {
  return (
    <Link
      href={href}
      aria-current={ativa ? "page" : undefined}
      className={
        ativa
          ? "rounded-selo bg-realce px-3 py-1.5 text-sm font-semibold text-titulo"
          : "rounded-selo px-3 py-1.5 text-sm font-semibold text-texto-suave hover:bg-surface hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      }
    >
      {rotulo}
    </Link>
  );
}

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
    <nav
      aria-label={`Ordenações — ${casa.nomeOficial}`}
      className="border-t border-borda-suave bg-surface"
    >
      <ul className="mx-auto flex max-w-7xl flex-wrap gap-x-1 gap-y-0 overflow-x-auto px-4">
        {ORDENACOES.map((ordenacao) => {
          const destino = caminhoDoRanking(casa, ordenacao);

          /*
            Igualdade exata aqui, ao contrário do nível de cima: a raiz da casa É o ranking
            por volume, então `startsWith` marcaria "Volume gasto" como ativo também em
            `/camara/indice`.
          */
          const ativa = caminho === destino;

          return (
            <li key={ordenacao.chave}>
              <Link
                href={destino}
                aria-current={ativa ? "page" : undefined}
                className={
                  ativa
                    ? "-mb-px inline-block border-b-2 border-acento px-3 py-2.5 text-sm font-semibold whitespace-nowrap text-titulo"
                    : "-mb-px inline-block border-b-2 border-transparent px-3 py-2.5 text-sm font-medium whitespace-nowrap text-texto-suave hover:border-borda-estrutural hover:text-titulo focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo"
                }
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
