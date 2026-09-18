import type { Metadata } from "next";
import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { CASAS, type Casa } from "@/lib/casas";
import { formatarNumero, formatarReais } from "@/lib/formato";
import { ORDENACOES, caminhoDoRanking } from "@/lib/ordenacoes";
import { AlternadorDeTema } from "@/componentes/AlternadorDeTema";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "Explanando — gastos da cota parlamentar",
  description:
    "Quanto deputados e senadores consumiram da cota parlamentar, e os indicadores de um "
    + "motor de regras determinístico. Descreve inconsistências; não imputa conduta.",
  alternates: { canonical: "/" },
};

/**
 * A porta de entrada, e **não** um ranking.
 *
 * A home era o ranking por volume da Câmara. Com as duas casas na base, ela não pode mais
 * ser: qualquer escolha faria uma das duas parecer a principal e a outra um apêndice — e a
 * decisão de produto é que são duas coisas separadas, não uma maior e uma menor.
 *
 * Separadas porque as cotas são. A CEAP e a CEAPS têm regras e valores próprios por UF;
 * ordenar as duas no mesmo eixo poria lado a lado números que não medem a mesma coisa. O
 * tamanho desigual — 513 contra 118 — é real e não vira hierarquia aqui.
 *
 * O que a home entrega, então, é a escolha: o que é o site, quanto cada casa gastou, e o
 * caminho para cada ranking.
 */
export default async function Home() {
  const totais = await Promise.all(
    CASAS.map(async (casa) => ({ casa, itens: await ranquearTudo(casa, "volume") })),
  );

  return (
    <main className="mx-auto max-w-5xl px-4 py-8">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <h1 className="font-display text-3xl font-bold tracking-tight text-titulo">
          Gastos da cota parlamentar
        </h1>

        <AlternadorDeTema />
      </div>

      <p className="mt-2 max-w-3xl text-sm">
        Dados públicos da Câmara dos Deputados e do Senado Federal. Um motor de regras
        determinístico cruza cada despesa com o cadastro do fornecedor e{" "}
        <strong>descreve inconsistências — não imputa conduta.</strong> A conclusão fica com
        quem lê, ou com a autoridade competente.
      </p>

      {/*
        Duas seções irmãs, na ordem em que as casas aparecem em `CASAS`. Nenhuma é "a
        principal": pôr uma delas num lugar de destaque contradiria a separação que o resto
        do produto sustenta.
      */}
      <div className="mt-8 grid gap-5 sm:grid-cols-2">
        {totais.map(({ casa, itens }) => (
          <SecaoDaCasa key={casa.chave} casa={casa} itens={itens} />
        ))}
      </div>

      <p className="mt-8 max-w-3xl text-xs text-texto-suave">
        Os dois rankings são separados porque as cotas são: a{" "}
        <strong>CEAP</strong> da Câmara e a <strong>CEAPS</strong> do Senado têm regras e
        valores próprios por unidade federativa. Comparar um deputado a um senador no mesmo
        eixo poria lado a lado números que não medem a mesma coisa.
      </p>
    </main>
  );
}

function SecaoDaCasa({
  casa,
  itens,
}: {
  casa: Casa;
  itens: Awaited<ReturnType<typeof ranquearTudo>>;
}) {
  const total = itens?.reduce((soma, item) => soma + item.volumeTotalGasto, 0) ?? 0;

  return (
    <section
      aria-labelledby={`casa-${casa.chave}`}
      className="border border-borda-suave bg-fundo p-5"
    >
      <h2
        id={`casa-${casa.chave}`}
        className="font-display text-xl font-bold tracking-tight text-titulo"
      >
        {casa.nomeOficial}
      </h2>

      {/*
        Fonte fora do ar não vira número zero. "R$ 0,00" afirmaria que ninguém gastou, que é
        a mesma classe de erro do `Falhas: 0` da coleta: ausência apresentada como resultado.
      */}
      {itens === null ? (
        <p className="mt-2 text-sm text-abstencao-texto">
          Os números desta casa não puderam ser carregados agora. A falha é da plataforma,
          não do dado público.
        </p>
      ) : (
        <p className="mt-2 text-sm text-texto-suave">
          <strong className="numeros font-display text-lg text-titulo">
            {formatarNumero(itens.length)}
          </strong>{" "}
          {casa.integrantes} · cota {casa.cota}
          <br />
          <strong className="numeros font-display text-lg text-titulo">
            {formatarReais(total)}
          </strong>{" "}
          no período
        </p>
      )}

      {/*
        Os três caminhos precisam PARECER caminhos.

        Eram texto em negrito com sublinhado no `hover` — e em celular não existe hover.
        Numa página cuja única função é encaminhar, a rota que não se anuncia como rota é a
        falha mais cara possível. Agora cada uma é um alvo com borda, seta e área de toque
        cheia, com o mesmo vocabulário das pílulas da página de ranking.
      */}
      <ul className="mt-4 flex flex-col gap-2">
        {ORDENACOES.map((ordenacao) => (
          <li key={ordenacao.chave}>
            <Link
              href={caminhoDoRanking(casa, ordenacao)}
              className="flex items-center justify-between gap-3 border border-borda-suave bg-surface px-3 py-2.5 hover:border-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
            >
              <span>
                <span className="block text-sm font-semibold text-titulo">
                  {ordenacao.rotulo}
                </span>
                <span className="block text-xs text-texto-suave">{ordenacao.resumo}</span>
              </span>

              <span aria-hidden="true" className="text-titulo">
                →
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
