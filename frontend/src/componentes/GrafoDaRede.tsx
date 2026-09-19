"use client";

import { useId, useState } from "react";
import type { AgrupamentoDaRede, NoDaRede } from "@/lib/grafoDaRede";
import { nomeCurto } from "@/lib/grafoDaRede";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { formatarMoedaCompacta } from "@/lib/formato";

/**
 * Um agrupamento do mapa de fornecedores compartilhados.
 *
 * **A ideia é a da referência, com a aresta trocada.** No *Power map* do CivLab, retratos
 * circulares de pessoas se ligam por aparecerem nos mesmos artigos de jornal. Aqui se
 * ligam por dividirem fornecedores — o que é um fato da nossa base, com CNPJ, nota e
 * valor, e não uma medida da imprensa.
 *
 * **Nenhum agrupamento aqui acusa ninguém de nada** (ADR-0004). Dois gabinetes
 * contratarem a mesma agência é lícito e comum, e a página diz isso acima do desenho, com
 * a frase que vem da própria API. O que o desenho faz é mostrar *onde* isso acontece com
 * empresas que quase ninguém mais usa.
 *
 * **O que a cor e o traço significam**, e nada além disso:
 *
 * | Elemento | Significado |
 * |---|---|
 * | Espessura da linha | Quantos fornecedores os dois dividem |
 * | Linha tracejada e em destaque | Os dois são de **estados diferentes** — o caso raro |
 * | Tamanho do nó | Quantas ligações a pessoa tem dentro deste agrupamento |
 * | Anel em destaque no retrato | A pessoa é a **única** do seu estado na ilha |
 *
 * O destaque é sempre acento, nunca vermelho: vermelho é gravidade alta no resto do
 * produto, e aqui não há gravidade nenhuma a comunicar (ADR-0013).
 */
export function GrafoDaRede({
  agrupamento,
  aoEscolher,
}: {
  agrupamento: AgrupamentoDaRede;
  aoEscolher?: (id: string | null) => void;
}) {
  const [sobre, setSobre] = useState<string | null>(null);
  const prefixo = useId().replace(/:/g, "");

  const destacado = (id: string) => sobre === null || sobre === id;

  const arestaDestacada = (de: string, para: string) =>
    sobre === null || sobre === de || sobre === para;

  return (
    <svg
      viewBox={`0 0 ${agrupamento.largura} ${agrupamento.altura}`}
      className="h-auto w-full"
      role="img"
      aria-label={rotuloDoAgrupamento(agrupamento)}
    >
      <defs>
        {agrupamento.nos.map((no) => {
          const retrato = retratoSeguro(no.pessoa.fotoUrl);

          return retrato === null ? null : (
            <clipPath key={no.pessoa.id} id={`${prefixo}-${no.pessoa.id}`}>
              <circle cx={no.x} cy={no.y} r={no.raio - 1.5} />
            </clipPath>
          );
        })}
      </defs>

      {/* ------------------------------------------------------------------ */}
      {/*  As arestas primeiro, para os retratos ficarem por cima            */}
      {/* ------------------------------------------------------------------ */}
      <g>
        {agrupamento.arestas.map((aresta) => (
          <line
            key={`${aresta.de}-${aresta.para}`}
            x1={aresta.x1}
            y1={aresta.y1}
            x2={aresta.x2}
            y2={aresta.y2}
            stroke={aresta.mesmaUf ? "var(--color-borda-estrutural)" : "var(--color-acento)"}
            strokeWidth={aresta.espessura}
            strokeLinecap="round"
            /* Tracejado no par que cruza estados: cor E traço, nunca cor sozinha. */
            strokeDasharray={aresta.mesmaUf ? undefined : "7 4"}
            opacity={arestaDestacada(aresta.de, aresta.para) ? 0.62 : 0.12}
            style={{ transition: "opacity 180ms ease" }}
          />
        ))}
      </g>

      {/* ------------------------------------------------------------------ */}
      {/*  As pessoas                                                         */}
      {/* ------------------------------------------------------------------ */}
      <g>
        {agrupamento.nos.map((no) => (
          <NoDesenhado
            key={no.pessoa.id}
            no={no}
            prefixo={prefixo}
            aceso={destacado(no.pessoa.id)}
            aoEntrar={() => {
              setSobre(no.pessoa.id);
              aoEscolher?.(no.pessoa.id);
            }}
            aoSair={() => {
              setSobre(null);
              aoEscolher?.(null);
            }}
          />
        ))}
      </g>
    </svg>
  );
}

