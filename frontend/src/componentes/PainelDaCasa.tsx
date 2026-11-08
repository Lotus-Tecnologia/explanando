import type { ItemDoRanking } from "@/lib/contrato";
import type { Casa } from "@/lib/casas";
import { formatarDensidade, formatarNumero, formatarReais } from "@/lib/formato";

/**
 * O painel que abre o ranking de uma casa.
 *
 * Eram quatro cartões chapados com um número cada. O problema não era a moldura: é que
 * quatro totais lado a lado não dizem **nada sobre a forma** do que se está olhando. Saber
 * que há 21.078 indicadores não informa; saber que 3% deles são de peso alto, sim.
 *
 * Então o painel mostra duas coisas que um total não mostra:
 *
 * **A distribuição por peso**, numa barra. É a leitura mais importante do produto inteiro e
 * a mais difícil de passar em texto — o vermelho ocupa uma lasca da barra, e essa lasca diz
 * de uma vez o que um parágrafo tentaria explicar: achado grave é raro, e o resto é ruído
 * de baixa relevância que ninguém deveria ler como acusação.
 *
 * **A mediana**, porque "R$ 803 milhões" é grande demais para significar alguma coisa. O
 * gasto típico de uma pessoa é a escala que o leitor consegue segurar, e é comparável entre
 * as casas mesmo com as cotas sendo diferentes.
 *
 * <para>Mediana e não média: a média de um ranking é puxada pelos extremos e descreve
 * alguém que não existe. A mediana é uma pessoa real no meio da lista.</para>
 *
 * <para>Nenhum número aqui é telemetria de plataforma. As referências de painel abrem com
 * "Pending Audits 42" e "System Uptime 285", que servem a quem opera a ferramenta todo dia;
 * quem chega aqui veio de um link e quer saber sobre gente.</para>
 */
export function PainelDaCasa({
  casa,
  itens,
}: {
  casa: Casa;
  itens: readonly ItemDoRanking[];
}) {
  const volume = itens.reduce((soma, item) => soma + item.volumeTotalGasto, 0);
  const despesas = itens.reduce((soma, item) => soma + item.despesasAvaliadas, 0);

  const altas = itens.reduce((soma, item) => soma + item.flagsAltas, 0);
  const medias = itens.reduce((soma, item) => soma + item.flagsMedias, 0);
  const baixas = itens.reduce((soma, item) => soma + item.flagsBaixas, 0);
  const indicadores = altas + medias + baixas;

  const taxa = despesas === 0 ? null : (indicadores / despesas) * 100;

  /*
    Sobre VOLUME, e por isso independe de amostra — dá para calcular para todo mundo. Sobre
    indicadores excluiria quem está abaixo do piso amostral, e a estatística mentiria sobre
    o próprio denominador.
  */
  const ordenadosPorVolume = [...itens]
    .map((item) => item.volumeTotalGasto)
    .sort((a, b) => a - b);

  const mediana =
    ordenadosPorVolume.length === 0
      ? null
      : ordenadosPorVolume.length % 2 === 1
        ? ordenadosPorVolume[(ordenadosPorVolume.length - 1) / 2]
        : (ordenadosPorVolume[ordenadosPorVolume.length / 2 - 1]
            + ordenadosPorVolume[ordenadosPorVolume.length / 2]) / 2;

  return (
    <section
      aria-label={`Panorama — ${casa.nomeOficial}`}
      className="mt-6 overflow-hidden rounded-cartao border border-borda-suave bg-surface"
    >
      <div className="grid divide-y divide-borda-suave lg:grid-cols-5 lg:divide-x lg:divide-y-0">
        {/* ---- os dois números que respondem "o que é isto" ---- */}
        {/*
          Uma coluna no celular, duas no tablet, uma de novo no desktop — onde o painel já
          está dividido em cinco e estes dois ocupam duas.

          Em duas colunas num aparelho de 375px cada destaque fica com ~170px, e
          "R$ 121.019.164,39" em corpo 3xl era cortado no meio. Número de dinheiro truncado
          é pior que número pequeno: ele parece outro valor.
        */}
        <dl className="grid gap-px bg-borda-suave sm:grid-cols-2 lg:col-span-2 lg:grid-cols-1">
          <Destaque
            rotulo="Volume gasto"
            valor={formatarReais(volume)}
            nota={`${formatarNumero(itens.length)} ${casa.integrantes} · cota ${casa.cota}`}
          />
          <Destaque
            rotulo="Despesas analisadas"
            valor={formatarNumero(despesas)}
            nota={
              taxa === null
                ? "nenhuma despesa no período"
                : `${formatarDensidade(taxa)} indicadores por 100`
            }
          />
        </dl>

        {/* ---- a forma do que foi encontrado ---- */}
        <div className="p-5 lg:col-span-3">
          <h3 className="text-xs font-bold text-texto-suave">
            Indicadores por peso
          </h3>

          {indicadores === 0 ? (
            <p className="mt-3 text-sm text-texto-suave">
              Nenhum indicador nesta casa. Isso não é atestado de regularidade — parte das
              regras depende de dado que nem toda fonte publica.
            </p>
          ) : (
            <>
              <BarraDePesos altas={altas} medias={medias} baixas={baixas} />

              <dl className="mt-4 grid gap-x-6 gap-y-2 sm:grid-cols-3">
                <Peso
                  rotulo="Alta"
                  quantidade={altas}
                  total={indicadores}
                  corDaMarca="bg-alta-texto"
                  forma="octogono"
                />
                <Peso
                  rotulo="Média"
                  quantidade={medias}
                  total={indicadores}
                  corDaMarca="bg-media-texto"
                  forma="triangulo"
                />
                <Peso
                  rotulo="Baixa"
                  quantidade={baixas}
                  total={indicadores}
                  corDaMarca="bg-baixa-texto"
                  forma="circulo"
                />
              </dl>
            </>
          )}
        </div>
      </div>

      {mediana !== null ? (
        <div className="border-t border-borda-suave bg-surface px-5 py-3">
          <p className="text-sm text-texto-suave">
            Metade dos {casa.integrantes} gastou mais de{" "}
            <strong className="numeros font-semibold text-titulo">
              {formatarReais(mediana)}
            </strong>{" "}
            no período, e metade gastou menos.
          </p>
        </div>
      ) : null}
    </section>
  );
}

