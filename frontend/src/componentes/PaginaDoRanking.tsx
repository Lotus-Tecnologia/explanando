import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import { ORDENACOES, caminhoDoRanking, type Ordenacao } from "@/lib/ordenacoes";
import { CASAS, type Casa } from "@/lib/casas";
import { TabelaDoRanking } from "./TabelaDoRanking";
import { CartoesDeNumero } from "./CartoesDeNumero";

/**
 * O corpo das três páginas de ranking.
 *
 * As três renderizam a mesma coisa com uma ordenação diferente, e cada uma tem rota própria
 * para ser estática e indexável. Escrever o corpo uma vez é o que garante que a explicação
 * de cada ordenação apareça sempre ao lado da tabela — sem ela, "densidade" que exclui 15
 * pessoas é lido como "essas 15 não têm indicador".
 */
export async function PaginaDoRanking({
  casa,
  ordenacao,
}: {
  casa: Casa;
  ordenacao: Ordenacao;
}) {
  const itens = await ranquearTudo(casa, ordenacao.chave);

  return (
    <main className="mx-auto max-w-7xl px-4 py-8">
      <h1 className="font-display text-3xl font-bold tracking-tight text-titulo">
        Gastos da cota parlamentar
        <span className="block text-texto-suave">{casa.nomeOficial}</span>
      </h1>

      <p className="mt-2 max-w-3xl text-sm">
        Dados públicos {casa.chave === "camara" ? "da" : "do"} {casa.nomeOficial}, cota{" "}
        {casa.cota}. Os indicadores vêm de um motor de regras determinístico e{" "}
        <strong>descrevem inconsistências, não imputam conduta.</strong> Cada achado traz o
        que a regra procurou, com o documento oficial ao alcance do clique quando a fonte o
        publica.
      </p>

      {/*
        As duas casas ficam sempre visíveis, uma ao lado da outra, e nunca no mesmo ranking:
        a CEAP e a CEAPS têm regras e valores próprios por UF, e os números não se comparam
        entre elas. Poder trocar de casa sem voltar à home é conveniência; misturá-las seria
        erro.
      */}
      <nav aria-label="Casa legislativa" className="mt-5">
        <ul className="flex flex-wrap gap-2">
          {CASAS.map((opcao) => {
            const atual = opcao.chave === casa.chave;

            return (
              <li key={opcao.chave}>
                <Link
                  href={caminhoDoRanking(opcao, ordenacao)}
                  aria-current={atual ? "page" : undefined}
                  className={
                    atual
                      ? "inline-block border-b-2 border-titulo px-1 pb-1 text-sm font-bold text-titulo"
                      : "inline-block border-b-2 border-transparent px-1 pb-1 text-sm font-semibold text-texto-suave hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
                  }
                >
                  {opcao.nomeOficial}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      <nav aria-label="Ordenação do ranking" className="mt-6">
        <ul className="flex flex-wrap gap-2">
          {ORDENACOES.map((opcao) => {
            const ativa = opcao.chave === ordenacao.chave;

            return (
              <li key={opcao.chave}>
                <Link
                  href={caminhoDoRanking(casa, opcao)}
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
        <p className="mt-6 rounded-cartao border border-borda-suave bg-abstencao-fundo p-4 text-sm text-abstencao-texto">
          O ranking não pôde ser carregado agora. A falha é da plataforma, não do dado
          público — os registros seguem disponíveis nas fontes originais da Câmara e do
          Senado. Esta página se atualiza sozinha em poucos minutos.
        </p>
      ) : (
        <>
          {/*
            Os números da casa antes da tabela: quem chega precisa saber o que está olhando
            antes de encarar 513 linhas. Depois da checagem de nulo, porque cartão com
            "R$ 0,00" afirmaria que ninguém gastou quando a fonte só não respondeu.
          */}
          <CartoesDeNumero casa={casa} itens={itens} />

          <TabelaDoRanking itens={itens} rotulo={ordenacao.rotulo} />

          <p className="mt-3 text-xs text-texto-suave">
            {ordenacao.chave === "volume" ? (
              <>
                {/*
                  "estão neste ranking", e não "aparecem aqui": com a exibição paginada, a
                  segunda frase passou a ser falsa — só 25 aparecem por vez. A ressalva sobre
                  a busca é o que garante que paginar não escondeu ninguém.
                */}
                {"Os "}
                <strong className="numeros">{formatarNumero(itens.length)}</strong>{" "}
                {casa.integrantes} da base estão neste ranking, e a busca acha qualquer um
                deles.
              </>
            ) : (
              <>
                <strong className="numeros">{formatarNumero(itens.length)}</strong>{" "}
                {casa.integrantes} aparecem nesta ordenação. Quem tem menos de 100 despesas
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
