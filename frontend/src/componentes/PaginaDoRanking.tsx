import { ranquearTudo } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import type { Ordenacao } from "@/lib/ordenacoes";
import type { Casa } from "@/lib/casas";
import { TabelaDoRanking } from "./TabelaDoRanking";
import { PainelDaCasa } from "./PainelDaCasa";

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
      {/*
        A casa é o TÍTULO, e "gastos da cota parlamentar" virou o contexto acima dele.

        Antes a frase aparecia duas vezes: no título e no nome da casa logo abaixo. E o
        cabeçalho do site já diz em que casa se está, com a aba marcada.

        O nome da casa continua no `h1` — e isso não é a repetição que foi removida. São
        631 páginas disputando o índice do buscador, que é o canal de aquisição principal
        (ADR-0001); dois rankings com `h1` idêntico competiriam entre si. Aba de navegação
        e título de página são papéis diferentes.
      */}
      <p className="text-xs font-bold tracking-widest text-acento uppercase">
        Gastos da cota parlamentar
      </p>

      <h1 className="mt-1 font-display text-3xl font-bold tracking-tight text-titulo">
        {casa.nomeOficial}
      </h1>

      <p className="mt-2 max-w-3xl text-sm">
        Dados públicos {casa.chave === "camara" ? "da" : "do"} {casa.nomeOficial}, cota{" "}
        {casa.cota}. Os indicadores vêm de um motor de regras determinístico e{" "}
        <strong>descrevem inconsistências, não imputam conduta.</strong> Cada achado traz o
        que a regra procurou, com o documento oficial ao alcance do clique quando a fonte o
        publica.
      </p>

      {/*
        A fileira de pílulas das três ordenações virou submenu no cabeçalho. A explicação de
        cada uma, porém, FICA aqui: ela existe para impedir a leitura errada — "densidade"
        que exclui gente sem dizer por quê faz o leitor concluir que essas pessoas não têm
        indicador nenhum. Num submenu, cabe o rótulo e não a ressalva.
      */}
      <p className="mt-4 max-w-3xl text-sm text-texto-suave">{ordenacao.explicacao}</p>

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
          <PainelDaCasa casa={casa} itens={itens} />

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
