"use client";

import { useState } from "react";
import type { NoDoEstado } from "@/lib/contrato";
import {
  RAIO_DO_MIOLO,
  caminhoDaAresta,
  caminhoDaForma,
  formaDoNo,
  giroPara,
  montarGrafo,
  type NoPosicionado,
} from "@/lib/grafoRadial";
import { corDoPoder } from "@/lib/grafo";
import { formatarNumero } from "@/lib/formato";
import { PainelDaUnidade } from "./PainelDaUnidade";

/**
 * O mapa da estrutura do Estado, como grafo de nós e arestas.
 *
 * **Duas dimensões independentes.** O ângulo é o poder; o raio é a natureza jurídica —
 * ente federativo, administração direta, administração indireta, colegiado. O raio ser
 * natureza, e não profundidade na árvore, é o que impede um conselho ligado direto à
 * Presidência de parecer ter a estatura de um ministério.
 *
 * **Cada nó é um link de verdade.** O componente intercepta o clique para selecionar sem
 * sair da página, mas o `<a href>` embaixo continua lá: sem JavaScript, clicar leva à
 * página da unidade; com teclado, o nó é alcançável e ativável; com Ctrl ou botão do meio,
 * abre em outra aba. A interação é melhoria, não requisito — é a aposta do ADR-0001.
 *
 * **Forma, e não só cor.** Órgão é quadrado, entidade é hexágono, ente é círculo,
 * colegiado é losango. Quem não distingue as cores dos poderes distingue os contornos.
 */
