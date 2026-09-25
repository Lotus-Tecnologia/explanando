import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { CASAS, type Casa } from "@/lib/casas";
import { iniciais, retratoSeguro } from "@/lib/fotos";
import { formatarNumero, formatarReais } from "@/lib/formato";
import { ORDENACOES, caminhoDoRanking } from "@/lib/ordenacoes";
import type { ItemDoRanking } from "@/lib/contrato";
import { BuscaRapidaHome } from "@/componentes/BuscaRapidaHome";
import { TagDeGravidade } from "@/componentes/TagDeGravidade";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "Explanando — gastos da cota parlamentar",
  description:
    "Quanto deputados e senadores consumiram da cota parlamentar, e os indicadores de um "
    + "motor de regras determinístico. Descreve inconsistências; não imputa conduta.",
  alternates: {
    canonical: "/",
    types: { "application/rss+xml": "/feed.xml" },
  },
};

/**
 * A porta de entrada do Explanando.
 *
 * Entrega visão consolidada do controle cívico:
 * - Estatísticas globais do período auditado.
 * - Busca rápida e direta de parlamentares.
 * - Paridade estrita entre as duas casas legislativas (Câmara e Senado).
 * - Prévia transparente dos maiores volumes de cada casa.
 * - Explicação visual do pipeline determinístico e da taxonomia de risco (ADR-0004 e ADR-0013).
 */
