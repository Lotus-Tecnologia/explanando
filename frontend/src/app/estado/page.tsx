import type { Metadata } from "next";
import Link from "next/link";
import { LegendaDePoderes } from "@/componentes/GrafoDoEstado";
import { VistasDoEstado } from "@/componentes/VistasDoEstado";
import { DistribuicaoDoEstado, LinhaDoTempoDoEstado } from "@/componentes/PainelDoEstado";
import { SerieDePessoal } from "@/componentes/SerieDePessoal";
import {
  listarMudancasDoEstado,
  obterMapaDePoder,
  obterMapaDoEstado,
  obterSerieDePessoal,
} from "@/lib/api";
import { formatarCompetencia, formatarMomento, formatarNumero } from "@/lib/formato";
import { corDaEscala, corDoPoder } from "@/lib/grafo";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "O mapa do Estado brasileiro",
  description:
    "Que órgãos existem na administração pública federal, de quem cada um depende e sob " +
    "que norma foi criado. Dados do SIORG, a estrutura organizacional oficial.",
  alternates: { canonical: "/estado" },
};

/**
 * O grafo do Estado — a adaptação ao Brasil do CivLab.
 *
 * **É outra pergunta, não outro ranking.** O resto do produto responde "quanto se gastou";
 * esta página responde "que órgãos existem, de quem cada um depende, sob que norma — e o
 * que andou mudando". Não há parlamentar, não há cota e não há indicador de risco: nada
 * aqui descreve conduta de ninguém, e por isso nada aqui usa o vocabulário de gravidade.
 *
 * **A metade das pessoas está faltando, e a página diz isso.** A referência mostra cadeiras
 * vagas e ocupantes interinos; o SIORG não publica cargo nem titular — nenhum campo, em
 * nenhum endpoint. Esse dado vive no Diário Oficial, cujo acesso estruturado exige
 * cadastro. Omitir a ausência faria o mapa parecer completo quando é meio mapa.
 */
