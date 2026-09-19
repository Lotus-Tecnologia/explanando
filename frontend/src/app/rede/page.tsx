import type { Metadata } from "next";
import Link from "next/link";
import { LegendaDaRede } from "@/componentes/GrafoDaRede";
import {
  AgrupamentosDaRede,
  DuplasDaRede,
  FornecedoresDaRede,
  OQueORecorteMostra,
} from "@/componentes/PainelDaRede";
import { obterMapaDaRede } from "@/lib/api";
import { formatarNumero, formatarMoedaCompacta } from "@/lib/formato";
import { montarAgrupamentos } from "@/lib/grafoDaRede";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "Quem contrata com quem",
  description:
    "Os parlamentares que dividem fornecedores pouco compartilhados entre si — quem são, " +
    "de que estados, e quanto passa por essas empresas. Dividir fornecedor não é indício " +
    "de irregularidade.",
  alternates: { canonical: "/rede" },
};

/**
 * O mapa de fornecedores compartilhados — a vista de **pessoas** do mapa do Estado.
 *
 * **É a nossa resposta ao *Power map* da referência, com a aresta trocada.** Lá dois nomes
 * se ligam por aparecerem nos mesmos artigos de jornal, o que mede a imprensa e não o
 * Estado; não temos fonte de notícias, e comprar uma para desenhar uma nuvem de citações
 * mediria a cobertura jornalística. Aqui a aresta é fornecedor em comum: um fato da base,
 * com CNPJ, nota fiscal e valor.
 *
 * **O corte veio de medir, e a página mostra a medição.** Os fornecedores mais
 * compartilhados da base são companhias aéreas e postos de combustível de Brasília, que
 * atendem centenas de gabinetes; desenhar essas arestas ligaria quase o Congresso inteiro
 * por um fato que é geografia. Entram só os que atendem de dois a cinco parlamentares e
 * somam ao menos R$ 50 mil.
 *
 * **Nada aqui acusa ninguém** (ADR-0004). A ressalva vem da própria API, e não deste
 * arquivo, porque é uma afirmação sobre o que o dado significa — e afirmação dessas tem
 * dono e tem teste.
 */
