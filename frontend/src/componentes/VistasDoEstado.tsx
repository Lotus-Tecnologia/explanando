"use client";

import { useState } from "react";
import type { MapaDePoderCompleto, NoDoEstado } from "@/lib/contrato";
import { GrafoDoEstado } from "./GrafoDoEstado";
import { MapaDePoder, ProcedenciaDoMapa } from "./MapaDePoder";
import { corDoPoder } from "@/lib/grafo";

type Vista = "instituicoes" | "poder";

/**
 * As duas vistas do Estado, uma substituindo a outra no mesmo lugar.
 *
 * **É o `Graph | Power map` da referência, e o ponto é a substituição.** Duas páginas
 * separadas fariam o leitor perder o contexto a cada troca; aqui o desenho some e o outro
 * aparece no mesmo quadro, e o que estava selecionado num continua valendo no outro.
 *
 * **Clicar numa cunha do grafo abre o comando daquele poder.** É a ligação que a
 * referência não tem: lá o *Power map* é uma lista fechada de vinte pessoas; aqui ele
 * responde ao que o leitor apontou no mapa de instituições. Clicar no Executivo leva ao
 * Presidente e aos 38 ministros; clicar no Legislativo, à Mesa, às lideranças e às
 * presidências de comissão.
 *
 * **O poder escolhido sobrevive à troca de vista**, porque é a mesma pergunta vista de
 * dois ângulos. O que não sobrevive é a seleção de um nó — essa pertence ao grafo.
 */
export function VistasDoEstado({
  nos,
  poderes,
}: {
  nos: readonly NoDoEstado[];
  poderes: MapaDePoderCompleto | null;
}) {
  const [vista, setVista] = useState<Vista>("instituicoes");

  // O Executivo por padrao: e' o poder com 266 dos 336 orgaos e com os 39 ocupantes que
  // temos. Abrir o mapa de poder num setor vazio seria abrir numa explicacao de ausencia.
  const [poder, setPoder] = useState("executivo");

  const escolhido = poderes?.poderes.find((p) => p.chaveDoPoder === poder) ?? null;

  return (
    <div>
      <div className="relative">
        {vista === "instituicoes" ? (
          <GrafoDoEstado
            nos={nos}
            aoEscolherPoder={
              poderes === null
                ? undefined
                : (chave) => {
                    setPoder(chave);
                    setVista("poder");
                  }
            }
          />
        ) : (
          <div>
            <AbasDePoder mapa={poderes} atual={poder} aoEscolher={(chave) => setPoder(chave)} />

            <div className="mt-8">
              {escolhido === null ? (
                <p className="rounded-cartao border border-borda-suave bg-surface p-5 text-sm text-texto-suave">
                  O mapa de poder não pôde ser carregado agora.
                </p>
              ) : (
                <>
                  <MapaDePoder mapa={escolhido} />
                  <ProcedenciaDoMapa
                    mapa={escolhido}
                    ressalva={poderes?.ressalvaDaCuradoria ?? ""}
                  />
                </>
              )}
            </div>
          </div>
        )}
      </div>

      {/*
        O alternador fica ao pé do desenho, como na referência, e não no topo da página:
        é um controle do objeto que se está olhando, não da navegação do site.
      */}
      <div className="mt-6 flex justify-center">
        <Alternador
          vista={vista}
          aoTrocar={setVista}
          desabilitado={poderes === null ? "poder" : null}
        />
      </div>
    </div>
  );
}

/**
 * As abas de poder dentro do mapa de poder.
 *
 * Trazem a contagem de cadeiras, e o poder sem nenhuma aparece **apagado mas clicável**:
 * esconder o Judiciário faria parecer que ele não tem comando, e desabilitá-lo esconderia
 * a explicação de por que não sabemos quem o ocupa.
 */
function AbasDePoder({
  mapa,
  atual,
  aoEscolher,
}: {
  mapa: MapaDePoderCompleto | null;
  atual: string;
  aoEscolher: (chave: string) => void;
}) {
  if (mapa === null) return null;

  return (
    <div role="tablist" aria-label="Poder" className="flex flex-wrap gap-2">
      {mapa.poderes.map((poder) => {
        const ativo = poder.chaveDoPoder === atual;
        const vazio = poder.totalDeCadeiras === 0;

        return (
          <button
            key={poder.chaveDoPoder}
            type="button"
            role="tab"
            aria-selected={ativo}
            onClick={() => aoEscolher(poder.chaveDoPoder)}
            className={
              ativo
                ? "flex items-center gap-2 rounded-selo border border-borda-suave bg-surface px-3.5 py-2 text-sm font-semibold text-titulo"
                : "flex items-center gap-2 rounded-selo border border-transparent px-3.5 py-2 text-sm font-semibold text-texto-suave hover:text-titulo"
            }
          >
            <span
              aria-hidden="true"
              className="inline-block size-2.5 rounded-full"
              style={{
                background: corDoPoder(poder.chaveDoPoder),
                opacity: vazio ? 0.35 : 1,
              }}
            />
            {poder.poder}
            <span className={vazio ? "numeros opacity-50" : "numeros"}>
              {poder.totalDeCadeiras}
            </span>
          </button>
        );
      })}
    </div>
  );
}

function Alternador({
  vista,
  aoTrocar,
  desabilitado,
}: {
  vista: Vista;
  aoTrocar: (vista: Vista) => void;
  desabilitado: Vista | null;
}) {
  return (
    <div
      className="inline-flex rounded-selo border border-borda-suave bg-surface p-0.5"
      role="group"
      aria-label="Vista do mapa"
    >
      <Aba
        rotulo="Instituições"
        ativa={vista === "instituicoes"}
        aoClicar={() => aoTrocar("instituicoes")}
        inativa={desabilitado === "instituicoes"}
      />
      <Aba
        rotulo="Quem comanda"
        ativa={vista === "poder"}
        aoClicar={() => aoTrocar("poder")}
        inativa={desabilitado === "poder"}
      />
    </div>
  );
}

function Aba({
  rotulo,
  ativa,
  aoClicar,
  inativa,
}: {
  rotulo: string;
  ativa: boolean;
  aoClicar: () => void;
  inativa: boolean;
}) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      disabled={inativa}
      aria-pressed={ativa}
      className={
        ativa
          ? "rounded-selo bg-realce px-4 py-1.5 text-sm font-semibold text-titulo"
          : "rounded-selo px-4 py-1.5 text-sm font-semibold text-texto-suave hover:text-titulo disabled:cursor-not-allowed disabled:opacity-40"
      }
    >
      {rotulo}
    </button>
  );
}