export default async function Pagina() {
  const [mapa, mudancas, serie, poderes] = await Promise.all([
    obterMapaDoEstado(),
    listarMudancasDoEstado(30),
    obterSerieDePessoal(),
    obterMapaDePoder(),
  ]);

  if (!mapa) {
    return (
      <main className="mx-auto max-w-5xl px-4 py-16">
        <h1 className="font-display text-3xl font-bold text-titulo">O mapa do Estado</h1>
        <p className="mt-4 text-texto">
          A estrutura do Estado não pôde ser carregada agora. Tente de novo em instantes.
        </p>
      </main>
    );
  }

  const poderesComDado = (poderes?.poderes ?? [])
    .filter((p) => p.totalDeCadeiras > 0)
    .map((p) => p.poder);
  const poderesSemDado = (poderes?.poderes ?? [])
    .filter((p) => p.totalDeCadeiras === 0)
    .map((p) => p.poder);

  const orgaos = mapa.nos.filter((no) => no.tipo === "Órgão" || no.tipo === "Entidade");

  return (
    <main>
      {/* ---------------------------------------------------------------- */}
      {/*  Abertura                                                         */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-b border-borda-suave bg-surface">
        <div className="mx-auto max-w-5xl px-4 py-12 sm:py-16">
          <p className="text-sm font-semibold text-texto-suave">
            Estrutura organizacional, em dados abertos
          </p>

          <h1 className="mt-3 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-titulo sm:text-5xl">
            O mapa do Estado brasileiro
          </h1>

          <p className="mt-4 max-w-2xl text-base text-texto">
            <strong className="font-semibold text-titulo">
              {formatarNumero(mapa.totalDeUnidades)} unidades
            </strong>{" "}
            compõem a administração pública federal — de ministérios a conselhos de três
            pessoas. O desenho mostra os órgãos de cima, e cada um abre as unidades que tem abaixo:
            de quem cada uma depende, quanta gente trabalha nelas, e se foram criadas por lei ou
            por uma assinatura interna.
          </p>

          {mapa.totalDeServidores != null ? (
            <p className="mt-3 max-w-2xl text-base text-texto">
              <strong className="font-semibold text-titulo">
                {formatarNumero(mapa.totalDeServidores)} servidores
              </strong>{" "}
              no cadastro de pessoal de {formatarCompetencia(mapa.competenciaDoPessoal)}.
            </p>
          ) : null}

          {mapa.ultimaColeta ? (
            <p className="mt-3 text-sm text-texto-suave">
              Consultado em{" "}
              <time dateTime={mapa.ultimaColeta}>
                {formatarMomento(mapa.ultimaColeta)}
              </time>{" "}
              no SIORG, o cadastro oficial da estrutura organizacional.
            </p>
          ) : null}
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  O desenho                                                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        {/*
          As duas vistas do mesmo contexto, uma substituindo a outra. O alternador desceu
          para o pe' do desenho — e' controle do objeto que se olha, e nao da navegacao.
        */}
        <VistasDoEstado nos={mapa.nos} poderes={poderes} temHistorico={mapa.temHistorico} />

        <div className="mt-6">
          <LegendaDePoderes poderes={mapa.porPoder} />
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Os cortes                                                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-y border-borda-suave bg-surface/50">
        <div className="mx-auto grid max-w-5xl gap-5 px-4 py-12 sm:grid-cols-2 lg:grid-cols-3">
          <DistribuicaoDoEstado
            titulo="Por poder"
            explicacao="Onde está o volume da máquina pública."
            contagens={mapa.porPoder}
            cores={corDoPoder}
          />

          <DistribuicaoDoEstado
            titulo="Por tipo de unidade"
            explicacao="Órgão, entidade, unidade administrativa e colegiado não pesam igual."
            contagens={mapa.porTipo}
            cores={(_, indice) => corDaEscala(indice)}
          />

          <DistribuicaoDoEstado
            titulo="Por base legal"
            explicacao="A diferença entre precisar do Congresso para sumir e sumir com uma assinatura."
            contagens={mapa.porBaseLegal}
            cores={(_, indice) => corDaEscala(indice)}
          />
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Quem trabalha nele                                               */}
      {/* ---------------------------------------------------------------- */}
      {mapa.totalDeServidores != null ? (
        <section className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
            Quem trabalha no Estado
          </h2>

          <p className="mt-2 max-w-2xl text-sm text-texto-suave">
            O cadastro de pessoal e a estrutura são <strong>duas fontes diferentes</strong>,
            publicadas em cadências diferentes, e nenhuma das duas traz o código da outra. A
            ligação entre elas é feita pelo nome do órgão, com regra explícita — e o que não
            casa com exatidão fica sem par, em vez de ser adivinhado.
          </p>

          <dl className="mt-6 grid grid-cols-1 gap-px overflow-hidden rounded-cartao border border-borda-suave sm:grid-cols-3">
            <div className="bg-surface px-5 py-4">
              <dt className="text-xs font-bold text-texto-suave">
                Servidores em {formatarCompetencia(mapa.competenciaDoPessoal)}
              </dt>
              <dd className="numeros mt-1 font-display text-3xl font-bold text-titulo">
                {formatarNumero(mapa.totalDeServidores)}
              </dd>
            </div>

            <div className="bg-surface px-5 py-4">
              <dt className="text-xs font-bold text-texto-suave">
                Ligados a um órgão do mapa
              </dt>
              <dd className="numeros mt-1 font-display text-3xl font-bold text-titulo">
                {mapa.coberturaDoPessoal == null
                  ? "—"
                  : `${(mapa.coberturaDoPessoal * 100).toFixed(1).replace(".", ",")}%`}
              </dd>
              <dd className="mt-1 text-xs text-texto-suave">
                O resto está em rubricas que o cadastro de estrutura não tem — os governos dos
                ex-territórios, por exemplo.
              </dd>
            </div>

            <div className="bg-surface px-5 py-4">
              <dt className="text-xs font-bold text-texto-suave">
                De qual poder
              </dt>
              <dd className="mt-1 text-sm text-texto">
                Só do <strong className="font-semibold text-titulo">Executivo civil</strong>. O
                Judiciário e o Legislativo mantêm folhas próprias e aparecem aqui apenas em
                parte — por isso muitos tribunais mostram &ldquo;sem dado&rdquo; em vez de zero.
              </dd>
            </div>
          </dl>

          <div className="mt-8 rounded-cartao border border-borda-suave bg-surface p-5">
            <SerieDePessoal
              serie={serie}
              titulo="O Executivo federal, mês a mês"
              explicacao="Cada ponto é uma folha mensal publicada. Passe o ponteiro para ver o mês."
            />
          </div>
        </section>
      ) : null}

      {/* ---------------------------------------------------------------- */}
      {/*  O que mudou                                                      */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
          O que mudou na estrutura
        </h2>

        <p className="mt-2 max-w-2xl text-sm text-texto-suave">
          A fonte publica um retrato do agora e não guarda histórico — os campos existem no
          esquema e chegam vazios. Cada linha abaixo foi{" "}
          <strong className="font-semibold">deduzida</strong> comparando duas coletas nossas.
        </p>

        <div className="mt-6">
          <LinhaDoTempoDoEstado mudancas={mudancas} temHistorico={mapa.temHistorico} />
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  A lista, que é o desenho em texto                                */}
      {/* ---------------------------------------------------------------- */}
      <section id="orgaos-e-entidades" className="scroll-mt-24 border-t border-borda-suave">
        <div className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
            Órgãos e entidades
          </h2>

          <p className="mt-2 max-w-2xl text-sm text-texto-suave">
            Os {formatarNumero(orgaos.length)} do primeiro nível, do maior ramo para o menor. É
            a mesma informação do desenho, em texto — quem não distingue as cores lê aqui.
          </p>

          {/*
            Um grupo por poder, recolhido: aberta, a lista de primeiro nível passava de 24 mil
            pixels no celular. O `<details>` funciona sem JavaScript e no teclado.
          */}
          <div className="mt-6 grid gap-2">
            {gruposDePoder(orgaos).map(({ poder, rotulo, itens }) => (
              <details key={rotulo} className="rounded-cartao border border-borda-suave bg-surface">
                <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-4 py-3 [&::-webkit-details-marker]:hidden">
                  <span
                    aria-hidden="true"
                    className="size-3 shrink-0 rounded-full"
                    style={{ background: corDoPoder(poder) }}
                  />
                  <span className="flex-1 font-semibold text-texto">{rotulo}</span>
                  <span className="numeros text-sm text-texto-suave">
                    {formatarNumero(itens.length)} {itens.length === 1 ? "órgão" : "órgãos"}
                  </span>
                  <span aria-hidden="true" className="text-texto-suave">▾</span>
                </summary>

                <ul className="grid grid-cols-1 border-t border-borda-suave sm:grid-cols-2">
                  {itens.map((orgao) => (
                    <li key={orgao.codigo}>
                      <Link
                        href={`/estado/${orgao.codigo}`}
                        className="flex min-h-11 items-baseline gap-3 px-4 py-3 hover:bg-realce"
                      >
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-semibold text-texto">
                            {orgao.sigla ?? orgao.nome}
                          </span>
                          <span className="block truncate text-sm text-texto-suave">
                            {orgao.sigla ? orgao.nome : orgao.tipo}
                          </span>
                        </span>

                        <span className="numeros shrink-0 text-right text-sm text-texto">
                          {formatarNumero(orgao.unidadesSubordinadas)}
                          <span className="block text-texto-suave">
                            {orgao.servidoresNoRamo == null
                              ? "sem dado"
                              : `${formatarNumero(orgao.servidoresNoRamo)} pessoas`}
                          </span>
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </details>
            ))}
          </div>
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  O que ainda não está aqui                                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="mx-auto max-w-5xl px-4 py-12">
        <div className="rounded-cartao border border-borda-suave bg-surface p-6">
          <h2 className="font-display text-lg font-bold text-titulo">
            O que este mapa ainda não mostra
          </h2>

          {/*
            O que já existe vem do próprio mapa de poder, e não de texto fixo. Medido em 25/09:
            este bloco dizia que o mapa não mostra quem ocupa os cargos, enquanto a vista "Quem
            comanda" listava 40 ocupantes do Executivo e 100 do Legislativo, com fonte e data.
          */}
          {poderesComDado.length > 0 ? (
            <p className="mt-2 text-sm text-texto">
              <strong className="font-semibold">Parte das pessoas já está.</strong> A vista
              “Quem comanda” traz os ocupantes dos cargos de comando, com a fonte e a data de cada
              leitura, em: {juntar(poderesComDado)}.
              {poderesSemDado.length > 0
                ? ` Ainda sem fonte que diga quem ocupa esses cargos: ${juntar(poderesSemDado)}.`
                : null}
            </p>
          ) : null}

          <p className="mt-2 text-sm text-texto">
            <strong className="font-semibold">O resto das pessoas.</strong> Quem ocupa cada cargo
            abaixo do comando, quantas cadeiras estão vagas, há quanto tempo. O SIORG não publica
            isso — não há campo de titular em endpoint nenhum —, e são três ausências diferentes:
          </p>

          <ul className="mt-3 space-y-2 text-sm text-texto">
            <li>
              <strong className="font-semibold text-titulo">Quem ocupa cada cargo hoje</strong>{" "}
              é dado público: o Portal da Transparência publica 778.337 vínculos por mês. O que
              falta é a ponte entre os códigos de órgão das duas fontes, que não são os mesmos.
            </li>
            <li>
              <strong className="font-semibold text-titulo">
                As nomeações ao longo do tempo
              </strong>{" "}
              estão no Diário Oficial, e o acesso estruturado a ele depende de um cadastro que
              ainda não fizemos.
            </li>
            <li>
              <strong className="font-semibold text-titulo">As cadeiras vagas</strong> não estão
              em fonte nenhuma: ninguém publica quantos cargos cada órgão está autorizado a ter,
              que é o número sem o qual não existe conta de vaga.
            </li>
          </ul>

          <p className="mt-3 text-sm text-texto-suave">
            Preferimos dizer isto a deixar o mapa parecer completo. É a mesma regra que vale no
            resto do site: ausência de dado nunca é apresentada como ausência de fato.
          </p>
        </div>
      </section>
    </main>
  );
}

const ROTULO_DO_PODER: Record<string, string> = {
  executivo: "Executivo",
  legislativo: "Legislativo",
  judiciario: "Judiciário",
  essenciais: "Funções essenciais à Justiça",
};

/**
 * Os órgãos agrupados por poder, cada grupo do maior ramo para o menor, e os grupos pelo
 * número de órgãos. Sem poder classificado vai por último, com nome próprio: a fonte não o
 * classificou, e pô-lo noutro grupo inventaria a classificação.
 */
function gruposDePoder<T extends { poder: string | null; unidadesSubordinadas: number }>(
  orgaos: readonly T[],
) {
  const mapa = new Map<string, { poder: string | null; rotulo: string; itens: T[] }>();
  for (const orgao of orgaos) {
    const rotulo = orgao.poder ? (ROTULO_DO_PODER[orgao.poder] ?? orgao.poder) : "Sem poder classificado";
    const grupo = mapa.get(rotulo) ?? { poder: orgao.poder, rotulo, itens: [] };
    grupo.itens.push(orgao);
    mapa.set(rotulo, grupo);
  }
  for (const grupo of mapa.values()) grupo.itens.sort((a, b) => b.unidadesSubordinadas - a.unidadesSubordinadas);
  return [...mapa.values()].sort((a, b) =>
    a.poder === null ? 1 : b.poder === null ? -1 : b.itens.length - a.itens.length,
  );
}

/** "Executivo e Legislativo" — a enumeração do português, com o "e" antes do último. */
function juntar(itens: string[]): string {
  return new Intl.ListFormat("pt-BR", { style: "long", type: "conjunction" }).format(itens);
}
