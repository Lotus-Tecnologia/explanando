import Link from "next/link";
import { ranquear } from "@/lib/api";
import { formatarReais } from "@/componentes/Comprovante";
import type { ItemDoRanking, OrdenacaoDoRanking } from "@/lib/contrato";

export const revalidate = 300;

const ORDENACOES: { chave: OrdenacaoDoRanking; rotulo: string; explicacao: string }[] = [
  {
    chave: "volume",
    rotulo: "Volume gasto",
    explicacao:
      "Soma em reais, com estornos abatidos. Inclui todos os parlamentares — gasto não depende de amostra.",
  },
  {
    chave: "indice",
    rotulo: "Densidade de indicadores",
    explicacao:
      "Indicadores por 100 despesas. Exclui quem tem menos de 100 despesas analisadas, onde a densidade seria ruído.",
  },
  {
    chave: "gravidade",
    rotulo: "Indicadores de alta relevância",
    explicacao:
      "Ordena apenas pelos achados de maior peso. É a fila de triagem — pelo total, quem tem muitos achados graves fica abaixo de quem tem zero.",
  },
];

function Cabecalho({ children }: { children: React.ReactNode }) {
  return (
    <th
      scope="col"
      className="sticky top-0 z-10 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-left text-xs font-bold text-titulo"
    >
      {children}
    </th>
  );
}

function Linha({ item, posicao }: { item: ItemDoRanking; posicao: number }) {
  return (
    <tr className="odd:bg-fundo even:bg-surface">
      <td className="numeros px-3 py-2 text-sm text-texto-suave">{posicao}</td>

      <td className="px-3 py-2 text-sm">
        <Link
          href={`/parlamentares/${item.id}`}
          className="font-semibold text-titulo underline-offset-2 hover:underline"
        >
          {item.nomeParlamentar}
        </Link>
        <span className="ml-2 text-xs text-texto-suave">
          {item.siglaPartido} · {item.uf}
        </span>
      </td>

      <td className="numeros px-3 py-2 text-right text-sm font-semibold text-titulo">
        {formatarReais(item.volumeTotalGasto)}
      </td>

      <td className="numeros px-3 py-2 text-right text-sm">
        {/*
          Nulo e zero são coisas diferentes. Abaixo do piso amostral a densidade não foi
          medida, e escrever "0,00" afirmaria que foi.
        */}
        {item.indiceDeRisco === null ? (
          <span
            className="text-xs text-texto-suave"
            title="Menos de 100 despesas analisadas: a densidade não é publicável."
          >
            amostra insuficiente
          </span>
        ) : (
          item.indiceDeRisco.toFixed(2)
        )}
      </td>

      <td className="numeros px-3 py-2 text-right text-sm">
        <span className="text-alta-texto">{item.flagsAltas}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-media-texto">{item.flagsMedias}</span>
        <span className="text-texto-suave"> · </span>
        <span className="text-texto-suave">{item.flagsBaixas}</span>
      </td>

      <td className="numeros px-3 py-2 text-right text-sm text-texto-suave">
        {item.despesasAvaliadas.toLocaleString("pt-BR")}
      </td>
    </tr>
  );
}

export default async function Home({
  searchParams,
}: {
  searchParams: Promise<{ ordenar?: string }>;
}) {
  const { ordenar } = await searchParams;

  const escolhida =
    ORDENACOES.find((o) => o.chave === ordenar) ?? ORDENACOES[0]!;

  const pagina = await ranquear(escolhida.chave, 1, 50);

  return (
    <main className="mx-auto max-w-7xl px-4 py-8">
      <h1 className="text-2xl font-bold text-titulo">
        Gastos da cota parlamentar
      </h1>

      <p className="mt-2 max-w-3xl text-sm">
        <strong className="numeros">{pagina.total.toLocaleString("pt-BR")}</strong>{" "}
        parlamentares, com dados públicos da Câmara dos Deputados. Os indicadores vêm de um
        motor de regras determinístico e{" "}
        <strong>descrevem inconsistências, não imputam conduta.</strong>
      </p>

      <nav aria-label="Ordenação do ranking" className="mt-6">
        <ul className="flex flex-wrap gap-2">
          {ORDENACOES.map((opcao) => {
            const ativa = opcao.chave === escolhida.chave;

            return (
              <li key={opcao.chave}>
                <Link
                  href={opcao.chave === "volume" ? "/" : `/?ordenar=${opcao.chave}`}
                  aria-current={ativa ? "true" : undefined}
                  className={
                    ativa
                      ? "inline-block bg-titulo px-3 py-1.5 text-sm font-semibold text-white"
                      : "inline-block border border-borda-suave px-3 py-1.5 text-sm font-semibold text-titulo hover:bg-surface"
                  }
                >
                  {opcao.rotulo}
                </Link>
              </li>
            );
          })}
        </ul>

        <p className="mt-2 max-w-3xl text-xs text-texto-suave">{escolhida.explicacao}</p>
      </nav>

      <div className="mt-4 overflow-x-auto border border-borda-suave">
        <table className="w-full border-collapse">
          <caption className="sr-only">
            Ranking de parlamentares por {escolhida.rotulo.toLowerCase()}
          </caption>
          <thead>
            <tr>
              <Cabecalho>#</Cabecalho>
              <Cabecalho>Parlamentar</Cabecalho>
              <Cabecalho>Volume gasto</Cabecalho>
              <Cabecalho>Densidade</Cabecalho>
              <Cabecalho>Alta · Média · Baixa</Cabecalho>
              <Cabecalho>Despesas</Cabecalho>
            </tr>
          </thead>
          <tbody>
            {pagina.itens.map((item, indice) => (
              <Linha key={item.id} item={item} posicao={indice + 1} />
            ))}
          </tbody>
        </table>
      </div>
    </main>
  );
}
