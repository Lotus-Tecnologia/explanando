"use client";

import Link from "next/link";
import type { NoDoEstado } from "@/lib/contrato";
import { formatarNumero } from "@/lib/formato";
import { corDoPoder } from "@/lib/grafo";

/**
 * O painel de uma unidade selecionada no grafo.
 *
 * **É um resumo, e a página da unidade continua sendo o destino.** A referência põe aqui
 * descrição em prosa, fonte legal, site oficial, ocupante com retrato e um feed de
 * notícias — nada disso o SIORG publica, e inventar não é opção. O que temos são os
 * campos do cadastro, os números de pessoal e as ligações; o painel mostra isso e leva
 * para a página, que tem a lista completa de subordinadas.
 *
 * O painel é enfeite sobre um link que já funcionava: sem JavaScript, clicar num nó abre
 * `/estado/{codigo}` direto.
 */
export function PainelDaUnidade({
  no,
  ligados,
  aoFechar,
}: {
  no: NoDoEstado;
  ligados: readonly NoDoEstado[];
  aoFechar: () => void;
}) {
  const fichas = [
    { termo: "Tipo", valor: no.tipo },
    { termo: "Poder", valor: no.poder },
    { termo: "Forma jurídica", valor: no.natureza },
    { termo: "Base legal", valor: no.baseLegal },
  ];

  // Quem manda nele e quem ele manda, separados: é a única distinção de relação que a
  // fonte nos dá, e agrupar as duas numa lista só perderia a direção.
  const acima = ligados.filter((outro) => outro.codigo === (no.codigoPaiNoMapa ?? no.codigoPai));
  const abaixo = ligados.filter((outro) => !acima.includes(outro));

  const parcial = no.servidores != null && no.poder !== null && no.poder !== "Executivo";

  return (
    <aside
      className="rounded-cartao border border-borda-suave bg-surface p-5 shadow-cartao"
      aria-label={`Resumo de ${no.nome}`}
    >
      <div className="flex items-start gap-3">
        <span
          aria-hidden="true"
          className="mt-1.5 size-3.5 shrink-0 rounded-[4px]"
          style={{ background: corDoPoder(no.chaveDoPoder) }}
        />

        <div className="min-w-0 flex-1">
          <h3 className="font-display text-xl font-bold tracking-tight text-balance text-titulo">
            {no.nome}
          </h3>
          {no.sigla ? (
            <p className="mt-0.5 text-sm font-semibold text-texto-suave">{no.sigla}</p>
          ) : null}
        </div>

        <button
          type="button"
          onClick={aoFechar}
          className="shrink-0 rounded-selo px-2.5 py-1 text-xs font-semibold text-texto-suave hover:bg-realce hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Fechar
        </button>
      </div>

      <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3">
        {fichas.map((ficha) => (
          <div key={ficha.termo}>
            <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
              {ficha.termo}
            </dt>
            <dd className="mt-0.5 text-sm text-texto">
              {ficha.valor ?? (
                <span className="text-texto-suave">Não informado pela fonte</span>
              )}
            </dd>
          </div>
        ))}

        <div>
          <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
            Unidades subordinadas
          </dt>
          <dd className="mt-0.5 font-display text-xl font-bold text-titulo">
            {formatarNumero(no.unidadesSubordinadas)}
          </dd>
        </div>

        <div>
          <dt className="text-xs font-bold tracking-wider text-texto-suave uppercase">
            Servidores no ramo
          </dt>
          <dd className="mt-0.5 font-display text-xl font-bold text-titulo">
            {/* Sem dado, e nunca zero: a ponte entre os cadastros nao alcanca tudo, e o
                de pessoal e' do Executivo civil. */}
            {no.servidoresNoRamo == null ? (
              <span className="text-sm font-normal text-texto-suave">Sem dado</span>
            ) : (
              formatarNumero(no.servidoresNoRamo)
            )}
          </dd>
        </div>
      </dl>

      {parcial ? (
        <p className="mt-3 text-xs text-texto">
          <strong className="font-semibold text-titulo">Contagem parcial.</strong> O cadastro
          de pessoal é do Executivo civil, e o {no.poder} mantém folha própria.
        </p>
      ) : null}

      {acima.length > 0 ? (
        <Ligacoes titulo="Responde a" nos={acima} />
      ) : null}

      {abaixo.length > 0 ? (
        <Ligacoes
          titulo="Respondem a esta unidade, no mapa"
          nos={abaixo}
          nota={
            "São as ligações que o desenho mostra — só entes, órgãos e entidades. A " +
            "página traz também as unidades administrativas."
          }
        />
      ) : null}

      <Link
        href={`/estado/${no.codigo}`}
        className="mt-5 inline-block rounded-campo bg-titulo px-4 py-2 text-sm font-semibold text-white hover:bg-titulo-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      >
        Abrir a página {no.sigla ? `da ${no.sigla}` : "completa"}
      </Link>
    </aside>
  );
}

function Ligacoes({
  titulo,
  nos,
  nota,
}: {
  titulo: string;
  nos: readonly NoDoEstado[];
  nota?: string;
}) {
  const MOSTRADOS = 12;

  return (
    <section className="mt-5">
      <h4 className="flex items-baseline justify-between gap-3 text-sm font-semibold text-titulo">
        <span>{titulo}</span>
        <span className="numeros text-xs font-normal text-texto-suave">{nos.length}</span>
      </h4>

      {nota ? <p className="mt-1 text-xs text-texto-suave">{nota}</p> : null}

      <ul className="mt-2 grid gap-1 sm:grid-cols-2">
        {[...nos]
          .sort((a, b) => b.unidadesSubordinadas - a.unidadesSubordinadas)
          .slice(0, MOSTRADOS)
          .map((outro) => (
            <li key={outro.codigo}>
              <Link
                href={`/estado/${outro.codigo}`}
                className="flex items-baseline gap-2 rounded-campo px-2 py-1 text-sm text-texto hover:bg-realce focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-titulo"
              >
                <span
                  aria-hidden="true"
                  className="size-2 shrink-0 translate-y-1 rounded-[2px]"
                  style={{ background: corDoPoder(outro.chaveDoPoder) }}
                />
                <span className="min-w-0 flex-1 truncate">{outro.nome}</span>
              </Link>
            </li>
          ))}
      </ul>

      {nos.length > MOSTRADOS ? (
        <p className="mt-1 px-2 text-xs text-texto-suave">
          e mais {formatarNumero(nos.length - MOSTRADOS)}
        </p>
      ) : null}
    </section>
  );
}
