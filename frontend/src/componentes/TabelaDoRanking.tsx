"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import type { ItemDoRanking } from "@/lib/contrato";

/**
 * O ranking inteiro numa tabela, com busca no próprio navegador.
 *
 * A escolha de trazer os 513 de uma vez, em vez de paginar, é deliberada. A API pública não
 * tem endpoint de busca, e paginar de 50 em 50 esconderia 463 pessoas atrás de dez cliques —
 * num produto cujo leitor típico chega procurando **um** parlamentar específico. Com a lista
 * completa já na página, achar alguém custa uma digitação e nenhuma requisição, e a página
 * continua sendo um arquivo estático que a CDN serve (ADR-0001).
 *
 * A **ordem** continua vindo do servidor. Ordenar aqui duplicaria a regra do ADR-0009 — que
 * exclui do índice quem está abaixo do piso amostral — no lugar onde ela sairia de sincronia
 * sem ninguém perceber.
 */

/** Dobra acento e caixa: ninguém digita "JOÃO ANDRÉ" com til para procurar. */
function dobrar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase();
}

function Cabecalho({
  children,
  alinhamento = "left",
}: {
  children: React.ReactNode;
  alinhamento?: "left" | "right";
}) {
  return (
    <th
      scope="col"
      className={`sticky top-0 z-10 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-xs font-bold text-titulo ${
        alinhamento === "right" ? "text-right" : "text-left"
      }`}
    >
      {children}
    </th>
  );
}

function Linha({ item, posicao }: { item: ItemDoRanking; posicao: number }) {
  return (
    <tr className="odd:bg-fundo even:bg-surface">
      <td className="numeros px-3 py-2 text-sm text-texto-suave">{posicao}</td>

      <td className="px-3 py-2 text-sm">
        <Link
          href={`/parlamentares/${item.id}`}
          className="font-semibold text-titulo underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          {item.nomeParlamentar}
        </Link>
        <span className="ml-2 text-xs text-texto-suave">
          {item.siglaPartido} · {item.uf}
        </span>
      </td>

      <td className="numeros px-3 py-2 text-right text-sm font-semibold text-titulo">
        {formatarReais(item.volumeTotalGasto)}
      </td>

      <td className="numeros px-3 py-2 text-right text-sm">
        {/*
          Nulo e zero são coisas diferentes. Abaixo do piso amostral a densidade não foi
          medida, e escrever "0,00" afirmaria que foi.
        */}
        {item.indiceDeRisco === null ? (
          <span
            className="text-xs text-texto-suave"
            title="Menos de 100 despesas analisadas: a densidade não é publicável."
          >
            amostra insuficiente
          </span>
        ) : (
          formatarDensidade(item.indiceDeRisco)
        )}
      </td>

      <td className="numeros px-3 py-2 text-right text-sm">
        <span className="text-alta-texto">{item.flagsAltas}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-media-texto">{item.flagsMedias}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-texto-suave">{item.flagsBaixas}</span>
      </td>

      <td className="numeros px-3 py-2 text-right text-sm text-texto-suave">
        {formatarNumero(item.despesasAvaliadas)}
      </td>
    </tr>
  );
}

export function TabelaDoRanking({
  itens,
  rotulo,
}: {
  itens: ItemDoRanking[];
  rotulo: string;
}) {
  const [busca, setBusca] = useState("");

  // A posição é calculada ANTES do filtro. Renumerar de 1 diria que a pessoa filtrada é a
  // primeira colocada, que é o oposto do que o número significa.
  const comPosicao = useMemo(
    () => itens.map((item, indice) => ({ item, posicao: indice + 1 })),
    [itens],
  );

  const visiveis = useMemo(() => {
    const procurado = dobrar(busca.trim());

    if (procurado === "") {
      return comPosicao;
    }

    return comPosicao.filter(({ item }) =>
      dobrar(`${item.nomeParlamentar} ${item.siglaPartido} ${item.uf}`).includes(procurado),
    );
  }, [comPosicao, busca]);

  return (
    <>
      <div className="mt-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <label htmlFor="busca" className="block text-xs font-semibold text-texto-suave">
            Procurar parlamentar
          </label>
          <input
            id="busca"
            type="search"
            value={busca}
            onChange={(evento) => setBusca(evento.target.value)}
            placeholder="nome, partido ou UF"
            autoComplete="off"
            className="mt-1 w-72 max-w-full border border-borda-suave px-3 py-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-titulo"
          />
        </div>

        {/*
          A contagem vive numa região viva: quem usa leitor de tela precisa saber que a
          lista encolheu, e a mudança acontece longe do campo onde o foco está.
        */}
        <p className="text-sm text-texto-suave" role="status" aria-live="polite">
          <strong className="numeros text-titulo">{formatarNumero(visiveis.length)}</strong>{" "}
          {visiveis.length === 1 ? "parlamentar" : "parlamentares"}
          {busca.trim() === "" ? "" : ` de ${formatarNumero(comPosicao.length)}`}
        </p>
      </div>

      <div className="mt-3 overflow-x-auto border border-borda-suave">
        <table className="w-full border-collapse">
          <caption className="sr-only">
            Ranking de parlamentares por {rotulo.toLowerCase()}
          </caption>
          <thead>
            <tr>
              <Cabecalho>#</Cabecalho>
              <Cabecalho>Parlamentar</Cabecalho>
              <Cabecalho alinhamento="right">Volume gasto</Cabecalho>
              <Cabecalho alinhamento="right">Densidade</Cabecalho>
              <Cabecalho alinhamento="right">Alta · Média · Baixa</Cabecalho>
              <Cabecalho alinhamento="right">Despesas</Cabecalho>
            </tr>
          </thead>
          <tbody>
            {visiveis.map(({ item, posicao }) => (
              <Linha key={item.id} item={item} posicao={posicao} />
            ))}
          </tbody>
        </table>
      </div>

      {/*
        "Nenhum parlamentar corresponde à busca" — e não "nenhum gasto encontrado". A segunda
        frase afirmaria algo sobre o dado que a busca não mediu.
      */}
      {visiveis.length === 0 ? (
        <p className="mt-3 text-sm text-texto-suave">
          Nenhum parlamentar corresponde a <strong>{busca.trim()}</strong>.
        </p>
      ) : null}
    </>
  );
}
