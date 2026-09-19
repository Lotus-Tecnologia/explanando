import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import {
  coletarCodigosDoEstado,
  obterUnidadeDoEstado,
  obterUnidadesDoEstado,
} from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import { corDoPoder } from "@/lib/grafo";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

interface Parametros {
  params: Promise<{ codigo: string }>;
}

/**
 * As ~336 páginas de órgão viram arquivo no build; as outras 96 mil continuam sob demanda.
 *
 * O corte é o mesmo do mapa, e pela mesma razão: ninguém compartilha o link de uma
 * coordenação de terceiro nível, mas o do Ministério da Educação sim. `dynamicParams` segue
 * no padrão, então nada fica inacessível — pré-renderizar é otimização, não muro.
 */
export async function generateStaticParams() {
  const codigos = await coletarCodigosDoEstado();

  return codigos.map((codigo) => ({ codigo: String(codigo) }));
}

export async function generateMetadata({ params }: Parametros): Promise<Metadata> {
  const { codigo } = await params;
  const unidade = await obterUnidadeDoEstado(Number(codigo));

  if (!unidade) {
    return { title: "Unidade não encontrada" };
  }

  return {
    title: `${unidade.nome} — estrutura do Estado`,
    description:
      `${unidade.tipo}${unidade.poder ? ` do poder ${unidade.poder}` : ""}. ` +
      `${formatarNumero(unidade.unidadesSubordinadas)} unidades subordinadas. ` +
      `${unidade.baseLegal ?? "Base legal não informada pela fonte"}.`,
    alternates: { canonical: `/estado/${unidade.codigo}` },
  };
}

/**
 * Uma unidade da estrutura, e o que pende dela.
 *
 * **Um nível por vez.** Alguns órgãos têm milhares de unidades abaixo; devolver a árvore
 * inteira traria de volta exatamente o problema que o mapa inicial resolve — uma lista tão
 * longa que ninguém lê. Descer é um clique.
 */