export function GrafoDoEstado({ nos }: { nos: readonly NoDoEstado[] }) {
  const [selecionado, setSelecionado] = useState<number | null>(null);
  const [sobre, setSobre] = useState<number | null>(null);

  const grafo = montarGrafo(nos);

  if (grafo.nos.length === 0) return null;

  const porCodigo = new Map(grafo.nos.map((p) => [p.no.codigo, p]));
  const noMiolo = new Set(grafo.miolo.map((n) => n.codigo));

  const escolhido = selecionado === null ? null : porCodigo.get(selecionado);
  const giro = escolhido ? giroPara(escolhido.angulo) : 0;
  const giroEmGraus = (giro * 180) / Math.PI;

  // As arestas do nó selecionado, nos dois sentidos: quem manda nele e quem ele manda.
  const ligadas = new Set<number>();

  if (selecionado !== null) {
    for (const aresta of grafo.arestas) {
      if (aresta.de === selecionado) ligadas.add(aresta.para);
      if (aresta.para === selecionado) ligadas.add(aresta.de);
    }
  }

  const emFoco = (codigo: number) =>
    selecionado === null || codigo === selecionado || ligadas.has(codigo);

  const margem = 54;
  const lado = (grafo.raioExterno + margem) * 2;
  const destacado = sobre ?? selecionado;
  const rotulado = destacado === null ? null : porCodigo.get(destacado);

  return (
    <div>
      <figure className="m-0">
        <svg
          viewBox={`${-lado / 2} ${-lado / 2} ${lado} ${lado}`}
          className="mx-auto block h-auto w-full max-w-[42rem] touch-manipulation"
          role="img"
          aria-label={
            `Mapa da estrutura do Estado: ${formatarNumero(grafo.nos.length)} órgãos e ` +
            "entidades, dispostos por poder no ângulo e por natureza jurídica no raio. " +
            "A lista completa, navegável pelo teclado, vem logo abaixo do desenho."
          }
          onClick={() => setSelecionado(null)}
        >
          <g
            transform={`rotate(${giroEmGraus.toFixed(2)})`}
            style={{ transition: "transform 600ms cubic-bezier(0.22, 1, 0.36, 1)" }}
          >
            {/* Os anéis, como guias finíssimas. Sólidas: tracejado em grade lê-se como
                "projeção", e estas são posições reais. */}
            {grafo.aneis.map((anel) => (
              <circle
                key={anel.indice}
                r={anel.raio}
                fill="none"
                stroke="var(--color-borda-suave)"
                strokeWidth={1}
                vectorEffect="non-scaling-stroke"
                opacity={0.5}
              />
            ))}

            {/* As arestas. Quando há seleção, só as dele. */}
            {grafo.arestas.map((aresta) => {
              const de = porCodigo.get(aresta.de);
              const para = porCodigo.get(aresta.para);

              // Aresta que sai do miolo parte do centro.
              const origem = de ?? (noMiolo.has(aresta.de) ? { x: 0, y: 0 } : null);

              if (!origem || !para) return null;

              const relevante =
                selecionado === null ||
                aresta.de === selecionado ||
                aresta.para === selecionado;

              if (selecionado !== null && !relevante) return null;

              return (
                <path
                  key={`${aresta.de}-${aresta.para}`}
                  d={caminhoDaAresta(origem, para)}
                  fill="none"
                  stroke={corDoPoder(para.no.chaveDoPoder)}
                  strokeWidth={relevante && selecionado !== null ? 1.6 : 0.8}
                  vectorEffect="non-scaling-stroke"
                  opacity={selecionado === null ? 0.22 : 0.85}
                />
              );
            })}

            {/* Os nós. */}
            {grafo.nos.map((p) => (
              <NoDoGrafo
                key={p.no.codigo}
                posicionado={p}
                aceso={destacado === p.no.codigo}
                apagado={!emFoco(p.no.codigo)}
                aoEntrar={() => setSobre(p.no.codigo)}
                aoSair={() => setSobre(null)}
                aoEscolher={() => setSelecionado(p.no.codigo)}
              />
            ))}

            {/* O rótulo flutuante do nó apontado. Contra-girado para o texto ficar de pé
                mesmo com o disco virado. */}
            {rotulado ? (
              <g
                transform={`translate(${rotulado.x} ${rotulado.y}) rotate(${(-giroEmGraus).toFixed(2)})`}
                pointerEvents="none"
              >
                <RotuloFlutuante
                  texto={rotulado.no.nome}
                  cor={corDoPoder(rotulado.no.chaveDoPoder)}
                />
              </g>
            ) : null}
          </g>

          {/* O miolo não gira: é a raiz, e ela é o eixo. */}
          <circle
            r={RAIO_DO_MIOLO - 8}
            fill="var(--color-surface)"
            stroke="var(--color-acento)"
            strokeWidth={2}
            vectorEffect="non-scaling-stroke"
          />

          {grafo.miolo.map((no, i) => (
            <text
              key={no.codigo}
              textAnchor="middle"
              y={i === 0 ? -6 : 14}
              className={
                i === 0
                  ? "fill-titulo font-display text-[15px] font-bold"
                  : "fill-texto-suave text-[12px]"
              }
            >
              {no.sigla ?? no.nome}
            </text>
          ))}
        </svg>

        <figcaption className="mt-4 space-y-2 text-sm text-texto-suave">
          <p>
            O <strong className="font-semibold">ângulo</strong> é o poder; o{" "}
            <strong className="font-semibold">raio</strong> é a natureza jurídica — ente
            federativo no centro, depois administração direta e administração indireta. A
            forma do nó repete o tipo, para quem não distingue as cores. Passe o ponteiro
            para ver o nome; clique para isolar as ligações.
          </p>
          <p>
            Cada poder ocupa no mínimo 30° do círculo, senão o Legislativo — 4 órgãos
            contra 266 do Executivo — ficaria pequeno demais para ser clicado. A
            desproporção continua visível, e os números exatos estão na legenda.
          </p>
        </figcaption>
      </figure>

      {escolhido ? (
        <div className="mt-6">
          <PainelDaUnidade
            no={escolhido.no}
            /*
              O miolo entra na lista de ligados. Sem isto, selecionar o Poder Executivo
              mostrava a quem ele manda e escondia a quem ele responde — a Uniao mora no
              miolo, e o miolo nao esta' entre os nos posicionados.
            */
            ligados={[
              ...grafo.miolo.filter((n) => ligadas.has(n.codigo)),
              ...grafo.nos.filter((p) => ligadas.has(p.no.codigo)).map((p) => p.no),
            ]}
            aoFechar={() => setSelecionado(null)}
          />
        </div>
      ) : null}
    </div>
  );
}

