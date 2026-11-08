import type { Metadata } from "next";
import Link from "next/link";
import { ranquearTudo } from "@/lib/api";
import { CASAS, type Casa } from "@/lib/casas";
import { formatarNumero, formatarPorcentagem, formatarReais } from "@/lib/formato";
import { distribuicaoDeGravidade } from "@/lib/gravidade";
import { ORDENACOES, caminhoDoRanking } from "@/lib/ordenacoes";
import type { ItemDoRanking } from "@/lib/contrato";
import { BuscaRapidaHome } from "@/componentes/BuscaRapidaHome";
import { TagDeGravidade } from "@/componentes/TagDeGravidade";
import { TagDeAbstencao } from "@/componentes/Abstencao";
import { CartaoDePessoa } from "@/componentes/CartaoDePessoa";

/* Ver a nota em camara/page.tsx: precisa ser literal. */
export const revalidate = 300;

export const metadata: Metadata = {
  title: "Explanando — gastos da cota parlamentar",
  description:
    "Quanto deputados e senadores consumiram da cota parlamentar, e os indicadores de um "
    + "motor de regras determinístico. Descreve inconsistências; não imputa conduta.",
  alternates: {
    canonical: "/",
    types: { "application/rss+xml": "/feed.xml" },
  },
};

/**
 * A porta de entrada. Direção C: um assunto por cartão, cada número com a frase que o
 * explica, e as duas casas sempre lado a lado e do mesmo tamanho.
 */
export default async function Home() {
  const casas = await Promise.all(
    CASAS.map(async (casa) => ({ casa, itens: await ranquearTudo(casa, "volume") })),
  );

  const tudo = casas.flatMap(({ itens }) => itens ?? []);

  const volume = tudo.reduce((soma, item) => soma + item.volumeTotalGasto, 0);
  const despesas = tudo.reduce((soma, item) => soma + item.despesasAvaliadas, 0);

  // A parte de cada gravidade vem das duas casas, e não de um número escrito à mão: o
  // 43,3/53,4/3,3 fixo não batia com a base (41,9/55,1/3,0 na auditoria de 25/09).
  const distribuicao = distribuicaoDeGravidade(casas.map(({ itens }) => itens));
  const parte = (fracao: number | undefined) =>
    fracao === undefined ? "sem dado" : `${formatarPorcentagem(fracao)} dos achados`;

  return (
    <main className="mx-auto max-w-6xl px-4 pt-8 pb-4 sm:pt-12">
      <h1 className="max-w-3xl font-display text-[2rem] leading-[1.1] font-bold tracking-tight text-balance text-titulo sm:text-5xl">
        Para onde vai a cota dos seus parlamentares
      </h1>

      <p className="mt-4 max-w-2xl text-base text-texto sm:text-lg">
        Cada despesa de deputados e senadores passa por um motor de regras que cruza o gasto com
        o cadastro da Receita Federal. Ele descreve o que destoa, sem imputar conduta: a
        conclusão é de quem lê.
      </p>

      <div className="mt-6 max-w-2xl">
        <label htmlFor="busca-rapida" className="mb-2 block font-semibold text-texto">
          Procure um parlamentar
        </label>
        <BuscaRapidaHome parlamentares={tudo} idDoCampo="busca-rapida" />
      </div>

      {/*
        Sem número de reserva. Quando a API não responde, o cartão diz que não respondeu: um
        número escrito à mão seria o produto publicando um dado que não mediu.
      */}
      <section
        aria-labelledby="titulo-base"
        className="mt-10 grid gap-4 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)]"
      >
        <div className="grid content-start gap-4">
          <div className="rounded-cartao border border-borda-suave bg-surface p-5">
            <h2 id="titulo-base" className="font-semibold text-texto-suave">
              Gasto da cota na base
            </h2>
            {/* Número não quebra: "R$ 802.407.895,7" numa linha e "8" na outra é outro valor. */}
            <p className="numeros mt-1 font-display text-[1.875rem] leading-tight font-bold whitespace-nowrap text-titulo sm:text-[2.125rem]">
              {volume > 0 ? formatarReais(volume) : "Indisponível agora"}
            </p>
            {despesas > 0 ? (
              <p className="mt-2 text-texto-suave">
                {casasComDado(casas)}: {formatarNumero(tudo.length)} parlamentares e{" "}
                {formatarNumero(despesas)} despesas lidas uma a uma.
              </p>
            ) : null}
          </div>

          {/* Paridade: as duas casas lado a lado e do mesmo tamanho (ADR-0012). */}
          <div className="grid grid-cols-2 gap-3">
            {casas.map(({ casa, itens }) => (
              <Link
                key={casa.chave}
                href={casa.caminho}
                className="flex min-h-24 flex-col justify-end rounded-cartao bg-titulo p-4 text-sobre-titulo hover:bg-titulo-hover"
              >
                <span className="font-display text-xl font-bold">{casa.rotulo}</span>
                <span className="numeros text-sm">
                  {itens && itens.length > 0
                    ? `${formatarNumero(itens.length)} parlamentares`
                    : "Sem dado agora"}
                </span>
              </Link>
            ))}
          </div>
        </div>

        <div className="grid content-start gap-6 sm:grid-cols-2 lg:grid-cols-1">
          {casas.map(({ casa, itens }) => (
            <MaioresDaCasa key={casa.chave} casa={casa} itens={itens} />
          ))}
        </div>
      </section>

      <section aria-labelledby="titulo-matriz" className="mt-14">
        <h2
          id="titulo-matriz"
          className="font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl"
        >
          Como ler os achados
        </h2>
        <p className="mt-2 max-w-2xl text-texto-suave">
          Cada achado tem um peso, mostrado por cor, forma e nome. O vermelho fica só com a
          gravidade alta, a menor parte da base.
        </p>

        <ul className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <ItemDaMatriz
            tag={<TagDeGravidade gravidade="Baixa" />}
            parte={parte(distribuicao?.baixa)}
            texto="Desvio pequeno, com documentação regular. Aparece para registro."
          />
          <ItemDaMatriz
            tag={<TagDeGravidade gravidade="Media" />}
            parte={parte(distribuicao?.media)}
            texto="Algo atípico no gasto, que vale conferir no documento."
          />
          <ItemDaMatriz
            tag={<TagDeGravidade gravidade="Alta" />}
            parte={parte(distribuicao?.alta)}
            texto="O que mais destoa das regras da cota ou de limites legais."
          />
          <ItemDaMatriz
            tag={<TagDeAbstencao />}
            parte="sem dado"
            texto="A fonte não publicou o dado, então a regra não teve como olhar. Não é o mesmo que não ter achado."
          />
        </ul>
      </section>

      <section aria-labelledby="titulo-nao-e" className="mt-14 grid gap-6 lg:grid-cols-2">
        <div>
          <h2
            id="titulo-nao-e"
            className="font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl"
          >
            O que um indicador não é
          </h2>
          <p className="mt-3 text-texto">
            Um indicador aponta que algum dado destoa do esperado: um ramo de atividade que não
            combina com a despesa, um valor repetido ao centavo por meses, uma empresa aberta
            pouco antes do primeiro pagamento.{" "}
            <strong>Nada disso é, por si, irregularidade.</strong> Gastar a cota é prerrogativa
            do mandato, e a explicação pode ser legítima.
          </p>
          <p className="mt-3 text-texto">
            Por isso cada achado vem com a regra que o produziu e, quando a fonte publica, com o
            comprovante oficial.
          </p>
        </div>

        <div className="rounded-cartao border border-borda-suave bg-surface p-5">
          <h2 className="font-semibold text-texto-suave">De onde vem o dado</h2>
          {/* Lista, não sequência: as três coisas não têm ordem, então não ganham número. */}
          <ul className="mt-3 space-y-3 text-texto">
            <li>
              <strong>Câmara, Senado, Receita Federal e TSE.</strong> Tudo chega das bases
              abertas oficiais; nada é digitado à mão.
            </li>
            <li>
              <strong>Regras fixas.</strong> Atividade econômica, quadro de sócios, doação de
              campanha, datas e valores repetidos, sempre do mesmo jeito para todos.
            </li>
            <li>
              <strong>Sem dado, sem suspeita.</strong> Quando a fonte não traz o que a regra
              precisa, a regra se cala e a página diz que se calou.
            </li>
          </ul>
          <p className="mt-4 flex flex-wrap gap-x-5 gap-y-1">
            <Link href="/estado" className={LINK}>
              Conheça o mapa do Estado
            </Link>
            <Link href="/privacidade" className={LINK}>
              Privacidade e LGPD
            </Link>
          </p>
        </div>
      </section>
    </main>
  );
}

