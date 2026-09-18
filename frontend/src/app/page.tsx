import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { CASAS, type Casa } from "@/lib/casas";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { formatarNumero, formatarReais } from "@/lib/formato";
import { ORDENACOES, caminhoDoRanking } from "@/lib/ordenacoes";
import type { ItemDoRanking } from "@/lib/contrato";

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
 * A home era o ranking por volume da Câmara. Com as duas casas na base ela não pode mais
 * ser: qualquer escolha faria uma parecer a principal e a outra um apêndice — e a decisão de
 * produto é que são duas coisas separadas, não uma maior e uma menor.
 *
 * O que ela entrega é a escolha, e o contexto para fazê-la: o tamanho do que está sendo
 * auditado, quem lidera cada casa, e o que o motor faz antes de alguém clicar em qualquer
 * lugar.
 */
export default async function Home() {
  const casas = await Promise.all(
    CASAS.map(async (casa) => ({ casa, itens: await ranquearTudo(casa, "volume") })),
  );

  const tudo = casas.flatMap(({ itens }) => itens ?? []);

  const volume = tudo.reduce((soma, item) => soma + item.volumeTotalGasto, 0);
  const despesas = tudo.reduce((soma, item) => soma + item.despesasAvaliadas, 0);

  return (
    <main>
      {/* ---------------------------------------------------------------- */}
      {/*  Abertura                                                         */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-b border-borda-suave bg-surface">
        <div className="mx-auto max-w-5xl px-4 py-12 sm:py-16">
          <p className="text-xs font-bold tracking-widest text-acento uppercase">
            Utilidade pública · dados abertos
          </p>

          <h1 className="mt-3 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-titulo sm:text-5xl">
            Para onde vai a cota parlamentar
          </h1>

          <p className="mt-4 max-w-2xl text-base text-texto">
            Um motor de regras determinístico cruza cada despesa de deputados e senadores com
            o cadastro do fornecedor na Receita Federal e{" "}
            <strong>descreve inconsistências — não imputa conduta.</strong> A conclusão fica
            com quem lê, ou com a autoridade competente.
          </p>

          {tudo.length > 0 ? (
            <dl className="mt-8 flex flex-wrap gap-x-10 gap-y-5">
              <Resumo rotulo="Parlamentares" valor={formatarNumero(tudo.length)} />
              <Resumo rotulo="Despesas analisadas" valor={formatarNumero(despesas)} />
              <Resumo rotulo="Volume no período" valor={formatarReais(volume)} />
            </dl>
          ) : null}
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  As duas casas                                                    */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
          Escolha uma casa
        </h2>

        <p className="mt-2 max-w-2xl text-sm text-texto-suave">
          Os rankings são separados porque as cotas são. A <strong>CEAP</strong> da Câmara e a{" "}
          <strong>CEAPS</strong> do Senado têm regras e valores próprios por unidade
          federativa — compará-las no mesmo eixo poria lado a lado números que não medem a
          mesma coisa.
        </p>

        {/*
          Duas seções irmãs, do mesmo tamanho. 513 contra 118 é desproporção real, e ela não
          vira hierarquia aqui: pôr uma num lugar de destaque contradiria a separação que o
          resto do produto sustenta.
        */}
        <div className="mt-6 grid gap-5 lg:grid-cols-2">
          {casas.map(({ casa, itens }) => (
            <CartaoDaCasa key={casa.chave} casa={casa} itens={itens} />
          ))}
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Como o número aparece                                            */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-t border-borda-suave bg-surface">
        <div className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
            Como um indicador aparece
          </h2>

          <p className="mt-2 max-w-2xl text-sm text-texto-suave">
            Nenhum passo envolve opinião. São três, e o terceiro é o que separa este produto
            de uma lista de acusações.
          </p>

          {/*
            Numerado porque é mesmo uma sequência: cada passo só existe se o anterior
            aconteceu. Não é ornamento de seção.
          */}
          <ol className="mt-6 grid gap-4 sm:grid-cols-3">
            <Passo
              numero="1"
              titulo="Coleta"
              texto="As despesas vêm das APIs oficiais da Câmara e do Senado, e o cadastro dos fornecedores vem da Receita Federal. Nada é digitado à mão."
            />
            <Passo
              numero="2"
              titulo="Cruzamento"
              texto="Doze regras determinísticas comparam cada despesa com o ramo de atividade do fornecedor, o histórico do parlamentar e o calendário legislativo."
            />
            <Passo
              numero="3"
              titulo="Abstenção"
              texto="Quando falta dado público para avaliar, a regra se cala — e a página diz que se calou. Ausência de indicador nunca é apresentada como aprovação."
            />
          </ol>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  A ressalva que precede tudo                                      */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        <div className="rounded-cartao border border-borda-suave bg-fundo p-6 shadow-cartao">
          <h2 className="font-display text-xl font-bold tracking-tight text-titulo">
            O que um indicador não é
          </h2>

          <p className="mt-3 max-w-3xl text-sm text-texto">
            Um indicador aponta que <strong>alguma coisa destoa do padrão</strong> — um ramo
            de atividade que não combina com o gasto, um valor repetido ao centavo por meses
            seguidos, uma nota emitida fora da base eleitoral. Nada disso é, por si,
            irregularidade: gastar a cota é legal, e a explicação pode ser trivial.
          </p>

          <p className="mt-3 max-w-3xl text-sm text-texto">
            Por isso cada achado traz{" "}
            <strong>o que a regra procurou e o documento oficial ao alcance do clique</strong>{" "}
            quando a fonte o publica. O que o site faz é mostrar o dado público organizado,
            não decidir por você.
          </p>
        </div>
      </section>
    </main>
  );
}

function Resumo({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold tracking-wider text-texto-suave uppercase">
        {rotulo}
      </dt>
      <dd className="numeros mt-0.5 font-display text-2xl font-bold tracking-tight text-titulo">
        {valor}
      </dd>
    </div>
  );
}

function Passo({
  numero,
  titulo,
  texto,
}: {
  numero: string;
  titulo: string;
  texto: string;
}) {
  return (
    <li className="rounded-cartao border border-borda-suave bg-fundo p-5">
      <span
        aria-hidden="true"
        className="numeros flex size-7 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo"
      >
        {numero}
      </span>

      <h3 className="mt-3 font-display text-base font-bold text-titulo">{titulo}</h3>
      <p className="mt-1.5 text-sm text-texto-suave">{texto}</p>
    </li>
  );
}

function CartaoDaCasa({ casa, itens }: { casa: Casa; itens: ItemDoRanking[] | null }) {
  const total = itens?.reduce((soma, item) => soma + item.volumeTotalGasto, 0) ?? 0;
  const primeiros = itens?.slice(0, 3) ?? [];

  return (
    <section
      aria-labelledby={`casa-${casa.chave}`}
      /*
        `min-w-0` não é enfeite: item de grade tem `min-width: auto`, e por isso se recusa a
        encolher abaixo do conteúdo. Sem ele, a linha do topo — retrato, nome e valor —
        empurrava o cartão para 387px numa tela de 375, e a página inteira ganhava rolagem
        horizontal. Medido antes de corrigir.
      */
      className="flex min-w-0 flex-col rounded-cartao border border-borda-suave bg-fundo p-6 shadow-cartao"
    >
      <h3
        id={`casa-${casa.chave}`}
        className="font-display text-xl font-bold tracking-tight text-titulo"
      >
        {casa.nomeOficial}
      </h3>

      {/*
        Fonte fora do ar não vira número zero. "R$ 0,00" afirmaria que ninguém gastou, que é
        a mesma classe de erro do `Falhas: 0` da coleta: ausência apresentada como resultado.
      */}
      {itens === null ? (
        <p className="mt-3 rounded-campo bg-abstencao-fundo p-3 text-sm text-abstencao-texto">
          Os números desta casa não puderam ser carregados agora. A falha é da plataforma, não
          do dado público.
        </p>
      ) : (
        <>
          <p className="mt-1 text-sm text-texto-suave">
            <strong className="numeros text-texto">{formatarNumero(itens.length)}</strong>{" "}
            {casa.integrantes} · cota {casa.cota} ·{" "}
            <strong className="numeros text-texto">{formatarReais(total)}</strong>
          </p>

          {primeiros.length > 0 ? (
            <ol className="mt-4 flex flex-col gap-2">
              {primeiros.map((item, indice) => (
                <PreviaDoTopo key={item.id} item={item} posicao={indice + 1} />
              ))}
            </ol>
          ) : null}
        </>
      )}

      {/*
        Os três caminhos precisam PARECER caminhos. Eram texto em negrito com sublinhado no
        `hover` — e em celular não existe hover. Numa página cuja função é encaminhar, a rota
        que não se anuncia como rota é a falha mais cara possível.
      */}
      <ul className="mt-5 flex flex-col gap-2">
        {ORDENACOES.map((ordenacao) => (
          <li key={ordenacao.chave}>
            <Link
              href={caminhoDoRanking(casa, ordenacao)}
              className="flex items-center justify-between gap-3 rounded-campo border border-borda-suave bg-surface px-3.5 py-2.5 hover:border-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
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

/**
 * Os três primeiros por volume, com retrato.
 *
 * É o que dá rosto à página — e, mais que isso, é a prova de que há dado de verdade atrás
 * dela. Uma home que só promete auditoria, sem mostrar uma linha do resultado, pede
 * confiança antes de dar motivo.
 */
function PreviaDoTopo({ item, posicao }: { item: ItemDoRanking; posicao: number }) {
  const retrato = retratoSeguro(item.fotoUrl);

  return (
    <li>
      <Link
        href={`/parlamentares/${item.id}`}
        className="flex min-w-0 items-center gap-3 rounded-campo px-2 py-1.5 hover:bg-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      >
        <span className="numeros w-3 shrink-0 text-xs text-texto-suave">{posicao}</span>

        {retrato ? (
          <Image
            src={retrato}
            alt=""
            width={32}
            height={32}
            unoptimized
            className="size-8 shrink-0 rounded-selo bg-surface object-cover"
          />
        ) : (
          <span
            aria-hidden="true"
            className="numeros flex size-8 shrink-0 items-center justify-center rounded-selo bg-realce text-[10px] font-bold text-titulo"
          >
            {iniciais(item.nomeParlamentar)}
          </span>
        )}

      {/*
        O valor vai na SEGUNDA linha, junto do partido — e não ao lado do nome.

        Lado a lado, numa tela de 375px, "R$ 2.287.735,08" comia tanto espaço que os nomes
        truncavam em "Pompeo d…" e "Albuquer…". Um ranking que corta o nome de quem ranqueia
        falha na única coisa que precisa entregar.
      */}
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm font-semibold text-titulo">
            {item.nomeParlamentar}
          </span>
          <span className="block truncate text-xs text-texto-suave">
            {item.siglaPartido} · {item.uf} ·{" "}
            <span className="numeros font-semibold text-texto">
              {formatarReais(item.volumeTotalGasto)}
            </span>
          </span>
        </span>
      </Link>
    </li>
  );
}
