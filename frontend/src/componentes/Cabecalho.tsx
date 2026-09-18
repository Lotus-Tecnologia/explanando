"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { CASAS } from "@/lib/casas";
import { AlternadorDeTema } from "./AlternadorDeTema";

/**
 * O cabeçalho do site.
 *
 * **Faltava um.** Cada página abria com o próprio título e nada mais, então não havia como
 * sair de um ranking a não ser voltando pelo navegador — e quem chega de um link
 * compartilhado cai direto numa página interna, sem nunca ter visto a home.
 *
 * As duas casas ficam no topo, sempre, lado a lado e do mesmo tamanho. É a mesma regra que
 * governa a home e o sitemap: 513 contra 118 é desproporção real, e ela não vira hierarquia
 * na navegação.
 *
 * Fica grudado no topo porque a tabela de ranking é longa — quem rolou até o centésimo
 * deputado precisa poder trocar de casa sem subir de volta.
 */
export function Cabecalho() {
  const caminho = usePathname();

  return (
    <header className="sticky top-0 z-30 border-b border-borda-suave bg-fundo/90 backdrop-blur">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3">
        <Link
          href="/"
          className="font-display text-lg font-bold tracking-tight text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Explanando
        </Link>

        <nav aria-label="Casas legislativas" className="flex items-center gap-1">
          {CASAS.map((casa) => {
            /*
              `startsWith` e não igualdade: `/camara/indice` também é a Câmara, e marcar só a
              raiz deixaria a aba apagada nas duas ordenações internas.
            */
            const ativa = caminho.startsWith(casa.caminho);

            return (
              <Link
                key={casa.chave}
                href={casa.caminho}
                aria-current={ativa ? "page" : undefined}
                className={
                  ativa
                    ? "rounded-selo bg-realce px-3 py-1.5 text-sm font-semibold text-titulo"
                    : "rounded-selo px-3 py-1.5 text-sm font-semibold text-texto-suave hover:bg-surface hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
                }
              >
                {casa.rotulo}
              </Link>
            );
          })}
        </nav>

        <div className="ms-auto">
          <AlternadorDeTema />
        </div>
      </div>
    </header>
  );
}
