import Link from "next/link";
import { notFound } from "next/navigation";
import { listarDespesas, obterPerfil } from "@/lib/api";
import { BlocoDeCobertura } from "@/componentes/Abstencao";
import { Comprovante, ValoresDaDespesa, formatarReais } from "@/componentes/Comprovante";
import { TagDeGravidade } from "@/componentes/TagDeGravidade";
import type { ItemDeDespesa } from "@/lib/contrato";

export const revalidate = 300;

function Numero({
  rotulo,
  valor,
  nota,
}: {
  rotulo: string;
  valor: string;
  nota?: string;
}) {
  return (
    <div className="border border-borda-suave bg-fundo p-4">
      <dt className="text-xs font-semibold text-texto-suave">{rotulo}</dt>
      <dd className="numeros mt-1 text-xl font-bold text-titulo">{valor}</dd>
      {nota ? <p className="mt-1 text-xs text-texto-suave">{nota}</p> : null}
    </div>
  );
}

function Despesa({ despesa }: { despesa: ItemDeDespesa }) {
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
              <TagDeGravidade gravidade={flag.gravidade} codigoRegra={flag.codigoRegra} />
              <p className="flex-1 text-xs text-texto">{flag.descricao}</p>
            </li>
          ))}
        </ul>
      ) : null}

      <div className="mt-3">
        <Comprovante despesa={despesa} />
      </div>
    </li>
  );
}

export default async function Perfil({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const perfil = await obterPerfil(id);

  if (!perfil) {
    notFound();
  }

  const despesas = await listarDespesas(id, { comFlag: true, itens: 25 });

  return (
    <main className="mx-auto max-w-7xl px-4 py-8">
      <Link href="/" className="text-sm text-titulo underline-offset-2 hover:underline">
        ← Ranking
      </Link>

      <header className="mt-4">
        <h1 className="text-2xl font-bold text-titulo">{perfil.nomeParlamentar}</h1>
        <p className="mt-1 text-sm text-texto-suave">
          {perfil.nomeCivil} · {perfil.siglaPartido} · {perfil.uf} · {perfil.casa}
        </p>
      </header>

      <dl className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Numero
          rotulo="Volume gasto"
          valor={formatarReais(perfil.volumeTotalGasto)}
          nota={`${perfil.despesasAvaliadas.toLocaleString("pt-BR")} despesas analisadas`}
        />
        <Numero
          rotulo="Densidade de indicadores"
          valor={
            perfil.indiceDeRisco === null
              ? "não publicável"
              : perfil.indiceDeRisco.toFixed(2)
          }
          nota={
            perfil.indiceDeRisco === null
              ? "Menos de 100 despesas: a densidade seria ruído."
              : "Indicadores por 100 despesas."
          }
        />
        <Numero
          rotulo="Alta relevância"
          valor={perfil.flagsAltas.toLocaleString("pt-BR")}
          nota="Os achados de maior peso."
        />
        <Numero
          rotulo="Média e baixa"
          valor={`${perfil.flagsMedias} · ${perfil.flagsBaixas}`}
          nota="Indicadores de peso intermediário e baixo."
        />
      </dl>

      {/*
        A cobertura vem ANTES da lista de achados, e não em rodapé. É o que impede o leitor
        de ler o silêncio de uma regra como aprovação — ADR-0013 §2.
      */}
      <div className="mt-8">
        <BlocoDeCobertura
          cobertura={perfil.cobertura}
          regrasSemDado={perfil.regrasSemDado}
        />
      </div>

      <section aria-labelledby="titulo-despesas" className="mt-8">
        <h2 id="titulo-despesas" className="text-base font-bold text-titulo">
          Despesas com indicadores
        </h2>

        {despesas && despesas.total > 0 ? (
          <>
            <p className="mt-1 text-sm text-texto-suave">
              <strong className="numeros">{despesas.total.toLocaleString("pt-BR")}</strong>{" "}
              de {perfil.despesasAvaliadas.toLocaleString("pt-BR")} despesas receberam ao
              menos um indicador.
            </p>

            <ul className="mt-3 border border-borda-suave">
              {despesas.itens.map((despesa) => (
                <Despesa key={despesa.id} despesa={despesa} />
              ))}
            </ul>
          </>
        ) : (
          <p className="mt-1 text-sm text-texto-suave">
            Nenhuma despesa recebeu indicador entre as que puderam ser analisadas.
          </p>
        )}
      </section>
    </main>
  );
}