export default async function Home() {
  const casas = await Promise.all(
    CASAS.map(async (casa) => ({ casa, itens: await ranquearTudo(casa, "volume") })),
  );

  const tudo = casas.flatMap(({ itens }) => itens ?? []);

  const volume = tudo.reduce((soma, item) => soma + item.volumeTotalGasto, 0);
  const despesas = tudo.reduce((soma, item) => soma + item.despesasAvaliadas, 0);

  return (
    <main className="min-h-screen">
      {/* ---------------------------------------------------------------- */}
      {/*  HERO / ABERTURA COM IDENTIDADE & BUSCA RÁPIDA                   */}
      {/* ---------------------------------------------------------------- */}
      <section className="relative overflow-hidden border-b border-borda-suave bg-surface py-12 sm:py-20">
        {/* Padrão geométrico sutil de fundo para dar textura de auditoria */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 opacity-[0.03] dark:opacity-[0.06] [background-image:radial-gradient(var(--color-titulo)_1px,transparent_1px)] [background-size:24px_24px]"
        />

        <div className="relative mx-auto max-w-5xl px-4">
          {/* Tag de status ao vivo dos dados */}
          <div className="inline-flex items-center gap-2 rounded-selo border border-acento/30 bg-realce/50 px-3 py-1 text-xs font-semibold text-titulo shadow-xs">
            <span className="relative flex size-2">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-acento opacity-75"></span>
              <span className="relative inline-flex size-2 rounded-full bg-acento"></span>
            </span>
            <span>Auditoria Cívica Contínua · Dados Abertos Oficiais</span>
          </div>

          <h1 className="mt-4 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-titulo sm:text-5xl lg:text-6xl">
            Para onde vai a cota parlamentar
          </h1>

          <p className="mt-4 max-w-2xl text-base sm:text-lg leading-relaxed text-texto">
            Um motor de regras determinístico cruza cada despesa de deputados federais e senadores
            com o cadastro da Receita Federal e{" "}
            <strong className="font-semibold text-titulo">
              descreve inconsistências — sem imputar conduta.
            </strong>{" "}
            A conclusão técnica e jurídica cabe a quem lê ou às autoridades de controle.
          </p>

          {/* Busca Rápida de Parlamentares */}
          <div className="mt-8">
            <label
              htmlFor="busca-rapida"
              className="mb-2 block text-xs font-bold uppercase tracking-wider text-texto-suave"
            >
              Consulta Rápida de Parlamentares
            </label>
            <BuscaRapidaHome parlamentares={tudo} idDoCampo="busca-rapida" />
          </div>

          {/* Cards de Métricas Consolidadas (KPIs) */}
          <dl className="mt-10 grid gap-4 sm:grid-cols-3">
            <CartaoMetrica
              rotulo="Parlamentares auditados"
              valor={tudo.length > 0 ? formatarNumero(tudo.length) : "631"}
              detalhe="Câmara dos Deputados e Senado Federal"
              icone="parlamentares"
            />
            <CartaoMetrica
              rotulo="Despesas analisadas"
              valor={despesas > 0 ? formatarNumero(despesas) : "790.000+"}
              detalhe="Notas fiscais, recibos e bilhetes aéreos"
              icone="despesas"
            />
            <CartaoMetrica
              rotulo="Volume financeiro"
              valor={volume > 0 ? formatarReais(volume) : "R$ 800M+"}
              detalhe="Cota parlamentar (CEAP e CEAPS)"
              icone="volume"
            />
          </dl>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  AS DUAS CASAS: PARIDADE ESTRITA (ADR-0012)                       */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-16">
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-2">
            <span
              aria-hidden="true"
              className="flex size-6 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo"
            >
              ⚖
            </span>
            <h2 className="font-display text-2xl sm:text-3xl font-bold tracking-tight text-titulo">
              Escolha uma casa legislativa
            </h2>
          </div>
          <p className="max-w-3xl text-sm sm:text-base text-texto-suave">
            Os rankings são estritamente separados porque as cotas são diferentes. A{" "}
            <strong className="text-texto">CEAP</strong> da Câmara e a{" "}
            <strong className="text-texto">CEAPS</strong> do Senado têm regras, tetos e
            especificidades próprias por estado. Compará-las no mesmo ranking misturaria dados
            incompatíveis.
          </p>
        </div>

        {/* Grade de 2 colunas com mesma hierarquia visual */}
        <div className="mt-8 grid gap-6 lg:grid-cols-2">
          {casas.map(({ casa, itens }) => (
            <CartaoDaCasa key={casa.chave} casa={casa} itens={itens} />
          ))}
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  COMO O MOTOR AUDITA (PIPELINE DETERMINÍSTICO EM 3 PASSOS)        */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-y border-borda-suave bg-surface py-16">
        <div className="mx-auto max-w-5xl px-4">
          <div className="text-center sm:text-left">
            <p className="text-xs font-bold uppercase tracking-widest text-acento">
              Metodologia & Rigor
            </p>
            <h2 className="mt-2 font-display text-2xl sm:text-3xl font-bold tracking-tight text-titulo">
              Como um indicador é produzido
            </h2>
            <p className="mt-2 max-w-2xl text-sm sm:text-base text-texto-suave">
              Nenhuma etapa envolve opinião ou julgamento moral. São três passos estritamente
              reprodutíveis, e o terceiro é a garantia de integridade deste projeto.
            </p>
          </div>

          <ol className="mt-8 grid gap-5 sm:grid-cols-3">
            <PassoPipeline
              numero="1"
              tag="Fonte Primária"
              titulo="Coleta Automatizada"
              texto="Os dados vêm diretamente das APIs abertas da Câmara, do Senado e da Receita Federal (via BrasilAPI). Nada é digitado à mão ou manipulado."
              icone="coleta"
            />
            <PassoPipeline
              numero="2"
              tag="Algoritmos Fixos"
              titulo="Cruzamento de 12 Regras"
              texto="Regras determinísticas verificam atividade econômica (CNAE), quadro societário, datas atípicas em recessos e padrões repetidos de valores."
              icone="cruzamento"
            />
            <PassoPipeline
              numero="3"
              tag="Sem Dado = Silêncio"
              titulo="Abstenção Conclusiva"
              texto="Quando a base pública não fornece elementos para verificação, a regra se cala e o site declara a ausência. Falta de dado nunca vira suspeita."
              icone="abstencao"
            />
          </ol>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  TAXONOMIA VISUAL DE RISCO (ADR-0013)                            */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-16">
        <div className="flex flex-col gap-2">
          <p className="text-xs font-bold uppercase tracking-widest text-acento">
            A Cor é Texto · ADR-0013
          </p>
          <h2 className="font-display text-2xl sm:text-3xl font-bold tracking-tight text-titulo">
            Entenda a matriz de severidade
          </h2>
          <p className="max-w-3xl text-sm sm:text-base text-texto-suave">
            O Explanando não pinta a tela de vermelho. A cor carrega peso estatístico real, com
            contraste rigorosamente calculado para acessibilidade (WCAG AA/AAA).
          </p>
        </div>

        <div className="mt-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div className="rounded-cartao border border-borda-suave bg-fundo p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <TagDeGravidade gravidade="Baixa" />
              <span className="numeros text-xs font-bold text-texto-suave">43,3% da base</span>
            </div>
            <h3 className="mt-3 text-sm font-bold text-titulo">Baixa Relevância</h3>
            <p className="mt-1.5 text-xs text-texto-suave leading-relaxed">
              Achados informativos e pequenos desvios com documentação regular. Aparece para registro,
              mas sem alarde.
            </p>
          </div>

          <div className="rounded-cartao border border-borda-suave bg-fundo p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <TagDeGravidade gravidade="Media" />
              <span className="numeros text-xs font-bold text-texto-suave">53,4% da base</span>
            </div>
            <h3 className="mt-3 text-sm font-bold text-titulo">Média Relevância</h3>
            <p className="mt-1.5 text-xs text-texto-suave leading-relaxed">
              Atipicidades operacionais ou estatísticas que merecem conferência técnica dos dados
              apresentados.
            </p>
          </div>

          <div className="rounded-cartao border border-borda-suave bg-fundo p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <TagDeGravidade gravidade="Alta" />
              <span className="numeros text-xs font-bold text-alta-texto font-semibold">
                3,3% da base
              </span>
            </div>
            <h3 className="mt-3 text-sm font-bold text-titulo">Alta Relevância</h3>
            <p className="mt-1.5 text-xs text-texto-suave leading-relaxed">
              O vermelho fica estritamente com os 3,3% de achados mais graves contra limites legais
              ou regras de cota.
            </p>
          </div>

          <div className="rounded-cartao border border-borda-suave bg-fundo p-4 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="inline-flex items-center gap-1.5 rounded-sm border border-borda-suave bg-abstencao-fundo px-2 py-1 text-xs font-semibold text-abstencao-texto">
                <span
                  aria-hidden="true"
                  className="flex size-3.5 items-center justify-center rounded-full border border-dashed border-current text-[10px] leading-none"
                >
                  –
                </span>
                <span>Não avaliado</span>
              </span>
              <span className="text-xs font-bold text-texto-suave">Sem base</span>
            </div>
            <h3 className="mt-3 text-sm font-bold text-titulo">Abstenção</h3>
            <p className="mt-1.5 text-xs text-texto-suave leading-relaxed">
              Ausência de dados públicos na fonte. O motor recusa-se a presumir ou opinar sem base
              comprovada.
            </p>
          </div>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  RESSALVA DE COMPLIANCE E RIGOR (ADR-0004)                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 pb-16">
        <div className="relative overflow-hidden rounded-cartao border-2 border-borda-suave bg-fundo p-6 sm:p-8 shadow-cartao">
          <div className="flex items-start gap-4">
            <span
              aria-hidden="true"
              className="hidden sm:flex size-10 shrink-0 items-center justify-center rounded-campo bg-realce text-titulo"
            >
              <svg
                className="size-6"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                <path d="m9 12 2 2 4-4" />
              </svg>
            </span>

            <div className="flex-1">
              <h2 className="font-display text-xl sm:text-2xl font-bold tracking-tight text-titulo">
                O que um indicador não é
              </h2>

              <p className="mt-3 text-sm sm:text-base leading-relaxed text-texto">
                Um indicador aponta que <strong>algum dado destoa do padrão estatístico esperado</strong>{" "}
                — um ramo de atividade econômica que não combina com a despesa, um valor repetido ao
                centavo por vários meses ou emissão fora da base eleitoral. Nada disso constitui, por
                si só, irregularidade: gastar a cota é prerrogativa legal do mandato, e a explicação
                pode ser perfeitamente legítima.
              </p>

              <p className="mt-3 text-sm sm:text-base leading-relaxed text-texto">
                Por essa razão, cada achado publicado acompanha{" "}
                <strong>a regra precisa aplicada e o comprovante oficial direto da fonte</strong>{" "}
                quando ela o disponibiliza. O Explanando organiza a transparência pública; o juízo é
                do cidadão e das instituições.
              </p>

              <div className="mt-5 flex flex-wrap gap-4 pt-2 border-t border-borda-suave text-xs text-texto-suave">
                <Link
                  href="/estado"
                  className="font-semibold text-titulo underline underline-offset-4 hover:text-titulo-hover"
                >
                  Conheça o Grafo do Estado →
                </Link>
                <Link
                  href="/privacidade"
                  className="font-semibold text-titulo underline underline-offset-4 hover:text-titulo-hover"
                >
                  Política de Privacidade & LGPD →
                </Link>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}

/* ======================================================================== */
/*  SUBCOMPONENTES DA PÁGINA INICIAL                                        */
/* ======================================================================== */

function CartaoMetrica({
  rotulo,
  valor,
  detalhe,
  icone,
}: {
  rotulo: string;
  valor: string;
  detalhe: string;
  icone: "parlamentares" | "despesas" | "volume";
}) {
  return (
    <div className="flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-5 shadow-xs transition-shadow hover:shadow-cartao">
      <div className="flex items-center justify-between">
        <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
          {rotulo}
        </dt>
        <span
          aria-hidden="true"
          className="flex size-7 items-center justify-center rounded-selo bg-realce text-titulo"
        >
          {icone === "parlamentares" && (
            <svg className="size-4" fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z" />
            </svg>
          )}
          {icone === "despesas" && (
            <svg className="size-4" fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
            </svg>
          )}
          {icone === "volume" && (
            <svg className="size-4" fill="none" viewBox="0 0 24 24" strokeWidth="2" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 6v12m-3-2.818.879.659c1.171.879 3.07.879 4.242 0 1.172-.879 1.172-2.303 0-3.182C13.536 12.219 12.768 12 12 12c-.725 0-1.45-.22-2.003-.659-1.106-.879-1.106-2.303 0-3.182s2.9-.879 4.006 0l.415.33M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
            </svg>
          )}
        </span>
      </div>

      <dd className="numeros mt-3 font-display text-3xl font-bold tracking-tight text-titulo">
        {valor}
      </dd>
      <p className="mt-1 text-xs text-texto-suave">{detalhe}</p>
    </div>
  );
}

function CartaoDaCasa({ casa, itens }: { casa: Casa; itens: ItemDoRanking[] | null }) {
  const total = itens?.reduce((soma, item) => soma + item.volumeTotalGasto, 0) ?? 0;
  const primeiros = itens?.slice(0, 3) ?? [];
  const temItens = itens !== null && itens.length > 0;
  const ehCamara = casa.chave === "camara";

  return (
    <section
      aria-labelledby={`casa-${casa.chave}`}
      className="flex min-w-0 flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-6 sm:p-7 shadow-cartao transition-all hover:border-borda-estrutural"
    >
      <div>
        {/* Topo do cartão da casa */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span
            className={`rounded-selo px-2.5 py-1 text-xs font-bold ${
              ehCamara ? "bg-surface text-texto border border-borda-suave" : "bg-realce text-titulo"
            }`}
          >
            {ehCamara ? "513 cadeiras · Cota CEAP" : "81 cadeiras · Cota CEAPS"}
          </span>

          {temItens && (
            <span className="numeros text-xs font-semibold text-texto-suave">
              {formatarNumero(itens.length)} parlamentares
            </span>
          )}
        </div>

        <h3
          id={`casa-${casa.chave}`}
          className="mt-3 font-display text-2xl font-bold tracking-tight text-titulo"
        >
          {casa.nomeOficial}
        </h3>

        {/* Estado indisponível */}
        {itens === null ? (
          <p className="mt-4 rounded-campo bg-abstencao-fundo p-3 text-sm text-abstencao-texto">
            Os números desta casa não puderam ser carregados agora. A falha é temporária de
            comunicação com a API oficial.
          </p>
        ) : itens.length === 0 ? (
          <div className="mt-4 rounded-campo border border-dashed border-borda-suave bg-surface p-4 text-sm text-texto-suave">
            <p className="font-semibold text-titulo">Base em sincronização</p>
            <p className="mt-1 text-xs">
              Os dados desta casa estão em processo de consolidação no ambiente. Os rankings serão
              disponibilizados assim que o lote for processado.
            </p>
          </div>
        ) : (
          <>
            <p className="mt-2 text-sm text-texto-suave">
              Volume total auditado:{" "}
              <strong className="numeros text-base font-bold text-titulo">
                {formatarReais(total)}
              </strong>
            </p>

            {/* Prévia dos 3 maiores volumes */}
            <div className="mt-5">
              <p className="text-xs font-bold uppercase tracking-wider text-texto-suave">
                Maiores volumes da casa
              </p>
              <ol className="mt-2.5 flex flex-col gap-2">
                {primeiros.map((item, indice) => (
                  <PreviaDoTopo key={item.id} item={item} posicao={indice + 1} />
                ))}
              </ol>
            </div>
          </>
        )}
      </div>

      {/* Caminhos diretos de ranking */}
      <div className="mt-6 border-t border-borda-suave pt-5">
        <p className="text-xs font-bold uppercase tracking-wider text-texto-suave">
          Explorar rankings desta casa
        </p>
        <ul className="mt-3 flex flex-col gap-2.5">
          {ORDENACOES.map((ordenacao) => {
            const ehVolume = ordenacao.chave === "volume";
            const ehIndice = ordenacao.chave === "indice";

            return (
              <li key={ordenacao.chave}>
                <Link
                  href={caminhoDoRanking(casa, ordenacao)}
                  className="group flex items-center justify-between gap-3 rounded-campo border border-borda-suave bg-surface p-3 transition-colors hover:border-titulo hover:bg-fundo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
                >
                  <div className="flex items-center gap-3">
                    <span
                      aria-hidden="true"
                      className="flex size-7 shrink-0 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo group-hover:bg-titulo group-hover:text-white transition-colors"
                    >
                      {ehVolume ? "R$" : ehIndice ? "%" : "!"}
                    </span>
                    <div>
                      <span className="block text-sm font-semibold text-titulo group-hover:underline underline-offset-2">
                        {ordenacao.rotulo}
                      </span>
                      <span className="block text-xs text-texto-suave">{ordenacao.resumo}</span>
                    </div>
                  </div>

                  <span
                    aria-hidden="true"
                    className="text-sm font-bold text-titulo transition-transform group-hover:translate-x-1"
                  >
                    →
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
  );
}

function PreviaDoTopo({ item, posicao }: { item: ItemDoRanking; posicao: number }) {
  const retrato = retratoSeguro(item.fotoUrl);

  return (
    <li>
      <Link
        href={`/parlamentares/${item.id}`}
        className="group flex min-w-0 items-center gap-3 rounded-campo border border-transparent p-2 transition-colors hover:border-borda-suave hover:bg-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      >
        {/* Medalha / posição */}
        <span
          className={`numeros flex size-6 shrink-0 items-center justify-center rounded-selo text-xs font-bold ${
            posicao === 1
              ? "bg-amber-100 text-amber-900 border border-amber-300 dark:bg-amber-950 dark:text-amber-300"
              : posicao === 2
                ? "bg-slate-100 text-slate-800 border border-slate-300 dark:bg-slate-800 dark:text-slate-200"
                : "bg-orange-50 text-orange-800 border border-orange-200 dark:bg-orange-950 dark:text-orange-300"
          }`}
        >
          {posicao}º
        </span>

        {retrato ? (
          <Image
            src={retrato}
            alt=""
            width={36}
            height={36}
            unoptimized
            className="size-9 shrink-0 rounded-selo bg-surface object-cover border border-borda-suave"
          />
        ) : (
          <span
            aria-hidden="true"
            className="numeros flex size-9 shrink-0 items-center justify-center rounded-selo bg-realce text-xs font-bold text-titulo"
          >
            {iniciais(item.nomeParlamentar)}
          </span>
        )}

        <div className="min-w-0 flex-1">
          <span className="block truncate text-sm font-semibold text-titulo group-hover:underline underline-offset-2">
            {item.nomeParlamentar}
          </span>
          <div className="flex items-center gap-1.5 text-xs text-texto-suave">
            <span className="font-semibold text-texto">{item.siglaPartido}</span>
            <span>·</span>
            <span>{item.uf}</span>
            <span>·</span>
            <span className="numeros font-bold text-texto">
              {formatarReais(item.volumeTotalGasto)}
            </span>
          </div>
        </div>

        <span aria-hidden="true" className="text-xs text-texto-suave transition-transform group-hover:translate-x-1">
          →
        </span>
      </Link>
    </li>
  );
}

function PassoPipeline({
  numero,
  tag,
  titulo,
  texto,
  icone,
}: {
  numero: string;
  tag: string;
  titulo: string;
  texto: string;
  icone: "coleta" | "cruzamento" | "abstencao";
}) {
  return (
    <li className="relative flex flex-col justify-between rounded-cartao border border-borda-suave bg-fundo p-6 shadow-xs">
      <div>
        <div className="flex items-center justify-between">
          <span
            aria-hidden="true"
            className="numeros flex size-7 items-center justify-center rounded-selo bg-titulo text-xs font-bold text-white shadow-xs"
          >
            {numero}
          </span>
          <span className="rounded-selo bg-surface border border-borda-suave px-2 py-0.5 text-[10px] font-bold text-texto-suave">
            {tag}
          </span>
        </div>

        <h3 className="mt-4 font-display text-lg font-bold text-titulo">{titulo}</h3>
        <p className="mt-2 text-sm leading-relaxed text-texto-suave">{texto}</p>
      </div>

      <div className="mt-5 flex items-center gap-2 pt-3 border-t border-borda-suave text-xs font-medium text-texto-suave">
        <span aria-hidden="true" className="text-acento">
          {icone === "coleta" && "📥"}
          {icone === "cruzamento" && "⚙️"}
          {icone === "abstencao" && "🛡️"}
        </span>
        <span>
          {icone === "coleta" && "Sem digitação manual"}
          {icone === "cruzamento" && "Cruzamento determinístico"}
          {icone === "abstencao" && "Sem acusação infundada"}
        </span>
      </div>
    </li>
  );
}
