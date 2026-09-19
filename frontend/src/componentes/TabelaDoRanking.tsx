"use client";

import { useMemo, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import type { ItemDoRanking } from "@/lib/contrato";

/**
 * O ranking de uma casa: busca sobre o conjunto inteiro, exibição paginada, ordenação por
 * qualquer coluna.
 *
 * **A busca e a paginação não são a mesma coisa, e a diferença é uma decisão.** Antes, a
 * tabela trazia os 513 de uma vez sem paginar, e o motivo registrado era bom: paginar de 50
 * em 50 esconderia 463 pessoas atrás de dez cliques, num produto cujo leitor típico chega
 * procurando **um** parlamentar específico. Por isso a busca varre a lista completa, e não a
 * página visível. Digitou, achou. A contagem diz "X de Y" para que isso seja visível.
 *
 * **A ordenação por coluna é outra decisão, e tem uma ressalva.** A ordem que o servidor
 * manda é a do ADR-0009, e ela exclui do índice quem está abaixo do piso amostral — ordenar
 * por um índice que não se sustenta seria publicar ruído com nome e foto. Reordenar aqui não
 * pode desfazer isso, então **nulo vai sempre para o fim, nos dois sentidos**: quem não tem
 * densidade medida não é ranqueado por ela, fica apartado e rotulado.
 *
 * A coluna `#` guarda a posição na ordenação canônica da página, e **não** o índice da linha
 * na tela. Reordenar por outra coluna deixa os números fora de sequência de propósito: é
 * assim que se vê que o 3º em volume é o 87º em densidade. Clicar em `#` devolve a ordem do
 * servidor.
 */

const POR_PAGINA = 25;

type ChaveDeOrdem = "posicao" | "nome" | "volume" | "densidade" | "altas" | "despesas";
type Direcao = "asc" | "desc";

interface Ordem {
  chave: ChaveDeOrdem;
  direcao: Direcao;
}

/** Dobra acento e caixa: ninguém digita "JOÃO ANDRÉ" com til para procurar. */
function dobrar(texto: string): string {
  return texto
    .normalize("NFD")
    .replace(/\p{M}/gu, "")
    .toLowerCase();
}

interface Linhado {
  item: ItemDoRanking;
  posicao: number;
}

/**
 * O valor de uma linha na chave pedida. `null` significa **não medido**, e é tratado à
 * parte na comparação — nunca como zero.
 */
function valorDe(linha: Linhado, chave: ChaveDeOrdem): number | string | null {
  switch (chave) {
    case "posicao":
      return linha.posicao;
    case "nome":
      return dobrar(linha.item.nomeParlamentar);
    case "volume":
      return linha.item.volumeTotalGasto;
    case "densidade":
      return linha.item.indiceDeRisco;
    case "altas":
      return linha.item.flagsAltas;
    case "despesas":
      return linha.item.despesasAvaliadas;
  }
}

function ordenar(linhas: Linhado[], ordem: Ordem): Linhado[] {
  const sinal = ordem.direcao === "asc" ? 1 : -1;

  return [...linhas].sort((a, b) => {
    const va = valorDe(a, ordem.chave);
    const vb = valorDe(b, ordem.chave);

    /*
      Nulo por último SEMPRE, e não por capricho de apresentação.

      O ADR-0009 exclui do ranking de risco quem está abaixo do piso amostral, porque a
      densidade de quem tem 20 despesas é ruído. Se o nulo participasse da comparação, o
      sentido decrescente o jogaria para o topo — e a tela publicaria como "mais arriscado"
      exatamente quem o motor se recusou a medir.
    */
    if (va === null && vb === null) return a.posicao - b.posicao;
    if (va === null) return 1;
    if (vb === null) return -1;

    if (typeof va === "string" || typeof vb === "string") {
      return sinal * String(va).localeCompare(String(vb), "pt-BR");
    }

    // Empate cai na ordem canônica do servidor, para a tabela não tremer entre renderizações.
    return va === vb ? a.posicao - b.posicao : sinal * (va - vb);
  });
}

function Retrato({ item }: { item: ItemDoRanking }) {
  const url = retratoSeguro(item.fotoUrl);

  if (!url) {
    return (
      <span
        aria-hidden="true"
        className="numeros flex size-9 shrink-0 items-center justify-center rounded-selo bg-realce text-[11px] font-bold text-titulo"
      >
        {iniciais(item.nomeParlamentar)}
      </span>
    );
  }

  return (
    <Image
      src={url}
      alt=""
      width={36}
      height={36}
      unoptimized
      /*
        `alt` vazio de propósito: o nome vem escrito ao lado, e um leitor de tela anunciando
        "foto de Fulano" logo antes de "Fulano" lê a mesma pessoa duas vezes.
      */
      className="size-9 shrink-0 rounded-selo bg-surface object-cover"
    />
  );
}

function Linha({ item, posicao }: Linhado) {
  return (
    <tr className="hover:bg-surface">
      <td className="numeros px-3 py-2.5 text-sm text-texto-suave">{posicao}</td>

      <td className="px-3 py-2.5">
        <div className="flex items-center gap-3">
          <Retrato item={item} />

          <span className="min-w-0">
            <Link
              href={`/parlamentares/${item.id}`}
              className="block truncate text-sm font-semibold text-titulo underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
            >
              {item.nomeParlamentar}
            </Link>
            <span className="block truncate text-xs text-texto-suave">
              {item.siglaPartido} · {item.uf}
              {/*
                O volume repetido aqui, e só no celular. A tabela tem seis colunas e rola na
                horizontal em tela estreita — o que empurrava justamente o VOLUME para fora,
                numa página que ordena por volume.
              */}
              <span className="sm:hidden">
                {" · "}
                <span className="numeros font-semibold text-texto">
                  {formatarReais(item.volumeTotalGasto)}
                </span>
              </span>
            </span>
          </span>
        </div>
      </td>

      <td className="numeros hidden px-3 py-2.5 text-right text-sm font-semibold whitespace-nowrap text-titulo sm:table-cell">
        {formatarReais(item.volumeTotalGasto)}
      </td>

      <td className="numeros hidden px-3 py-2.5 text-right text-sm sm:table-cell">
        {/*
          Nulo e zero são coisas diferentes. Abaixo do piso amostral a densidade não foi
          medida, e escrever "0,00" afirmaria que foi.
        */}
        {item.indiceDeRisco === null ? (
          <span
            className="text-xs text-abstencao-texto"
            title="Menos de 100 despesas analisadas: a densidade não é publicável."
          >
            amostra insuficiente
          </span>
        ) : (
          formatarDensidade(item.indiceDeRisco)
        )}
      </td>

      <td className="numeros px-3 py-2.5 text-right text-sm whitespace-nowrap">
        <span className="text-alta-texto">{item.flagsAltas}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-media-texto">{item.flagsMedias}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-texto-suave">{item.flagsBaixas}</span>
      </td>

      <td className="numeros hidden px-3 py-2.5 text-right text-sm text-texto-suave sm:table-cell">
        {formatarNumero(item.despesasAvaliadas)}
      </td>
    </tr>
  );
}

/**
 * Um cabeçalho que ordena.
 *
 * O `<th>` carrega `aria-sort` e o botão carrega o rótulo — é o par que faz um leitor de
 * tela anunciar "coluna ordenada em ordem decrescente" em vez de só ler o texto. A seta é
 * `aria-hidden` porque `aria-sort` já disse a mesma coisa, e ouvir duas vezes atrapalha.
 */
function CabecalhoOrdenavel({
  children,
  chave,
  ordem,
  aoOrdenar,
  alinhamento = "left",
  apenasLargas = false,
  padraoDescendente = true,
}: {
  children: React.ReactNode;
  chave: ChaveDeOrdem;
  ordem: Ordem;
  aoOrdenar: (chave: ChaveDeOrdem, padraoDescendente: boolean) => void;
  alinhamento?: "left" | "right";
  apenasLargas?: boolean;
  padraoDescendente?: boolean;
}) {
  const ativa = ordem.chave === chave;

  return (
    <th
      scope="col"
      aria-sort={ativa ? (ordem.direcao === "asc" ? "ascending" : "descending") : "none"}
      className={`bg-surface p-0 ${apenasLargas ? "hidden sm:table-cell" : ""}`}
    >
      <button
        type="button"
        onClick={() => aoOrdenar(chave, padraoDescendente)}
        className={`flex w-full items-center gap-1 px-3 py-2.5 text-[11px] font-bold tracking-wider uppercase hover:text-titulo focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo ${
          alinhamento === "right" ? "justify-end" : "justify-start"
        } ${ativa ? "text-titulo" : "text-texto-suave"}`}
      >
        {children}

        <span aria-hidden="true" className={ativa ? "text-acento" : "text-borda-estrutural"}>
          {ativa ? (ordem.direcao === "asc" ? "↑" : "↓") : "↕"}
        </span>
      </button>
    </th>
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
  const [pagina, setPagina] = useState(1);

  // O padrão é a ordem que o servidor mandou — a do ADR-0009 para esta página.
  const [ordem, setOrdem] = useState<Ordem>({ chave: "posicao", direcao: "asc" });

  // A posição é calculada ANTES de filtrar e de reordenar. Renumerar diria que a pessoa
  // filtrada é a primeira colocada, que é o oposto do que o número significa.
  const comPosicao = useMemo(
    () => itens.map((item, indice) => ({ item, posicao: indice + 1 })),
    [itens],
  );

  const encontrados = useMemo(() => {
    const procurado = dobrar(busca.trim());

    const filtrados =
      procurado === ""
        ? comPosicao
        : comPosicao.filter(({ item }) =>
            dobrar(`${item.nomeParlamentar} ${item.siglaPartido} ${item.uf}`).includes(
              procurado,
            ),
          );

    return ordenar(filtrados, ordem);
  }, [comPosicao, busca, ordem]);

  const totalDePaginas = Math.max(1, Math.ceil(encontrados.length / POR_PAGINA));

  // Buscar de novo pode encurtar a lista abaixo da página em que se estava; sem este limite
  // a tela ficaria vazia com a paginação apontando para o nada.
  const paginaAtual = Math.min(pagina, totalDePaginas);
  const inicio = (paginaAtual - 1) * POR_PAGINA;
  const visiveis = encontrados.slice(inicio, inicio + POR_PAGINA);

  function procurar(valor: string) {
    setBusca(valor);
    setPagina(1);
  }

  /**
   * Primeiro clique escolhe a coluna; o seguinte inverte.
   *
   * Número abre em decrescente e texto em crescente, porque é o que se quer em cada caso:
   * de dinheiro se procura o maior, de nome se procura o A. Abrir sempre do mesmo jeito
   * obrigaria a dois cliques em metade das colunas.
   */
  function alternarOrdem(chave: ChaveDeOrdem, padraoDescendente: boolean) {
    setOrdem((atual) =>
      atual.chave === chave
        ? { chave, direcao: atual.direcao === "asc" ? "desc" : "asc" }
        : { chave, direcao: padraoDescendente ? "desc" : "asc" },
    );

    setPagina(1);
  }

  return (
    <div className="mt-5">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0 flex-1 sm:max-w-sm">
          <label htmlFor="busca" className="block text-xs font-semibold text-texto-suave">
            Procurar parlamentar
          </label>

          <div className="relative mt-1">
            <span
              aria-hidden="true"
              className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-texto-suave"
            >
              ⌕
            </span>

            <input
              id="busca"
              type="search"
              value={busca}
              onChange={(evento) => procurar(evento.target.value)}
              placeholder="nome, partido ou UF"
              autoComplete="off"
              className="w-full rounded-campo border border-borda-suave bg-fundo py-2 pr-3 pl-8 text-sm text-texto placeholder:text-texto-suave focus-visible:border-titulo focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-titulo"
            />
          </div>
        </div>

        {/*
          A contagem vive numa região viva: quem usa leitor de tela precisa saber que a lista
          encolheu, e a mudança acontece longe do campo onde o foco está.

          Ela diz "X de Y" sempre que há busca — a garantia visível de que a paginação não
          esconde ninguém, porque a busca varreu a lista inteira e não só esta página.
        */}
        <p className="text-sm text-texto-suave" role="status" aria-live="polite">
          <strong className="numeros text-titulo">{formatarNumero(encontrados.length)}</strong>{" "}
          {encontrados.length === 1 ? "parlamentar" : "parlamentares"}
          {busca.trim() === "" ? "" : ` de ${formatarNumero(comPosicao.length)}`}
        </p>
      </div>

      <div className="mt-3 overflow-hidden rounded-cartao border border-borda-suave shadow-cartao">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse">
            <caption className="sr-only">
              Ranking de parlamentares por {rotulo.toLowerCase()}. Clique num cabeçalho para
              reordenar.
            </caption>

            <thead>
              <tr className="border-b-2 border-borda-estrutural">
                <CabecalhoOrdenavel
                  chave="posicao"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  padraoDescendente={false}
                >
                  #
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="nome"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  padraoDescendente={false}
                >
                  Parlamentar
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="volume"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Volume gasto
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="densidade"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Densidade
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="altas"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                >
                  Alta · Média · Baixa
                </CabecalhoOrdenavel>

                <CabecalhoOrdenavel
                  chave="despesas"
                  ordem={ordem}
                  aoOrdenar={alternarOrdem}
                  alinhamento="right"
                  apenasLargas
                >
                  Despesas
                </CabecalhoOrdenavel>
              </tr>
            </thead>

            <tbody className="divide-y divide-borda-suave">
              {visiveis.map(({ item, posicao }) => (
                <Linha key={item.id} item={item} posicao={posicao} />
              ))}
            </tbody>
          </table>
        </div>

        {/*
          "Nenhum parlamentar corresponde à busca" — e não "nenhum gasto encontrado". A
          segunda frase afirmaria algo sobre o dado que a busca não mediu.
        */}
        {encontrados.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-texto-suave">
            Nenhum parlamentar corresponde a <strong className="text-texto">{busca.trim()}</strong>.
          </p>
        ) : null}

        {totalDePaginas > 1 ? (
          <nav
            aria-label="Páginas do ranking"
            className="flex flex-wrap items-center justify-between gap-3 border-t border-borda-suave bg-surface px-4 py-3"
          >
            <p className="numeros text-xs text-texto-suave">
              {formatarNumero(inicio + 1)}–{formatarNumero(inicio + visiveis.length)} de{" "}
              {formatarNumero(encontrados.length)}
            </p>

            <div className="flex items-center gap-2">
              <BotaoDePagina
                aoClicar={() => setPagina(paginaAtual - 1)}
                desabilitado={paginaAtual <= 1}
              >
                ← Anterior
              </BotaoDePagina>

              <span className="numeros px-1 text-xs text-texto-suave">
                {paginaAtual} / {totalDePaginas}
              </span>

              <BotaoDePagina
                aoClicar={() => setPagina(paginaAtual + 1)}
                desabilitado={paginaAtual >= totalDePaginas}
              >
                Próxima →
              </BotaoDePagina>
            </div>
          </nav>
        ) : null}
      </div>
    </div>
  );
}

function BotaoDePagina({
  children,
  aoClicar,
  desabilitado,
}: {
  children: React.ReactNode;
  aoClicar: () => void;
  desabilitado: boolean;
}) {
  return (
    <button
      type="button"
      onClick={aoClicar}
      disabled={desabilitado}
      className="rounded-campo border border-borda-suave bg-fundo px-3 py-1.5 text-xs font-semibold text-titulo enabled:hover:border-titulo disabled:cursor-not-allowed disabled:text-texto-suave disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
    >
      {children}
    </button>
  );
}
