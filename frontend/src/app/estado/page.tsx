import type { Metadata } from "next";
import Link from "next/link";
import { GrafoDoEstado, LegendaDePoderes } from "@/componentes/GrafoDoEstado";
import { DistribuicaoDoEstado, LinhaDoTempoDoEstado } from "@/componentes/PainelDoEstado";
import { listarMudancasDoEstado, obterMapaDoEstado } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import { corDaEscala, corDoPoder } from "@/lib/grafo";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "O mapa do Estado brasileiro",
  description:
    "Que órgãos existem na administração pública federal, de quem cada um depende e sob "
    + "que norma foi criado. Dados do SIORG, a estrutura organizacional oficial.",
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
  const [mapa, mudancas] = await Promise.all([
    obterMapaDoEstado(),
    listarMudancasDoEstado(30),
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

  const orgaos = mapa.nos.filter((no) => no.tipo === "Órgão" || no.tipo === "Entidade");

  return (
    <main>
      {/* ---------------------------------------------------------------- */}
      {/*  Abertura                                                         */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-b border-borda-suave bg-surface">
        <div className="mx-auto max-w-5xl px-4 py-12 sm:py-16">
          <p className="text-xs font-bold tracking-widest text-acento uppercase">
            Estrutura organizacional · dados abertos
          </p>

          <h1 className="mt-3 max-w-3xl font-display text-4xl font-bold tracking-tight text-balance text-titulo sm:text-5xl">
            O mapa do Estado brasileiro
          </h1>

          <p className="mt-4 max-w-2xl text-base text-texto">
            <strong className="font-semibold text-titulo">
              {formatarNumero(mapa.totalDeUnidades)} unidades
            </strong>{" "}
            compõem a administração pública federal — de ministérios a conselhos de três
            pessoas. Aqui estão todas: de quem cada uma depende, e se foi criada por lei ou
            por uma assinatura interna.
          </p>

          {mapa.ultimaColeta ? (
            <p className="mt-3 text-sm text-texto-suave">
              Consultado em{" "}
              <time dateTime={mapa.ultimaColeta}>
                {new Date(mapa.ultimaColeta).toLocaleDateString("pt-BR")}
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
        <GrafoDoEstado nos={mapa.nos} />

        <div className="mt-6">
          <LegendaDePoderes poderes={mapa.porPoder} />
        </div>
      </section>

      {/* ---------------------------------------------------------------- */}
      {/*  Os cortes                                                        */}
      {/* ---------------------------------------------------------------- */}
      <section className="border-y border-borda-suave bg-surface/50">
        <div className="mx-auto grid max-w-5xl gap-5 px-4 py-12 md:grid-cols-3">
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
      <section className="border-t border-borda-suave bg-surface/50">
        <div className="mx-auto max-w-5xl px-4 py-12">
          <h2 className="font-display text-2xl font-bold tracking-tight text-titulo">
            Órgãos e entidades
          </h2>

          <p className="mt-2 max-w-2xl text-sm text-texto-suave">
            Os {formatarNumero(orgaos.length)} do primeiro nível, do maior ramo para o menor.
            É a mesma informação do desenho, em texto — quem não distingue as cores lê aqui.
          </p>

          <ul className="mt-6 grid gap-px overflow-hidden rounded-cartao border border-borda-suave sm:grid-cols-2">
            {[...orgaos]
              .sort((a, b) => b.unidadesSubordinadas - a.unidadesSubordinadas)
              .map((orgao) => (
                <li key={orgao.codigo} className="bg-surface">
                  <Link
                    href={`/estado/${orgao.codigo}`}
                    className="flex items-baseline gap-3 px-4 py-3 hover:bg-realce focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo"
                  >
                    <span
                      aria-hidden="true"
                      className="size-2.5 shrink-0 translate-y-1 rounded-[2px]"
                      style={{ background: corDoPoder(orgao.poder) }}
                    />

                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-semibold text-titulo">
                        {orgao.sigla ?? orgao.nome}
                      </span>
                      <span className="block truncate text-xs text-texto-suave">
                        {orgao.sigla ? orgao.nome : orgao.tipo}
                      </span>
                    </span>

                    <span className="numeros shrink-0 text-sm text-texto">
                      {formatarNumero(orgao.unidadesSubordinadas)}
                    </span>
                  </Link>
                </li>
              ))}
          </ul>
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

          <p className="mt-2 text-sm text-texto">
            <strong className="font-semibold">As pessoas.</strong> Cadeiras vagas, ocupantes
            interinos, quanto tempo um cargo ficou sem titular — nada disso está aqui, porque
            o SIORG não publica cargo nem ocupante: não há campo, em nenhum endpoint. Esse
            dado vive no Diário Oficial da União, e o acesso estruturado a ele depende de um
            cadastro que ainda não fizemos.
          </p>

          <p className="mt-3 text-sm text-texto-suave">
            Preferimos dizer isto a deixar o mapa parecer completo. É a mesma regra que vale
            no resto do site: ausência de dado nunca é apresentada como ausência de fato.
          </p>
        </div>
      </section>
    </main>
  );
}
