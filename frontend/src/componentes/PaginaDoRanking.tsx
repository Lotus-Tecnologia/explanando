
import { ranquearTudo } from "@/lib/api";
import { formatarNumero } from "@/lib/formato";
import type { Ordenacao } from "@/lib/ordenacoes";
import type { Casa } from "@/lib/casas";
import { ExploradorDaCasa } from "./ExploradorDaCasa";

/**
 * O corpo das páginas de ranking da Câmara e do Senado.
 *
 * Título, o que a ordenação mede, o painel da casa e a lista.
 *
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
    <main className="mx-auto max-w-6xl px-4 py-6 sm:py-10">
      <h1 className="font-display text-[1.75rem] leading-tight font-bold tracking-tight text-titulo sm:text-4xl">
        {casa.nomeOficial}
      </h1>

      <p className="mt-3 max-w-3xl text-texto">
        Gastos da cota parlamentar, cruzados com o cadastro da Receita Federal. Os achados
        descrevem o que destoa, sem imputar conduta.{" "}
        {/* "Regra aplicada", e não "violada": o ADR-0004 descreve, não imputa. E o Senado não
            publica comprovante (ADR-0012) — prometê-lo ali era falso para a casa inteira. */}
        {ehCamara
          ? "Cada achado traz a regra aplicada e o comprovante oficial, quando a Câmara o publica."
          : "Cada achado traz a regra aplicada. O Senado não publica o comprovante das despesas."}
      </p>

      {/* A ordenação se escolhe no seletor do cabeçalho; aqui fica o que ela mede. */}
      <p className="mt-3 max-w-3xl rounded-cartao border border-borda-suave bg-surface p-4 text-texto-suave">
        <strong className="font-semibold text-texto">{ordenacao.rotulo}:</strong>{" "}
        {ordenacao.explicacao}
      </p>

      {itens === null ? (
        <div className="mt-6 rounded-cartao border border-borda-suave bg-surface p-5">
          <p className="font-semibold text-texto">O ranking não carregou agora</p>
          <p className="mt-1 text-texto-suave">
            A falha é de comunicação com a nossa API, não do dado público. A página se atualiza
            sozinha em poucos minutos.
          </p>
        </div>
      ) : (
        <>
          <ExploradorDaCasa casa={casa} itens={itens} base={base ?? undefined} ordenacao={ordenacao} />

          <p className="mt-8 max-w-3xl text-sm text-texto-suave">
            {/*
              Lista vazia não vira "0 aparecem, por causa do piso": na auditoria de 25/09 o
              /senado/gravidade serviu um cache velho, vazio, enquanto a API já devolvia 86
              senadores — e a frase atribuía o vazio às pessoas. Vazio aqui é sobre nós.
            */}
            {itens.length === 0 ? (
              <>
                Os dados desta ordenação não chegaram nesta atualização da página. Isso não é uma
                afirmação sobre os {casa.integrantes}: a página se atualiza em poucos minutos.
              </>
            ) : ordenacao.chave === "volume" ? (
              <>
                Todos os{" "}
                <strong className="numeros font-semibold text-texto">{formatarNumero(itens.length)}</strong>{" "}
                {casa.integrantes} da base estão neste ranking. A busca e os filtros olham o
                conjunto inteiro.
              </>
            ) : (
              <>
                <strong className="numeros font-semibold text-texto">{formatarNumero(itens.length)}</strong>{" "}
                {casa.integrantes} aparecem nesta ordenação. Quem tem menos de 100 despesas
                analisadas fica fora desta medida (ADR-0009), e{" "}
                <strong className="text-texto">ficar de fora não é atestado de ausência de achados.</strong>
              </>
            )}
          </p>
        </>
      )}
    </main>
  );
}
