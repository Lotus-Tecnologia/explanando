import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { listarDespesas, obterPerfil } from "@/lib/api";
import { ITENS_POR_PAGINA, comFlagDe, lerFiltros } from "@/lib/filtros";
import { formatarNumero } from "@/lib/formato";
import { BarraDeFiltros } from "@/componentes/BarraDeFiltros";
import { ListaDeDespesas } from "@/componentes/ListaDeDespesas";
import { Paginacao } from "@/componentes/Paginacao";

/*
  Literal, e não a constante importada de `lib/api`: o Next lê este export **estaticamente**,
  antes de executar qualquer coisa, e uma referência que ele não consegue resolver derruba o
  build inteiro com "Invalid segment configuration export". O valor é o mesmo
  `SEGUNDOS_DE_REVALIDACAO` — cinco minutos, a janela do cache do ranking na API.
*/
export const revalidate = 300;

type Parametros = { params: Promise<{ id: string }> };
type Consulta = { searchParams: Promise<Record<string, string | string[] | undefined>> };

export async function generateMetadata({ params }: Parametros): Promise<Metadata> {
  const { id } = await params;
  const perfil = await obterPerfil(id);

  if (!perfil) {
    return { title: "Parlamentar não encontrado" };
  }

  return {
    title: `Despesas de ${perfil.nomeParlamentar} (${perfil.siglaPartido}-${perfil.uf})`,
    description: `Todas as ${formatarNumero(perfil.despesasAvaliadas)} despesas da cota parlamentar analisadas, com filtro por ano, tipo e indicador.`,

    // A listagem filtrada não deve competir no índice com o perfil: são milhares de
    // combinações de query string sobre o mesmo conteúdo. O canônico aponta para a lista
    // sem filtro, e o perfil segue sendo a página que o buscador mostra.
    alternates: { canonical: `/parlamentares/${id}/despesas` },
  };
}

/**
 * Todas as despesas de um parlamentar, com a barra lateral de filtros.
 *
 * O perfil mostra só as que receberam indicador — o recorte certo para quem chega de um link
 * e quer ver o achado. Esta página é o oposto: **o gasto inteiro**, incluindo o que nenhuma
 * regra marcou, porque um portal de transparência que só mostra o que ele mesmo apontou está
 * publicando a própria opinião, não a prestação de contas.
 */
export default async function Despesas({ params, searchParams }: Parametros & Consulta) {
  const { id } = await params;
  const perfil = await obterPerfil(id);

  if (!perfil) {
    notFound();
  }

  // Os anos saem da série mensal, que já vem no perfil: são exatamente os meses em que houve
  // gasto. Uma lista fixa ofereceria anos vazios, e ano vazio devolve lista vazia — que o
  // leitor lê como "não gastou".
  const anos = [...new Set(perfil.serieMensal.map((mes) => mes.ano))].sort((a, b) => b - a);

  const filtros = lerFiltros(await searchParams, anos, perfil.tiposDeDespesa);

  const pagina = await listarDespesas(id, {
    ano: filtros.ano ?? undefined,
    tipo: filtros.tipo ?? undefined,
    comFlag: comFlagDe(filtros.indicador),
    pagina: filtros.pagina,
    itens: ITENS_POR_PAGINA,
  });

  const base = `/parlamentares/${id}/despesas`;

  return (
    <main className="mx-auto max-w-4xl px-4 py-6 sm:py-10">
      <Link
        href={`/parlamentares/${id}`}
        className="inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
      >
        ← {perfil.nomeParlamentar}
      </Link>

      <h1 className="mt-2 font-display text-[1.75rem] leading-tight font-bold tracking-tight text-titulo sm:text-4xl">
        Despesas de {perfil.nomeParlamentar}
      </h1>
      <p className="mt-2 text-texto-suave">
        {perfil.siglaPartido}, {perfil.uf}. {perfil.casa}.
      </p>

      <div className="mt-6">
        <BarraDeFiltros base={base} anos={anos} tipos={perfil.tiposDeDespesa} filtros={filtros} />
      </div>

      <section aria-label="Despesas" className="mt-4">
        {pagina === null ? (
          <p className="rounded-cartao border border-borda-suave bg-surface p-4 text-texto-suave">
            As despesas não puderam ser carregadas agora. A falha é da plataforma, não do dado
            público.
          </p>
        ) : (
          <>
            <p className="text-texto-suave">
              <strong className="numeros text-texto">{formatarNumero(pagina.total)}</strong>{" "}
              {pagina.total === 1 ? "despesa" : "despesas"}
              {filtros.ano !== null || filtros.tipo !== null || filtros.indicador !== "todas"
                ? " com este filtro"
                : " no total"}
              . Toque numa linha para ver os achados e a nota fiscal.
            </p>

            <ListaDeDespesas
              despesas={pagina.itens}
              casa={perfil.casa}
              mensagemVazia={
                // Página fora da faixa não é filtro sem resultado: o filtro casa centenas, e
                // só o número da página é que não existe. Dizer a mesma frase nos dois casos
                // seria mentir num deles.
                pagina.total > 0 ? (
                  <>
                    Esta página não existe para o filtro atual — ele tem{" "}
                    {formatarNumero(pagina.totalDePaginas)}{" "}
                    {pagina.totalDePaginas === 1 ? "página" : "páginas"}.{" "}
                    <Link
                      href={base}
                      className="font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4"
                    >
                      Voltar ao começo
                    </Link>
                    .
                  </>
                ) : undefined
              }
            />

            <Paginacao base={base} filtros={filtros} totalDePaginas={pagina.totalDePaginas} />
          </>
        )}
      </section>
    </main>
  );
}
