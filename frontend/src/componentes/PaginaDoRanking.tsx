import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import { ORDENACOES, caminhoDoRanking, type Ordenacao } from "@/lib/ordenacoes";
import type { Casa } from "@/lib/casas";
import { ExploradorDaCasa } from "./ExploradorDaCasa";

/**
 * O corpo das páginas de ranking da Câmara e do Senado.
 *
 * Integra cabeçalho institucional, dashboards executivos com gráficos e pódio,
 * e a tabela de auditoria completa com filtros coordenados.
 */
export async function PaginaDoRanking({
  casa,
  ordenacao,
}: {
  casa: Casa;
  ordenacao: Ordenacao;
}) {
  // Os totais do painel são da casa inteira; a ordenação por densidade deixa quem não tem
  // amostra de fora da lista, mas não do gasto. O Next deduplica a busca quando é a mesma.
  const [itens, base] = await Promise.all([
    ranquearTudo(casa, ordenacao.chave),
    ranquearTudo(casa, "volume"),
  ]);
  const ehCamara = casa.chave === "camara";

  return (
    <main className="mx-auto max-w-7xl px-4 py-8 sm:py-12">
      {/* ---------------------------------------------------------------- */}
      {/*  CABEÇALHO ANALÍTICO DA CASA                                      */}
      {/* ---------------------------------------------------------------- */}
      <div className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <span
            className={`rounded-selo px-2.5 py-0.5 text-xs font-bold ${
              ehCamara
                ? "bg-surface text-texto border border-borda-suave"
                : "bg-realce text-titulo"
            }`}
          >
            {ehCamara ? "513 deputados federais · Cota CEAP" : "81 senadores da República · Cota CEAPS"}
          </span>

          <span className="text-xs text-texto-suave">
            Auditoria cívica contínua de dados abertos
          </span>
        </div>

        <h1 className="font-display text-3xl sm:text-4xl font-bold tracking-tight text-titulo">
          {casa.nomeOficial}
        </h1>

        <p className="max-w-3xl text-sm sm:text-base leading-relaxed text-texto">
          Gastos da cota parlamentar auditados contra a base da Receita Federal e o histórico de
          mandato. Os indicadores são produzidos por um motor de regras determinístico e{" "}
          <strong className="text-titulo font-semibold">
            descrevem inconsistências, sem imputar conduta.
          </strong>{" "}
          {/* "Regra aplicada", e não "violada": o ADR-0004 descreve, não imputa. E o Senado não
              publica comprovante (ADR-0012) — prometê-lo ali era falso para a casa inteira. */}
          {ehCamara
            ? "Cada achado informa a regra aplicada e dá acesso ao comprovante oficial."
            : "Cada achado informa a regra aplicada. O Senado não publica o comprovante das despesas."}
        </p>

        {/* Seletor Rápido de Ordenação / Visão */}
        <div className="mt-2 flex flex-col gap-2">
          <p className="text-xs font-bold uppercase tracking-wider text-texto-suave">
            Visão de análise ativa:
          </p>

          <nav aria-label="Visões do ranking" className="flex flex-wrap gap-2">
            {ORDENACOES.map((ord) => {
              const ativo = ord.chave === ordenacao.chave;
              const destino = caminhoDoRanking(casa, ord);

              return (
                <Link
                  key={ord.chave}
                  href={destino}
                  aria-current={ativo ? "page" : undefined}
                  className={`inline-flex items-center gap-2 rounded-campo px-3 py-2 text-xs font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-titulo ${
                    ativo
                      ? "bg-titulo text-sobre-titulo shadow-xs"
                      : "border border-borda-suave bg-surface text-texto hover:border-titulo hover:text-titulo"
                  }`}
                >
                  <span
                    aria-hidden="true"
                    className={`flex size-4 items-center justify-center rounded-full text-[10px] ${
                      ativo ? "bg-sobre-titulo/20 text-sobre-titulo" : "bg-realce text-titulo"
                    }`}
                  >
                    {ord.chave === "volume" ? "R$" : ord.chave === "indice" ? "%" : "!"}
                  </span>
                  <span>{ord.rotulo}</span>
                </Link>
              );
            })}
          </nav>
        </div>

        {/* Explicação da ordenação ativa */}
        <div className="mt-1 rounded-campo border border-borda-suave/80 bg-surface/70 p-3.5 text-xs text-texto-suave leading-relaxed">
          <strong className="text-texto font-semibold">{ordenacao.rotulo}:</strong>{" "}
          {ordenacao.explicacao}
        </div>
      </div>

      {/* ---------------------------------------------------------------- */}
      {/*  CONTEÚDO PRINCIPAL (DASHBOARD + LISTAGEM)                        */}
      {/* ---------------------------------------------------------------- */}
      {itens === null ? (
        <div className="mt-8 rounded-cartao border border-borda-suave bg-abstencao-fundo p-6 text-sm text-abstencao-texto">
          <p className="font-semibold text-titulo">Ranking temporariamente indisponível</p>
          <p className="mt-1 text-xs">
            Os números desta casa não puderam ser carregados agora. A falha é temporária de conexão
            com a API de dados abertos. Esta página se atualiza automaticamente em poucos minutos.
          </p>
        </div>
      ) : (
        <>
          <ExploradorDaCasa casa={casa} itens={itens} base={base ?? undefined} ordenacao={ordenacao} />

          {/* Nota técnica de rodapé */}
          <footer className="mt-8 border-t border-borda-suave pt-4 text-xs text-texto-suave">
            <p>
              {/*
                Lista vazia não vira "0 aparecem, por causa do piso": na auditoria de 25/09 o
                /senado/gravidade serviu um cache velho, vazio, enquanto a API já devolvia 86
                senadores — e a frase atribuía o vazio às pessoas. Vazio aqui é sobre nós.
              */}
              {itens.length === 0 ? (
                <>
                  Os dados desta ordenação não chegaram nesta atualização da página. Isso não é
                  uma afirmação sobre os {casa.integrantes}: a página se atualiza em poucos
                  minutos.
                </>
              ) : ordenacao.chave === "volume" ? (
                <>
                  Todos os{" "}
                  <strong className="numeros font-semibold text-texto">
                    {formatarNumero(itens.length)}
                  </strong>{" "}
                  {casa.integrantes} da base estão disponíveis neste ranking. A busca e os filtros
                  varrem o conjunto integral.
                </>
              ) : (
                <>
                  <strong className="numeros font-semibold text-texto">
                    {formatarNumero(itens.length)}
                  </strong>{" "}
                  {casa.integrantes} aparecem nesta ordenação. Conforme o critério de significância
                  estatística (ADR-0009), parlamentares com menos de 100 despesas analisadas ficam
                  apartados desta métrica — e{" "}
                  <strong>ficar de fora não representa atestado de ausência de achados.</strong>
                </>
              )}
            </p>
          </footer>
        </>
      )}
    </main>
  );
}
