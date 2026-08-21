import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { obterDespesa } from "@/lib/api";
import { formatarReais } from "@/lib/formato";
import { Comprovante, ValoresDaDespesa } from "@/componentes/Comprovante";
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
    title: `${despesa.tipoDespesa} — ${formatarReais(despesa.valorLiquido)} · ${parlamentar.nomeParlamentar}`,
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
    <main className="mx-auto max-w-3xl px-4 py-8">
      <Link
        href={`/parlamentares/${parlamentar.id}`}
        className="text-sm text-titulo underline-offset-2 hover:underline"
      >
        ← {parlamentar.nomeParlamentar}
      </Link>

      <header className="mt-4">
        <h1 className="text-2xl font-bold text-titulo">{despesa.tipoDespesa}</h1>
        <p className="mt-1 text-sm text-texto-suave">
          {parlamentar.nomeParlamentar} · {parlamentar.siglaPartido} · {parlamentar.uf} ·{" "}
          {parlamentar.casa}
        </p>
      </header>

      <dl className="mt-6 grid gap-3 sm:grid-cols-2">
        <div className="border border-borda-suave p-4">
          <dt className="text-xs font-semibold text-texto-suave">Fornecedor</dt>
          <dd className="mt-1 text-sm font-semibold text-titulo">
            {despesa.nomeFornecedor ?? "Não identificado"}
          </dd>
          {/* CNPJ inteiro porque é dado de pessoa jurídica e público; CPF a API já entrega
              mascarado, e a borda não tem como contornar isso. */}
          <dd className="numeros mt-0.5 text-xs text-texto-suave">
            {despesa.documentoFornecedor}
          </dd>
        </div>

        <div className="border border-borda-suave p-4">
          <dt className="text-xs font-semibold text-texto-suave">Data e competência</dt>
          <dd className="numeros mt-1 text-sm font-semibold text-titulo">
            {new Date(despesa.dataDocumento).toLocaleDateString("pt-BR")}
          </dd>
          <dd className="numeros mt-0.5 text-xs text-texto-suave">
            competência {String(despesa.mes).padStart(2, "0")}/{despesa.ano}
          </dd>
        </div>
      </dl>

      <div className="mt-3 border border-borda-suave p-4">
        <ValoresDaDespesa despesa={despesa} />
        {despesa.ehEstorno ? (
          <p className="mt-2 text-xs text-texto-suave">
            É um estorno: uma devolução à cota. O valor entra negativo porque abate o gasto —
            escondê-lo inflaria o total do parlamentar.
          </p>
        ) : null}
      </div>

      <section aria-labelledby="titulo-achados" className="mt-8">
        <h2 id="titulo-achados" className="text-base font-bold text-titulo">
          O que o motor de regras apontou
        </h2>

        {despesa.flags.length === 0 ? (
          <p className="mt-2 text-sm text-texto-suave">
            Nenhuma regra emitiu indicador sobre esta despesa.{" "}
            <strong>Isso não é atestado de regularidade:</strong> parte das regras pode não
            ter tido dado público para avaliá-la. O bloco “O que foi analisado”, no perfil de{" "}
            <Link
              href={`/parlamentares/${parlamentar.id}`}
              className="text-titulo underline underline-offset-2"
            >
              {parlamentar.nomeParlamentar}
            </Link>
            , diz quais regras puderam ser aplicadas.
          </p>
        ) : (
          <ul className="mt-3 space-y-3 border border-borda-suave p-4">
            {despesa.flags.map((flag) => (
              <li key={flag.codigoRegra}>
                <div className="flex flex-wrap items-center gap-2">
                  <TagDeGravidade gravidade={flag.gravidade} codigoRegra={flag.codigoRegra} />
                  {/* O grau de confiança viaja com o achado porque nem todo indicador é um
                      fato: REG_NET_004 cruza pessoas e sai com 0,7, não com 1,0. */}
                  <span className="numeros text-xs text-texto-suave">
                    confiança {(flag.grauDeConfianca * 100).toFixed(0)}%
                  </span>
                </div>
                <p className="mt-1 text-sm text-texto">{flag.descricao}</p>
              </li>
            ))}
          </ul>
        )}
      </section>

      <div className="mt-6">
        <Comprovante despesa={despesa} casa={parlamentar.casa} />
      </div>

      <p className="mt-6 border-t border-borda-suave pt-4 text-xs text-texto-suave">
        Os indicadores acima descrevem inconsistências entre registros públicos e{" "}
        <strong>não caracterizam, por si, irregularidade</strong>. Cada um é identificado pelo
        código da regra determinística que o produziu.
      </p>
    </main>
  );
}