function NoDoGrafo({
  posicionado,
  aceso,
  apagado,
  aoEntrar,
  aoSair,
  aoEscolher,
}: {
  posicionado: NoPosicionado;
  aceso: boolean;
  apagado: boolean;
  aoEntrar: () => void;
  aoSair: () => void;
  aoEscolher: () => void;
}) {
  const { no, x, y, tamanho } = posicionado;
  const cor = corDoPoder(no.chaveDoPoder);

  return (
    <a
      href={`/estado/${no.codigo}`}
      onClick={(evento) => {
        // Ctrl, Cmd e botão do meio continuam abrindo a página, como em qualquer link.
        if (evento.metaKey || evento.ctrlKey || evento.shiftKey) return;

        evento.preventDefault();
        evento.stopPropagation();
        aoEscolher();
      }}
      onMouseEnter={aoEntrar}
      onMouseLeave={aoSair}
      onFocus={aoEntrar}
      onBlur={aoSair}
      aria-label={`${no.nome} — ${no.tipo}${no.poder ? `, ${no.poder}` : ""}`}
    >
      <g transform={`translate(${x.toFixed(1)} ${y.toFixed(1)})`}>
        {/* Alvo generoso e invisível: um nó de 4px que exige mira é o defeito que isto
            evita. O raio de 15 dá ~30px de diâmetro no tamanho renderizado. */}
        <circle r={15} className="fill-transparent" />

        <path
          d={caminhoDaForma(formaDoNo(no.chaveDoTipo), aceso ? tamanho * 1.35 : tamanho)}
          fill={aceso ? cor : "var(--color-fundo)"}
          stroke={cor}
          strokeWidth={aceso ? 2 : 1.4}
          vectorEffect="non-scaling-stroke"
          opacity={apagado ? 0.18 : 1}
          style={{ transition: "opacity 200ms, d 120ms" }}
        />

        <title>
          {`${no.nome}${no.sigla ? ` (${no.sigla})` : ""} — ${no.tipo}` +
            `${no.poder ? `, ${no.poder}` : ""}` +
            `, ${formatarNumero(no.unidadesSubordinadas)} unidades subordinadas`}
        </title>
      </g>
    </a>
  );
}

/**
 * O rótulo que segue o ponteiro.
 *
 * Fundo opaco com a cor do poder no texto: sobre um feixe de arestas, texto sem fundo
 * vira ilegível na hora.
 */
function RotuloFlutuante({ texto, cor }: { texto: string; cor: string }) {
  /*
    O NOME, e não a sigla. "PE" não diz nada a quem está descobrindo o que é aquele ponto,
    e o rótulo existe justamente para isso.

    Truncado em 46 caracteres: "Instituto Federal de Educação, Ciência e Tecnologia do
    Sudeste de Minas Gerais" sairia do desenho pelos dois lados. O nome inteiro está no
    `<title>` e no painel.
  */
  const LIMITE = 46;
  const curto = texto.length > LIMITE ? `${texto.slice(0, LIMITE - 1).trimEnd()}…` : texto;

  // Largura estimada pelo número de caracteres. Medir exigiria o DOM, e este componente
  // também renderiza no servidor.
  const largura = Math.max(46, curto.length * 7.4 + 16);

  return (
    <>
      <rect
        x={-largura / 2}
        y={-38}
        width={largura}
        height={22}
        rx={6}
        fill="var(--color-surface)"
        stroke={cor}
        strokeWidth={1}
        vectorEffect="non-scaling-stroke"
      />
      <text
        textAnchor="middle"
        y={-23}
        className="fill-titulo text-[12px] font-semibold"
        style={{ paintOrder: "stroke" }}
      >
        {curto}
      </text>
    </>
  );
}

/**
 * A legenda dos poderes.
 *
 * Fica fora do SVG: dentro, o texto escala com o desenho e some no celular.
 */
export function LegendaDePoderes({
  poderes,
}: {
  poderes: readonly { rotulo: string; unidades: number }[];
}) {
  return (
    <ul className="flex flex-wrap gap-x-5 gap-y-2">
      {poderes.map((poder) => (
        <li key={poder.rotulo} className="flex items-center gap-2 text-sm">
          <span
            aria-hidden="true"
            className="size-3 shrink-0 rounded-[3px]"
            style={{ background: corDoPoder(poder.rotulo) }}
          />
          <span className="text-texto">{poder.rotulo}</span>
          <span className="numeros text-texto-suave">{formatarNumero(poder.unidades)}</span>
        </li>
      ))}
    </ul>
  );
}