const LINK =
  "inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4 hover:decoration-titulo";

/**
 * Quais casas estão por trás do número. "Câmara e Senado" fixo mentia quando só uma
 * respondeu — a base local, por exemplo, tem só a Câmara.
 */
function casasComDado(casas: { casa: Casa; itens: ItemDoRanking[] | null }[]): string {
  const comDado = casas
    .filter(({ itens }) => itens && itens.length > 0)
    .map(({ casa }) => casa.rotulo);

  return comDado.length === casas.length ? "Câmara e Senado somados" : comDado.join(" e ");
}

function MaioresDaCasa({ casa, itens }: { casa: Casa; itens: ItemDoRanking[] | null }) {
  const primeiros = itens?.slice(0, 3) ?? [];
  const maior = primeiros[0]?.volumeTotalGasto ?? 0;

  return (
    <section aria-labelledby={`maiores-${casa.chave}`}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id={`maiores-${casa.chave}`} className="font-semibold text-texto">
          Quem mais gastou {casa.chave === "camara" ? "na Câmara" : "no Senado"}
        </h2>
        <Link href={caminhoDoRanking(casa, ORDENACOES[0])} className={LINK}>
          Ver todos
        </Link>
      </div>

      {itens === null ? (
        <p className="mt-2 rounded-cartao border border-borda-suave bg-surface p-4 text-texto-suave">
          Os números desta casa não puderam ser carregados agora. A falha é de comunicação com a
          nossa API, não do dado público.
        </p>
      ) : itens.length === 0 ? (
        <p className="mt-2 rounded-cartao border border-dashed border-borda-estrutural bg-surface p-4 text-texto-suave">
          A base desta casa ainda está sendo carregada.
        </p>
      ) : (
        <ol className="mt-2 grid gap-2">
          {primeiros.map((item, indice) => (
            <li key={item.id}>
              <CartaoDePessoa
                item={item}
                posicao={indice + 1}
                fracaoDaBarra={maior > 0 ? item.volumeTotalGasto / maior : 0}
              />
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function ItemDaMatriz({
  tag,
  parte,
  texto,
}: {
  tag: React.ReactNode;
  parte: string;
  texto: string;
}) {
  return (
    <li className="rounded-cartao border border-borda-suave bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        {tag}
        <span className="numeros text-sm text-texto-suave">{parte}</span>
      </div>
      <p className="mt-3 text-texto">{texto}</p>
    </li>
  );
}
