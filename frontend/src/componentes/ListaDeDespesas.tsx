import Link from "next/link";
import { Comprovante, ValoresDaDespesa } from "./Comprovante";
import { TagDeGravidade } from "./TagDeGravidade";
import type { ItemDeDespesa } from "@/lib/contrato";

/**
 * A lista de despesas, compartilhada pelo perfil e pela listagem completa.
 *
 * Uma implementação só porque as duas telas precisam mostrar a mesma coisa do mesmo jeito —
 * e porque a regra do ADR-0012 §2 (glosa desconhecida some por inteiro) e a do ADR-0013
 * (peso visual por gravidade) não podem valer numa tela e faltar na outra.
 */

function LinhaDaDespesa({ despesa, casa }: { despesa: ItemDeDespesa; casa?: string }) {
  return (
    <li className="border-b border-borda-suave px-3 py-3 last:border-b-0 odd:bg-fundo even:bg-surface">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-titulo">{despesa.tipoDespesa}</p>
          <p className="mt-0.5 text-xs text-texto-suave">
            {despesa.nomeFornecedor ?? "Fornecedor não identificado"}
            <span className="numeros"> · {despesa.documentoFornecedor}</span>
          </p>
          <p className="numeros mt-0.5 text-xs text-texto-suave">
            {new Date(despesa.dataDocumento).toLocaleDateString("pt-BR")} · competência{" "}
            {String(despesa.mes).padStart(2, "0")}/{despesa.ano}
          </p>
        </div>

        <ValoresDaDespesa despesa={despesa} />
      </div>

      {despesa.flags.length > 0 ? (
        <ul className="mt-3 space-y-2">
          {despesa.flags.map((flag) => (
            <li key={flag.codigoRegra} className="flex flex-wrap items-start gap-2">
              <TagDeGravidade gravidade={flag.gravidade} />
              <p className="flex-1 text-xs text-texto">{flag.descricao}</p>
            </li>
          ))}
        </ul>
      ) : null}

      <div className="mt-3 flex flex-wrap items-center gap-3">
        <Comprovante despesa={despesa} casa={casa} />

        {/*
          Cada despesa tem endereço próprio. É o que um link compartilhado abre, e é onde o
          achado aparece junto do documento oficial — o arranjo em que o ADR-0004 se apoia
          para descrever sem imputar.
        */}
        <Link
          href={`/despesas/${despesa.id}`}
          className="text-xs font-semibold text-titulo underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Ver despesa
        </Link>
      </div>
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
      <p className="mt-3 border border-borda-suave bg-abstencao-fundo p-4 text-sm text-abstencao-texto">
        {mensagemVazia ?? "Nenhuma despesa corresponde a este filtro."}
      </p>
    );
  }

  return (
    <ul className="mt-3 border border-borda-suave">
      {despesas.map((despesa) => (
        <LinhaDaDespesa key={despesa.id} despesa={despesa} casa={casa} />
      ))}
    </ul>
  );
}