function Destaque({
  rotulo,
  valor,
  nota,
}: {
  rotulo: string;
  valor: string;
  nota: string;
}) {
  return (
    <div className="bg-fundo px-5 py-4">
      <dt className="text-xs font-bold text-texto-suave">
        {rotulo}
      </dt>
      <dd className="numeros mt-1 font-display text-2xl font-bold tracking-tight text-titulo sm:text-3xl">
        {valor}
      </dd>
      <p className="mt-0.5 text-xs text-texto-suave">{nota}</p>
    </div>
  );
}

/**
 * A barra empilhada dos três pesos.
 *
 * **A lasca vermelha é o conteúdo.** Numa base real ela ocupa cerca de 3% da largura, e é
 * exatamente isso que precisa ser visto: o ADR-0013 reserva o vermelho aos achados de peso
 * alto justamente porque eles são raros, e uma barra mostra essa raridade de uma vez.
 *
 * A ordem é alta, média, baixa — o que pesa mais primeiro, como se lê a tabela.
 *
 * Cada faixa recebe `title`, e os números aparecem escritos na legenda abaixo: a barra
 * sozinha não informa quantidade, e cor sozinha falha para quem não a distingue.
 */
function BarraDePesos({
  altas,
  medias,
  baixas,
}: {
  altas: number;
  medias: number;
  baixas: number;
}) {
  const total = altas + medias + baixas;

  const faixas = [
    { rotulo: "Alta", valor: altas, classe: "bg-alta-texto" },
    { rotulo: "Média", valor: medias, classe: "bg-media-texto" },
    { rotulo: "Baixa", valor: baixas, classe: "bg-baixa-texto" },
  ].filter((faixa) => faixa.valor > 0);

  return (
    <div
      /*
        `gap` entre as faixas, e não faixas encostadas: os três tons do ADR-0013 são
        escuros, e o vermelho do peso alto colado no marrom do médio se lê como uma mancha
        só. O respiro de 2px é o que devolve a fronteira — sem ele a barra vira decoração
        em vez de leitura.
      */
      className="mt-3 flex h-3.5 gap-0.5 rounded-selo"
      role="img"
      aria-label={`${formatarNumero(altas)} de peso alto, ${formatarNumero(
        medias,
      )} de peso médio e ${formatarNumero(baixas)} de peso baixo.`}
    >
      {faixas.map((faixa) => (
        <span
          key={faixa.rotulo}
          className={`${faixa.classe} first:rounded-l-selo last:rounded-r-selo`}
          style={{ width: `calc(${(faixa.valor / total) * 100}% - 2px)`, minWidth: "3px" }}
          title={`${faixa.rotulo}: ${formatarNumero(faixa.valor)}`}
        />
      ))}
    </div>
  );
}

/**
 * Uma linha da legenda: forma, cor, rótulo e número.
 *
 * As três marcas juntas — cor **e** forma **e** rótulo — são a exigência do ADR-0013. Matiz
 * sozinho falha para cerca de 8% dos homens, e severidade é o atributo que menos pode se
 * perder.
 */
function Peso({
  rotulo,
  quantidade,
  total,
  corDaMarca,
  forma,
}: {
  rotulo: string;
  quantidade: number;
  total: number;
  corDaMarca: string;
  forma: "octogono" | "triangulo" | "circulo";
}) {
  const recorte =
    forma === "octogono"
      ? "[clip-path:polygon(30%_0,70%_0,100%_30%,100%_70%,70%_100%,30%_100%,0_70%,0_30%)]"
      : forma === "triangulo"
        ? "[clip-path:polygon(50%_0,100%_100%,0_100%)]"
        : "rounded-full";

  return (
    <div className="flex items-baseline gap-2">
      <span
        aria-hidden="true"
        className={`mt-1 size-2.5 shrink-0 self-start ${corDaMarca} ${recorte}`}
      />

      <div className="min-w-0">
        <dt className="text-xs text-texto-suave">{rotulo}</dt>
        <dd className="numeros text-sm font-semibold text-texto">
          {formatarNumero(quantidade)}
          <span className="ml-1.5 text-xs font-normal text-texto-suave">
            {formatarDensidade((quantidade / total) * 100)}%
          </span>
        </dd>
      </div>
    </div>
  );
}
