import Link from "next/link";
import { Comprovante } from "./Comprovante";
import { RespostasDoGabinete } from "./RespostasDoGabinete";
import { TagDeGravidade } from "./TagDeGravidade";
import type { Gravidade, ItemDeDespesa } from "@/lib/contrato";
import { formatarDataDoDocumento, formatarReais } from "@/lib/formato";
import { caixaDeFrase, documentoPublicavel } from "@/lib/frases";

/**
 * A lista de despesas, compartilhada pelo perfil e pela listagem completa.
 *
 * Uma implementação só porque as duas telas precisam mostrar a mesma coisa do mesmo jeito —
 * e porque a regra do ADR-0012 §2 (glosa desconhecida some por inteiro) e a do ADR-0013
 * (peso visual por gravidade) não podem valer numa tela e faltar na outra.
 *
 * **Revelação progressiva.** Cada linha é um `<details>`: fechada mostra só data, tipo,
 * fornecedor, o achado mais grave e o valor; aberta mostra cada achado, o documento e a nota
 * fiscal. Nativo de propósito: abre com Enter e Espaço, funciona sem JavaScript e não leva o
 * foco para lugar nenhum.
 */

const PESO: Record<Gravidade, number> = { Alta: 3, Media: 2, Baixa: 1 };

/** A gravidade que representa a linha fechada: a da API, ou a maior entre os achados. */
function maiorDa(despesa: ItemDeDespesa): Gravidade | null {
  if (despesa.maiorGravidade) return despesa.maiorGravidade;
  return despesa.flags.reduce<Gravidade | null>(
    (maior, flag) => (maior === null || PESO[flag.gravidade] > PESO[maior] ? flag.gravidade : maior),
    null,
  );
}

export function LinhaDeDespesa({
  despesa,
  casa,
  aberta = false,
}: {
  despesa: ItemDeDespesa;
  casa?: string;
  aberta?: boolean;
}) {
  const maior = maiorDa(despesa);
  const outros = despesa.flags.length - 1;
  // Glosa só aparece quando é conhecida E houve corte: nula não é zero (ADR-0012 §2).
  const houveCorte = despesa.valorGlosa !== null && despesa.valorGlosa > 0;

  return (
    <li>
      <details open={aberta} className="group rounded-cartao border border-borda-suave bg-surface">
        <summary className="grid min-h-16 cursor-pointer list-none grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1.5 px-4 py-3 [&::-webkit-details-marker]:hidden">
          <span className="min-w-0">
            <span className="block font-semibold text-texto">{caixaDeFrase(despesa.tipoDespesa)}</span>
            <span className="block truncate text-sm text-texto-suave" title={despesa.nomeFornecedor ?? undefined}>
              <span className="numeros">{formatarDataDoDocumento(despesa.dataDocumento)}</span>
              {", "}
              {despesa.nomeFornecedor ?? "fornecedor não identificado"}
            </span>
          </span>

          <span className="flex items-center gap-2">
            <span className="numeros text-right font-display text-[1.0625rem] font-bold whitespace-nowrap text-titulo">
              {formatarReais(despesa.valorLiquido)}
            </span>
            <svg
              aria-hidden="true"
              viewBox="0 0 20 20"
              className="size-5 shrink-0 text-texto-suave transition-transform group-open:rotate-180"
            >
              <path d="m5 8 5 5 5-5" fill="none" stroke="currentColor" strokeWidth="2" />
            </svg>
          </span>

          {maior ? (
            <span className="col-span-2 flex items-center gap-2">
              <TagDeGravidade gravidade={maior} />
              {outros > 0 ? (
                <span className="text-sm font-semibold text-texto-suave">
                  +{outros}
                  <span className="sr-only"> {outros === 1 ? "outro achado" : "outros achados"}</span>
                </span>
              ) : null}
            </span>
          ) : null}
        </summary>

        <div className="grid gap-4 border-t border-borda-suave px-4 py-4">
          {despesa.flags.length > 0 ? (
            <ul className="grid gap-3">
              {despesa.flags.map((flag) => (
                <li key={flag.codigoRegra} className="grid gap-1.5">
                  <span>
                    <TagDeGravidade gravidade={flag.gravidade} />
                  </span>
                  <p className="text-texto">{flag.descricao}</p>
                </li>
              ))}
            </ul>
          ) : null}

          <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-texto-suave">Documento do fornecedor</dt>
              <dd className="numeros font-semibold text-texto">{documentoPublicavel(despesa.documentoFornecedor)}</dd>
            </div>
            <div>
              <dt className="text-texto-suave">Competência</dt>
              <dd className="numeros font-semibold text-texto">
                {String(despesa.mes).padStart(2, "0")}/{despesa.ano}
              </dd>
            </div>
            {houveCorte ? (
              <div>
                <dt className="text-texto-suave">Declarado e glosa</dt>
                <dd className="numeros font-semibold text-texto">
                  {formatarReais(despesa.valorDocumento)}, glosa {formatarReais(despesa.valorGlosa!)}
                </dd>
              </div>
            ) : null}
            {despesa.ehEstorno ? (
              <div>
                <dt className="text-texto-suave">Tipo</dt>
                <dd className="font-semibold text-texto">Estorno, devolução à cota</dd>
              </div>
            ) : null}
          </dl>

          {despesa.respostas?.length ? <RespostasDoGabinete respostas={despesa.respostas} compacta /> : null}

          <div className="flex flex-wrap items-center gap-x-5 gap-y-2">
            <Comprovante despesa={despesa} casa={casa} />
            {/*
              Cada despesa tem endereço próprio. É o que um link compartilhado abre, e é onde o
              achado aparece junto do documento oficial — o arranjo em que o ADR-0004 se apoia.
            */}
            <Link
              href={`/despesas/${despesa.id}`}
              className="inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
            >
              Abrir a página da despesa
            </Link>
          </div>
        </div>
      </details>
    </li>
  );
}

export function ListaDeDespesas({
  despesas,
  casa,
  mensagemVazia,
}: {
  despesas: ItemDeDespesa[];
  casa?: string;

  /**
   * Por que a lista está vazia. Nem todo vazio tem a mesma causa: filtro que não casa nada
   * e página fora da faixa produzem a mesma tela e significam coisas diferentes.
   */
  mensagemVazia?: React.ReactNode;
}) {
  if (despesas.length === 0) {
    // "Nenhuma despesa corresponde a este filtro" — e não "nenhuma despesa". A segunda frase
    // afirmaria sobre o parlamentar algo que o filtro não mediu.
    return (
      <p className="mt-3 rounded-cartao border border-dashed border-borda-estrutural bg-surface p-4 text-texto-suave">
        {mensagemVazia ?? "Nenhuma despesa corresponde a este filtro."}
      </p>
    );
  }

  return (
    <ul className="mt-3 grid gap-2">
      {despesas.map((despesa) => (
        <LinhaDeDespesa key={despesa.id} despesa={despesa} casa={casa} />
      ))}
    </ul>
  );
}
