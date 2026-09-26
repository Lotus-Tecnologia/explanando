"use client";

import { useEffect, useState } from "react";
import type { MudancaDoEstado } from "@/lib/contrato";
import { buscarMudancasDaUnidade } from "@/lib/mudancasDaUnidade";
import { CartaoDeMudanca } from "./PainelDoEstado";

type Estado =
  { fase: "carregando" } | { fase: "erro" } | { fase: "pronto"; mudancas: MudancaDoEstado[] };

/**
 * O que mudou nesta unidade — o feed da barra lateral.
 *
 * **É o lugar da referência ocupado por notícias, com o que nós de fato apuramos.** O
 * CivLab põe ali manchetes de terceiros; nós pomos a nossa própria linha do tempo: mudou
 * de nome, mudou de sigla, passou a constar, deixou de constar. É notícia de verdade sobre
 * o órgão, deduzida comparando duas coletas, e não depende de ninguém.
 *
 * **Busca sob demanda, e não junto com a página.** A lista geral do mapa é limitada às
 * mudanças mais recentes do Estado inteiro; filtrá-la por unidade no navegador devolveria
 * "nada mudou" para um órgão cujas mudanças ficaram fora do corte — que é outra afirmação,
 * e a errada.
 *
 * **Os três estados vazios são diferentes e a tela diz qual é qual.** Carregando, falha de
 * rede, e "não há mudança" não podem parecer a mesma coisa: só o terceiro é uma afirmação
 * sobre o Estado.
 */
export function MudancasDaUnidade({
  codigo,
  temHistorico,
}: {
  codigo: number;

  /**
   * Já houve uma segunda coleta?
   *
   * Sem isto, lista vazia seria lida como "nada mudou" quando significa "ainda não
   * comparamos" — os dois zeros que o ADR-0009 proíbe confundir, agora por unidade.
   */
  temHistorico: boolean;
}) {
  const [estado, setEstado] = useState<Estado>({ fase: "carregando" });

  /*
    O estado NÃO é reposto para "carregando" aqui dentro — quem repõe é a chave de
    montagem no chamador, que troca junto com a unidade e faz o React recriar o
    componente do zero. Chamar `setState` no corpo do efeito provoca uma renderização em
    cascata, e o `react-hooks/set-state-in-effect` reclama com razão.
  */
  useEffect(() => {
    let vivo = true;

    buscarMudancasDaUnidade(codigo).then((mudancas) => {
      // O guarda evita a resposta de uma unidade antiga chegar depois da nova e pintar
      // a barra com a historia do orgao errado.
      if (!vivo) return;

      setEstado(mudancas === null ? { fase: "erro" } : { fase: "pronto", mudancas });
    });

    return () => {
      vivo = false;
    };
  }, [codigo]);

  return (
    <section>
      <h3 className="text-xs font-bold tracking-widest text-texto-suave uppercase">
        O que mudou aqui
      </h3>

      <div className="mt-3">
        {estado.fase === "carregando" ? (
          <p className="text-sm text-texto-suave">Carregando…</p>
        ) : estado.fase === "erro" ? (
          <p className="text-sm text-texto-suave">
            O histórico desta unidade não pôde ser carregado agora.
          </p>
        ) : estado.mudancas.length === 0 ? (
          <p className="text-sm text-texto-suave">
            {temHistorico
              ? "Nada mudou nesta unidade desde a coleta anterior."
              : "Ainda não há com o que comparar. A fonte publica um retrato do agora e não " +
                "guarda histórico, então a primeira notícia só pode existir depois da " +
                "segunda coleta."}
          </p>
        ) : (
          <ol className="space-y-3">
            {estado.mudancas.map((mudanca, indice) => (
              <li key={`${mudanca.detectadaEm}-${indice}`}>
                <CartaoDeMudanca mudanca={mudanca} />
              </li>
            ))}
          </ol>
        )}
      </div>
    </section>
  );
}
