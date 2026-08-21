import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import { ORDENACOES, type Ordenacao } from "@/lib/ordenacoes";
import { TabelaDoRanking } from "./TabelaDoRanking";

/**
 * O corpo das três páginas de ranking.
 *
 * As três renderizam a mesma coisa com uma ordenação diferente, e cada uma tem rota própria
 * para ser estática e indexável. Escrever o corpo uma vez é o que garante que a explicação
 * de cada ordenação apareça sempre ao lado da tabela — sem ela, "densidade" que exclui 15
 * pessoas é lido como "essas 15 não têm indicador".
 */
export async function PaginaDoRanking({ ordenacao }: { ordenacao: Ordenacao }) {
  const itens = await ranquearTudo(ordenacao.chave);

  return (
    <main className="mx-auto max-w-7xl px-4 py-8">
      <h1 className="text-2xl font-bold text-titulo">Gastos da cota parlamentar</h1>

      <p className="mt-2 max-w-3xl text-sm">
        Dados públicos da Câmara dos Deputados. Os indicadores vêm de um motor de regras
        determinístico e{" "}
        <strong>descrevem inconsistências, não imputam conduta.</strong> Cada achado é
        identificado pelo código da regra que o produziu, com o documento oficial ao alcance
        do clique quando a fonte o publica.
      </p>

      <nav aria-label="Ordenação do ranking" className="mt-6">
        <ul className="flex flex-wrap gap-2">
          {ORDENACOES.map((opcao) => {
            const ativa = opcao.chave === ordenacao.chave;

            return (
              <li key={opcao.chave}>
                <Link
                  href={opcao.caminho}
                  aria-current={ativa ? "page" : undefined}
                  className={
                    ativa
                      ? "inline-block bg-titulo px-3 py-1.5 text-sm font-semibold text-white"
                      : "inline-block border border-borda-suave px-3 py-1.5 text-sm font-semibold text-titulo hover:bg-surface focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
                  }
                >
                  {opcao.rotulo}
                </Link>
              </li>
            );
          })}
        </ul>

        <p className="mt-2 max-w-3xl text-xs text-texto-suave">{ordenacao.explicacao}</p>
      </nav>

      {/*
        Fonte fora do ar não vira tabela vazia. Uma lista sem linhas seria lida como "nenhum
        parlamentar gastou", que é a mesma classe de erro do `Falhas: 0` da coleta: ausência
        apresentada como resultado.
      */}
      {itens === null ? (
        <p className="mt-6 border border-borda-suave bg-abstencao-fundo p-4 text-sm text-abstencao-texto">
          O ranking não pôde ser carregado agora. A falha é da plataforma, não do dado
          público — os registros seguem disponíveis nas fontes originais da Câmara e do
          Senado. Esta página se atualiza sozinha em poucos minutos.
        </p>
      ) : (
        <>
          <TabelaDoRanking itens={itens} rotulo={ordenacao.rotulo} />

          <p className="mt-3 text-xs text-texto-suave">
            {ordenacao.chave === "volume" ? (
              <>
                Os <strong className="numeros">{formatarNumero(itens.length)}</strong>{" "}
                parlamentares da base aparecem aqui.
              </>
            ) : (
              <>
                <strong className="numeros">{formatarNumero(itens.length)}</strong>{" "}
                parlamentares aparecem nesta ordenação. Quem tem menos de 100 despesas
                analisadas fica de fora — a densidade seria ruído, não sinal —{" "}
                <strong>e ficar de fora não significa ausência de achado.</strong>
              </>
            )}
          </p>
        </>
      )}
    </main>
  );
}