export default async function Pagina() {
  const mapa = await obterMapaDaRede();

  if (!mapa) {
    return (
      <main className="mx-auto max-w-5xl px-4 py-16">
        <h1 className="font-display text-3xl font-bold text-titulo">Quem contrata com quem</h1>
        <p className="mt-4 text-texto">
          O mapa de fornecedores não pôde ser carregado agora. Tente de novo em instantes.
        </p>
      </main>
    );
  }

  const agrupamentos = montarAgrupamentos(mapa.parlamentares, mapa.pares);
  const ilhas = agrupamentos.filter((grupo) => grupo.nos.length >= 3);
  const duplas = agrupamentos.filter((grupo) => grupo.nos.length === 2);

  const noDesenho = agrupamentos.reduce((soma, grupo) => soma + grupo.nos.length, 0);
  const valorNoDesenho = agrupamentos.reduce((soma, grupo) => soma + grupo.valor, 0);

  return (
    <main>
      {/* ---------------------------------------------------------------- */}
      {/*  Abertura                                                         */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-b border-borda-suave bg-surface">
        <div className="mx-auto max-w-5xl px-4 py-12 sm:py-16">
          <p className="text-xs font-bold tracking-widest text-acento uppercase">
            Fornecedores em comum · dados abertos
          </p>

          <h1 className="mt-3 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-titulo sm:text-5xl">
            Quem contrata com quem
          </h1>

          <p className="mt-4 max-w-2xl text-base text-texto">
            <strong className="font-semibold text-titulo">
              {formatarNumero(noDesenho)} parlamentares
            </strong>{" "}
            dividem, dois a dois, mais de um fornecedor pouco compartilhado — agências de
            comunicação, consultorias, gráficas. Somados, esses contratos passam de{" "}
            <strong className="font-semibold text-titulo">
              {formatarMoedaCompacta(valorNoDesenho)}
            </strong>
            .
          </p>

          {/*
            A ressalva vem antes do desenho, e nao depois. Um mapa de pessoas ligadas por
            dinheiro e' lido como acusacao por padrao, e a correcao so' funciona se chegar
            ao leitor primeiro.
          */}
          <p className="mt-5 max-w-2xl rounded-cartao border border-borda-suave bg-fundo p-4 text-sm text-texto">
            {mapa.ressalva}
          </p>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Por que este corte                                               */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        <h2 className="font-display text-2xl font-bold text-titulo">
          Por que quase nenhum fornecedor entra aqui
        </h2>

        <p className="mt-3 max-w-2xl text-sm text-texto">
          A base tem{" "}
          <strong className="font-semibold text-titulo">
            {formatarNumero(mapa.fornecedoresNaBase)} fornecedores
          </strong>
          , dos quais{" "}
          <strong className="font-semibold text-titulo">
            {formatarNumero(mapa.fornecedoresCompartilhados)}
          </strong>{" "}
          atendem mais de um parlamentar.
          {mapa.maiorFornecedorCompartilhado === null ? null : (
            <>
              {" "}
              O mais compartilhado de todos é a{" "}
              <strong className="font-semibold text-titulo">
                {mapa.maiorFornecedorCompartilhado}
              </strong>
              , com {formatarNumero(mapa.parlamentaresAtendidosPeloMaior)} gabinetes.
            </>
          )}{" "}
          {mapa.porQueEsteCorte}
        </p>

        <div className="mt-8">
          <OQueORecorteMostra mapa={mapa} />
        </div>

        <p className="mt-5 max-w-2xl text-sm text-texto-suave">{mapa.oQueADensidadeMostra}</p>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Os agrupamentos                                                  */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-y border-borda-suave bg-surface/40">
        <div className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold text-titulo">
            {formatarNumero(ilhas.length)} grupos de três ou mais
          </h2>

          <p className="mt-3 max-w-2xl text-sm text-texto">
            Cada quadro é um grupo fechado: todos ali se ligam entre si, e a nenhum parlamentar
            de outro quadro. O tamanho do retrato é quantas ligações a pessoa tem dentro do
            grupo.
          </p>

          <div className="mt-5">
            <LegendaDaRede />
          </div>

          <div className="mt-8">
            <AgrupamentosDaRede agrupamentos={ilhas} />
          </div>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  As duplas                                                        */}
      {/* ---------------------------------------------------------------- */}
      {duplas.length === 0 ? null : (
        <section className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold text-titulo">
            {formatarNumero(duplas.length)} duplas
          </h2>

          <p className="mt-3 max-w-2xl text-sm text-texto">
            Dois parlamentares que dividem fornecedores um com o outro, e com mais ninguém nesta
            base.
          </p>

          <div className="mt-6">
            <DuplasDaRede agrupamentos={duplas} />
          </div>
        </section>
      )}

      {/* ---------------------------------------------------------------- */}
      {/*  A tabela                                                         */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-t border-borda-suave">
        <div className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold text-titulo">Os fornecedores</h2>

          <p className="mt-3 max-w-2xl text-sm text-texto">
            O grafo mostra as pessoas e esconde a empresa dentro da linha. Aqui estão as
            empresas — as de maior valor entre as {formatarNumero(mapa.ligacoes.length)} que
            atendem de dois a cinco gabinetes.
          </p>

          <div className="mt-6">
            <FornecedoresDaRede ligacoes={mapa.ligacoes} parlamentares={mapa.parlamentares} />
          </div>

          <p className="mt-8 text-sm text-texto-suave">
            Esta página é a vista de pessoas do{" "}
            <Link href="/estado" className="font-medium text-titulo hover:underline">
              mapa do Estado
            </Link>
            , que mostra as instituições.
          </p>
        </div>
      </section>
    </main>
  );
}