export default async function Pagina({ params }: Parametros) {
  const { codigo } = await params;
  const numero = Number(codigo);

  if (!Number.isInteger(numero) || numero <= 0) {
    notFound();
  }

  const [unidade, filhas] = await Promise.all([
    obterUnidadeDoEstado(numero),
    obterUnidadesDoEstado(numero),
  ]);

  if (!unidade) {
    notFound();
  }

  // Sem isto não há como subir: de dentro de um ministério, o único caminho de volta seria
  // o botão do navegador. Num grafo, andar para cima é metade da navegação.
  const pai =
    unidade.codigoPai === null ? null : await obterUnidadeDoEstado(unidade.codigoPai);

  const fichas = [
    { termo: "Tipo", valor: unidade.tipo },
    { termo: "Poder", valor: unidade.poder },
    { termo: "Forma jurídica", valor: unidade.natureza },
    { termo: "Base legal", valor: unidade.baseLegal },
  ];

  return (
    <main className="mx-auto max-w-5xl px-4 py-10">
      <nav aria-label="Trilha" className="text-sm">
        <Link
          href="/estado"
          className="text-texto-suave hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          ← Mapa do Estado
        </Link>
      </nav>

      {pai ? (
        <p className="mt-4 text-sm text-texto-suave">
          Responde a{" "}
          <Link
            href={`/estado/${pai.codigo}`}
            className="font-semibold text-titulo underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
          >
            {pai.nome}
          </Link>
        </p>
      ) : null}

      <header className="mt-6 flex items-start gap-4">
        <span
          aria-hidden="true"
          className="mt-2 size-4 shrink-0 rounded-[4px]"
          style={{ background: corDoPoder(unidade.poder) }}
        />

        <div className="min-w-0">
          <h1 className="font-display text-3xl font-bold tracking-tight text-balance text-titulo">
            {unidade.nome}
          </h1>

          {unidade.sigla ? (
            <p className="mt-1 text-sm font-semibold text-texto-suave">{unidade.sigla}</p>
          ) : null}
        </div>
      </header>

      {!unidade.ativa ? (
        <p className="mt-6 rounded-cartao border border-borda-estrutural bg-surface p-4 text-sm text-texto">
          <strong className="font-semibold text-titulo">
            Esta unidade deixou de constar na estrutura publicada.
          </strong>{" "}
          Não dizemos que foi extinta: a fonte não distingue extinção, incorporação e
          &ldquo;o cadastro parou de publicar&rdquo;. O que está acima é a última fotografia
          que tivemos dela.
        </p>
      ) : null}

      <dl className="mt-8 grid gap-px overflow-hidden rounded-cartao border border-borda-suave sm:grid-cols-2">
        {fichas.map((ficha) => (
          <div key={ficha.termo} className="bg-surface px-5 py-4">
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              {ficha.termo}
            </dt>
            <dd className="mt-1 text-sm text-texto">
              {/*
                Nulo vira "não informado pela fonte", e não um travessão nem um espaço em
                branco: a diferença entre "a fonte não diz" e "não tem" é a mesma que o
                resto do site protege, e um traço solto apaga essa distinção.
              */}
              {ficha.valor ?? (
                <span className="text-texto-suave">Não informado pela fonte</span>
              )}
            </dd>
          </div>
        ))}

        <div className="bg-surface px-5 py-4">
          <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
            Unidades subordinadas
          </dt>
          <dd className="numeros mt-1 font-display text-2xl font-bold text-titulo">
            {formatarNumero(unidade.unidadesSubordinadas)}
          </dd>
        </div>

        <div className="bg-surface px-5 py-4">
          <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
            Servidores no ramo
          </dt>
          <dd className="numeros mt-1 font-display text-2xl font-bold text-titulo">
            {/*
              "Sem dado", e nunca zero. A ponte entre o cadastro de estrutura e o de
              pessoal nao alcanca tudo, e o cadastro de pessoal e' do Executivo civil —
              escrever 0 aqui afirmaria que um tribunal nao tem servidor nenhum.
            */}
            {/*
              `== null` frouxo, e nao `=== null`: o campo pode chegar UNDEFINED de uma
              resposta mais antiga que a API — o cache de fetch do Next guarda a resposta
              por minutos, e uma implantacao no meio disso serve o contrato velho. Foi
              exatamente assim que esta pagina quebrou com "toLocaleString of undefined".
            */}
            {unidade.servidoresNoRamo == null ? (
              <span className="text-base font-normal text-texto-suave">Sem dado</span>
            ) : (
              formatarNumero(unidade.servidoresNoRamo)
            )}
          </dd>

          {unidade.servidores != null ? (
            <dd className="mt-1 text-xs text-texto-suave">
              {formatarNumero(unidade.servidores)} lotados nesta unidade
              {unidade.emFuncaoDeConfianca != null && unidade.emFuncaoDeConfianca > 0
                ? `, ${formatarNumero(unidade.emFuncaoDeConfianca)} em função de confiança`
                : ""}
            </dd>
          ) : null}
        </div>

        <div className="bg-surface px-5 py-4">
          <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
            Código no SIORG
          </dt>
          <dd className="numeros mt-1 text-sm text-texto">{unidade.codigo}</dd>
        </div>
      </dl>

      <section className="mt-10">
        <h2 className="font-display text-xl font-bold tracking-tight text-titulo">
          Responde diretamente a esta unidade
        </h2>

        {filhas && filhas.length > 0 ? (
          <>
            <p className="mt-2 text-sm text-texto-suave">
              {formatarNumero(filhas.length)}{" "}
              {filhas.length === 1 ? "unidade direta" : "unidades diretas"}, do maior ramo
              para o menor.
            </p>

            <ul className="mt-5 grid gap-px overflow-hidden rounded-cartao border border-borda-suave sm:grid-cols-2">
              {[...filhas]
                .sort((a, b) => b.unidadesSubordinadas - a.unidadesSubordinadas)
                .map((filha) => (
                  <li key={filha.codigo} className="bg-surface">
                    <Link
                      href={`/estado/${filha.codigo}`}
                      className="flex items-baseline gap-3 px-4 py-3 hover:bg-realce focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo"
                    >
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium text-titulo">
                          {filha.nome}
                        </span>
                        <span className="block truncate text-xs text-texto-suave">
                          {filha.tipo}
                          {filha.baseLegal ? ` · ${filha.baseLegal.toLowerCase()}` : ""}
                        </span>
                      </span>

                      {filha.unidadesSubordinadas > 0 ? (
                        <span className="numeros shrink-0 text-sm text-texto">
                          {formatarNumero(filha.unidadesSubordinadas)}
                        </span>
                      ) : null}
                    </Link>
                  </li>
                ))}
            </ul>
          </>
        ) : (
          <p className="mt-4 rounded-cartao border border-borda-suave bg-surface p-5 text-sm text-texto-suave">
            Nenhuma unidade responde diretamente a esta. É uma ponta da estrutura.
          </p>
        )}
      </section>
    </main>
  );
}
