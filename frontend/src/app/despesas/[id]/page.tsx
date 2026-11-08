import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { obterDespesa } from "@/lib/api";
import { formatarDataDoDocumento, formatarReais } from "@/lib/formato";
import { tituloDaDespesa } from "@/lib/metadados";
import { caixaDeFrase, documentoPublicavel } from "@/lib/frases";
import { Comprovante, ValoresDaDespesa } from "@/componentes/Comprovante";
import { RespostasDoGabinete, SemRespostaDoGabinete } from "@/componentes/RespostasDoGabinete";
import { TagDeGravidade } from "@/componentes/TagDeGravidade";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

type Parametros = { params: Promise<{ id: string }> };

/**
 * Uma despesa não é pré-renderizada: são 711.796 delas.
 *
 * Fica sob demanda com revalidação, o que é o certo para uma página que só é visitada quando
 * alguém compartilha um link específico. O perfil e o ranking — que é onde o pico bate —
 * continuam estáticos.
 */
export async function generateMetadata({ params }: Parametros): Promise<Metadata> {
  const { id } = await params;
  const detalhe = await obterDespesa(id);

  if (!detalhe) {
    return { title: "Despesa não encontrada" };
  }

  const { despesa, parlamentar } = detalhe;

  return {
    title: tituloDaDespesa(
      parlamentar.nomeParlamentar,
      formatarReais(despesa.valorLiquido),
      despesa.tipoDespesa,
    ),
    description:
      `Despesa de ${formatarReais(despesa.valorLiquido)} declarada em ` +
      `${String(despesa.mes).padStart(2, "0")}/${despesa.ano} por ` +
      `${parlamentar.nomeParlamentar} (${parlamentar.siglaPartido}-${parlamentar.uf}).`,
    alternates: { canonical: `/despesas/${id}` },
  };
}

/**
 * O endereço público de uma despesa.
 *
 * É a página que um link compartilhado abre, e o arranjo dela é o do ADR-0004 inteiro em uma
 * tela: o valor, quem gastou, o que o motor apontou **com o código da regra** e o documento
 * oficial ao alcance do clique. A conclusão fica com quem lê — a página não a oferece.
 */
export default async function Despesa({ params }: Parametros) {
  const { id } = await params;
  const detalhe = await obterDespesa(id);

  if (!detalhe) {
    notFound();
  }

  const { despesa, parlamentar } = detalhe;

  return (
    <main className="mx-auto max-w-3xl px-4 py-6 sm:py-10">
      <Link
        href={`/parlamentares/${parlamentar.id}`}
        className="inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
      >
        ← {parlamentar.nomeParlamentar}
      </Link>

      <header className="mt-2">
        <h1 className="font-display text-[1.75rem] leading-tight font-bold tracking-tight text-titulo sm:text-4xl">
          {caixaDeFrase(despesa.tipoDespesa)}
        </h1>
        <p className="mt-2 text-texto-suave">
          {parlamentar.nomeParlamentar}, {parlamentar.siglaPartido}, {parlamentar.uf}.{" "}
          {parlamentar.casa}.
        </p>
      </header>

      <div className="mt-6 rounded-cartao border border-borda-suave bg-surface p-5">
        <ValoresDaDespesa despesa={despesa} grande />
        {despesa.ehEstorno ? (
          <p className="mt-2 text-texto-suave">
            É um estorno: uma devolução à cota. O valor entra negativo porque abate o gasto —
            escondê-lo inflaria o total do parlamentar.
          </p>
        ) : null}

        <dl className="mt-4 grid gap-4 border-t border-borda-suave pt-4 sm:grid-cols-2">
          <div>
            <dt className="text-sm font-semibold text-texto-suave">Fornecedor</dt>
            <dd className="mt-0.5 font-semibold break-words text-texto">
              {despesa.nomeFornecedor ?? "Não identificado"}
            </dd>
            {/* CNPJ inteiro porque é dado de pessoa jurídica e público; CPF a API já entrega
                mascarado, e a borda não tem como contornar isso. */}
            <dd className="numeros text-sm text-texto-suave">{documentoPublicavel(despesa.documentoFornecedor)}</dd>
          </div>

          <div>
            <dt className="text-sm font-semibold text-texto-suave">Data e competência</dt>
            <dd className="numeros mt-0.5 font-semibold text-texto">
              {formatarDataDoDocumento(despesa.dataDocumento)}
            </dd>
            <dd className="numeros text-sm text-texto-suave">
              competência {String(despesa.mes).padStart(2, "0")}/{despesa.ano}
            </dd>
          </div>
        </dl>

        <div className="mt-4">
          <Comprovante despesa={despesa} casa={parlamentar.casa} />
        </div>
      </div>

      <section aria-labelledby="titulo-achados" className="mt-8">
        <h2 id="titulo-achados" className="font-display text-xl font-bold text-titulo">
          O que o motor de regras apontou
        </h2>

        {despesa.flags.length === 0 ? (
          <p className="mt-2 text-texto-suave">
            Nenhuma regra emitiu indicador sobre esta despesa.{" "}
            <strong className="text-texto">Isso não é atestado de regularidade:</strong> parte das
            regras pode não ter tido dado público para avaliá-la. O bloco “O que foi analisado”, no
            perfil de{" "}
            <Link
              href={`/parlamentares/${parlamentar.id}`}
              className="font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
            >
              {parlamentar.nomeParlamentar}
            </Link>
            , diz quais regras puderam ser aplicadas.
          </p>
        ) : (
          <ul className="mt-3 grid gap-2">
            {despesa.flags.map((flag) => (
              <li key={flag.codigoRegra} className="rounded-cartao border border-borda-suave bg-surface p-4">
                <div className="flex flex-wrap items-center gap-2">
                  <TagDeGravidade gravidade={flag.gravidade} />
                  {/* O grau de confiança viaja com o achado porque nem todo indicador é um
                      fato: REG_NET_004 cruza pessoas e sai com 0,7, não com 1,0. */}
                  <span className="numeros text-sm text-texto-suave">
                    confiança {(flag.grauDeConfianca * 100).toFixed(0)}%, regra {flag.codigoRegra}
                  </span>
                </div>
                <p className="mt-2 text-texto">{flag.descricao}</p>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/*
        O contraditório mora aqui, entre o achado e o documento — e não numa página à parte.
        Só aparece o lugar quando há achado: sem indicador, não há o que responder.
      */}
      {despesa.flags.length > 0 || (despesa.respostas?.length ?? 0) > 0 ? (
        <section aria-labelledby="titulo-resposta" className="mt-8">
          <h2 id="titulo-resposta" className="font-display text-xl font-bold text-titulo">
            O que o gabinete diz
          </h2>
          <div className="mt-3">
            {despesa.respostas?.length ? (
              <RespostasDoGabinete respostas={despesa.respostas} />
            ) : (
              <SemRespostaDoGabinete />
            )}
          </div>
        </section>
      ) : null}

      <p className="mt-8 border-t border-borda-suave pt-4 text-sm text-texto-suave">
        Os indicadores acima descrevem inconsistências entre registros públicos e{" "}
        <strong className="text-texto">não caracterizam, por si, irregularidade</strong>. Cada um
        é identificado pelo código da regra que o produziu.
      </p>
    </main>
  );
}