function NoDesenhado({
  no,
  prefixo,
  aceso,
  aoEntrar,
  aoSair,
}: {
  no: NoDaRede;
  prefixo: string;
  aceso: boolean;
  aoEntrar: () => void;
  aoSair: () => void;
}) {
  const retrato = retratoSeguro(no.pessoa.fotoUrl);
  const anel = no.forasteiro ? "var(--color-acento)" : "var(--color-borda-estrutural)";

  return (
    <a
      /*
        Um <a href> de verdade, e nao um onClick. Sem JavaScript, clicar no retrato ainda
        abre o perfil — e o robo de busca enxerga a ligacao entre as duas paginas.
      */
      href={`/parlamentares/${no.pessoa.id}`}
      onMouseEnter={aoEntrar}
      onMouseLeave={aoSair}
      onFocus={aoEntrar}
      onBlur={aoSair}
      style={{ opacity: aceso ? 1 : 0.28, transition: "opacity 180ms ease" }}
    >
      <title>
        {`${no.pessoa.nome} (${no.pessoa.partido}-${no.pessoa.uf}) — ` +
          `${no.grau} ${no.grau === 1 ? "ligação" : "ligações"} neste agrupamento`}
      </title>

      {/* O fundo: e' o que impede o retrato de ficar transparente sobre a aresta. */}
      <circle cx={no.x} cy={no.y} r={no.raio} fill="var(--color-surface)" />

      {retrato === null ? (
        <text
          x={no.x}
          y={no.y}
          textAnchor="middle"
          dominantBaseline="central"
          fontSize={no.raio * 0.78}
          fontWeight={700}
          fill="var(--color-texto-suave)"
        >
          {iniciais(no.pessoa.nome)}
        </text>
      ) : (
        <image
          href={retrato}
          x={no.x - no.raio}
          y={no.y - no.raio}
          width={no.raio * 2}
          height={no.raio * 2}
          clipPath={`url(#${prefixo}-${no.pessoa.id})`}
          preserveAspectRatio="xMidYMin slice"
        />
      )}

      <circle
        cx={no.x}
        cy={no.y}
        r={no.raio}
        fill="none"
        stroke={anel}
        strokeWidth={no.forasteiro ? 2.6 : 1.6}
      />

      {/*
        O rotulo sempre visivel, e nao so' no hover. O ADR-0013 pede rotulo alem da cor, e
        um mapa de PESSOAS em que os nomes so' aparecem ao passar o mouse nao se le no
        celular nem numa captura de tela.
      */}
      <text
        x={no.x}
        y={no.y + no.raio + 9}
        textAnchor="middle"
        fontSize={7.4}
        fontWeight={600}
        fill="var(--color-titulo)"
      >
        {nomeCurto(no.pessoa.nome)}
      </text>

      <text
        x={no.x}
        y={no.y + no.raio + 17}
        textAnchor="middle"
        fontSize={6.4}
        fill="var(--color-texto-suave)"
      >
        {`${no.pessoa.partido}-${no.pessoa.uf}`}
      </text>
    </a>
  );
}

/** A descrição do quadro para quem usa leitor de tela. */
export function rotuloDoAgrupamento(agrupamento: AgrupamentoDaRede): string {
  const pessoas = agrupamento.nos.length;
  const ufs = agrupamento.ufs.map((item) => item.uf).join(", ");

  return (
    `Agrupamento de ${pessoas} parlamentares de ${ufs}, ligados por ` +
    `${agrupamento.pares} ${agrupamento.pares === 1 ? "par" : "pares"} que dividem ` +
    `fornecedores, somando ${formatarMoedaCompacta(agrupamento.valor)}.`
  );
}

/**
 * A legenda do desenho.
 *
 * Fica ao lado do grafo, e não escondida atrás de um botão: sem ela, a linha tracejada é
 * decoração e o anel em destaque não quer dizer nada.
 */
export function LegendaDaRede() {
  return (
    <ul className="flex flex-wrap items-center gap-x-5 gap-y-2 text-xs text-texto-suave">
      <li className="flex items-center gap-2">
        <svg width="34" height="10" aria-hidden="true">
          <line
            x1="1"
            y1="5"
            x2="33"
            y2="5"
            stroke="var(--color-borda-estrutural)"
            strokeWidth="2"
            strokeLinecap="round"
          />
        </svg>
        Mesmo estado
      </li>

      <li className="flex items-center gap-2">
        <svg width="34" height="10" aria-hidden="true">
          <line
            x1="1"
            y1="5"
            x2="33"
            y2="5"
            stroke="var(--color-acento)"
            strokeWidth="2.6"
            strokeDasharray="7 4"
            strokeLinecap="round"
          />
        </svg>
        Estados diferentes
      </li>

      <li className="flex items-center gap-2">
        <svg width="34" height="12" aria-hidden="true">
          <line
            x1="2"
            y1="6"
            x2="14"
            y2="6"
            stroke="var(--color-borda-estrutural)"
            strokeWidth="1.4"
            strokeLinecap="round"
          />
          <line
            x1="20"
            y1="6"
            x2="32"
            y2="6"
            stroke="var(--color-borda-estrutural)"
            strokeWidth="5"
            strokeLinecap="round"
          />
        </svg>
        Linha mais grossa, mais fornecedores em comum
      </li>

      <li className="flex items-center gap-2">
        <svg width="16" height="16" aria-hidden="true">
          <circle
            cx="8"
            cy="8"
            r="6"
            fill="none"
            stroke="var(--color-acento)"
            strokeWidth="2.6"
          />
        </svg>
        Único do seu estado no grupo
      </li>
    </ul>
  );
}
