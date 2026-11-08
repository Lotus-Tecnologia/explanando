import type { PontoDaSerie } from "@/lib/contrato";
import { formatarCompetencia, formatarCompetenciaCurta, formatarNumero } from "@/lib/formato";

/**
 * A evolução mensal do quadro de pessoal.
 *
 * **Uma série só, e por isso nenhuma legenda**: o título já nomeia o que está desenhado.
 * A cor é o acento do tema, que o ADR-0013 já mediu contra os dois fundos — nada de paleta
 * categórica aqui, porque não há categorias a distinguir.
 *
 * **Sem JavaScript, como o resto desta página.** A camada de leitura por ponto é o
 * `<title>` nativo do SVG, que o navegador mostra ao passar o mouse sem custar um byte de
 * script; e nenhum valor depende dela — a tabela embaixo traz a série inteira, aberta por
 * um `<details>` que também é nativo. Tooltip que é a única forma de ler um número é
 * defeito de acessibilidade, não recurso.
 */
export function SerieDePessoal({
  serie,
  titulo,
  explicacao,
}: {
  serie: readonly PontoDaSerie[];
  titulo: string;
  explicacao: string;
}) {
  // Com menos de dois pontos não há evolução para desenhar — e um ponto só num gráfico de
  // linha é um gráfico que afirma uma tendência que não existe.
  if (serie.length < 2) return null;

  const valores = serie.map((ponto) => ponto.vinculos);
  const menor = Math.min(...valores);
  const maior = Math.max(...valores);

  const primeiro = serie[0];
  const ultimo = serie[serie.length - 1];
  const variacao = ultimo.vinculos - primeiro.vinculos;

  // A escala começa no menor valor, não em zero. É deliberado e está dito na legenda: a
  // variação do quadro federal é de ~1% ao ano, e uma escala desde zero desenharia uma
  // reta perfeitamente horizontal — verdadeira e inútil. O eixo rotulado nas duas pontas
  // é o que impede a leitura exagerada.
  const largura = 600;
  const altura = 120;
  const folga = 8;
  const amplitude = maior - menor || 1;

  /*
    O eixo horizontal é TEMPO, não posição na lista. Com espaçamento por índice, um buraco
    de nove meses na série — e há um: a origem parou de entregar no meio de uma carga —
    apareceria com a mesma largura de um mês, e a linha afirmaria uma evolução mensal que
    não foi medida. Posicionar pela data faz o vão aparecer como vão.
  */
  const emMeses = (competencia: string) => {
    const [ano, mes] = competencia.split("-").map(Number);
    return ano * 12 + mes;
  };

  const inicio = emMeses(serie[0].competencia);
  const vao = emMeses(serie[serie.length - 1].competencia) - inicio || 1;

  const x = (competencia: string) => ((emMeses(competencia) - inicio) / vao) * largura;
  const y = (valor: number) =>
    altura - folga - ((valor - menor) / amplitude) * (altura - folga * 2);

  /*
    A série é quebrada em TRECHOS CONTÍNUOS, e o vão entre eles não recebe linha cheia.

    É a correção de uma mentira silenciosa: a série real tem um buraco de nove meses — a
    origem passou a recusar downloads no meio de uma carga —, e um traço sólido atravessando
    esse buraco afirma uma trajetória mensal que não foi medida. O vão vira um pontilhado
    fraco, que liga os pontos sem dizer por onde a linha passou.
  */
  const emSequencia = (anterior: PontoDaSerie, atual: PontoDaSerie) =>
    emMeses(atual.competencia) - emMeses(anterior.competencia) === 1;

  const trechos: PontoDaSerie[][] = [[serie[0]]];

  for (let i = 1; i < serie.length; i++) {
    if (emSequencia(serie[i - 1], serie[i])) {
      trechos[trechos.length - 1].push(serie[i]);
    } else {
      trechos.push([serie[i]]);
    }
  }

  const coordenadas = (ponto: PontoDaSerie) =>
    `${x(ponto.competencia).toFixed(1)},${y(ponto.vinculos).toFixed(1)}`;

  const temVao = trechos.length > 1;

  return (
    <figure className="m-0">
      <figcaption className="mb-3">
        <h3 className="font-display text-base font-bold text-titulo">{titulo}</h3>
        <p className="mt-1 text-sm text-texto-suave">{explicacao}</p>
      </figcaption>

      <p className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        {/*
          Figuras proporcionais, e nao tabulares: digito de largura fixa faz um numero
          grande e solto parecer frouxo. `tabular-nums` so' onde numeros se alinham em
          coluna — e' o caso da tabela abaixo, nao o deste.
        */}
        <span className="font-display text-3xl font-bold text-titulo">
          {formatarNumero(ultimo.vinculos)}
        </span>

        <span className="text-sm text-texto-suave">
          em {formatarCompetencia(ultimo.competencia)}
          {variacao !== 0 ? (
            <>
              {" · "}
              <strong className="font-semibold text-texto">
                {variacao > 0 ? "+" : "−"}
                {formatarNumero(Math.abs(variacao))}
              </strong>{" "}
              desde {formatarCompetencia(primeiro.competencia)}
            </>
          ) : null}
        </span>
      </p>

      <svg
        viewBox={`0 0 ${largura} ${altura}`}
        /*
          Escala UNIFORME. Com `preserveAspectRatio="none"` a linha esticaria bem, mas os
          pontos virariam elipses — e o marcador da ponta e' justamente o elemento que
          precisa parecer um ponto.
        */
        className="mt-3 block h-auto w-full"
        role="img"
        aria-label={
          `Evolução mensal: ${formatarNumero(primeiro.vinculos)} vínculos em ` +
          `${formatarCompetencia(primeiro.competencia)} e ${formatarNumero(ultimo.vinculos)} em ` +
          `${formatarCompetencia(ultimo.competencia)}.` +
          (temVao ? " A série tem meses não coletados, marcados com pontilhado." : "") +
          " A série completa está na tabela abaixo."
        }
      >
        {trechos.map((trecho) => {
          const pontos = trecho.map(coordenadas);
          const primeiroX = x(trecho[0].competencia).toFixed(1);
          const ultimoX = x(trecho[trecho.length - 1].competencia).toFixed(1);

          return (
            <g key={trecho[0].competencia}>
              {/* Um trecho de um ponto so' nao tem area nem linha: e' so' o marcador. */}
              {trecho.length > 1 ? (
                <>
                  <path
                    d={
                      `M ${pontos[0]} L ${pontos.slice(1).join(" L ")} ` +
                      `L ${ultimoX},${altura} L ${primeiroX},${altura} Z`
                    }
                    fill="var(--color-acento)"
                    opacity={0.12}
                  />

                  <polyline
                    points={pontos.join(" ")}
                    fill="none"
                    stroke="var(--color-acento)"
                    strokeWidth={2}
                    strokeLinejoin="round"
                    strokeLinecap="round"
                    vectorEffect="non-scaling-stroke"
                  />
                </>
              ) : null}
            </g>
          );
        })}

        {/* Os vaos: pontilhado fraco, que liga sem afirmar o caminho. */}
        {trechos.slice(1).map((trecho, i) => (
          <line
            key={`vao-${trecho[0].competencia}`}
            x1={x(trechos[i][trechos[i].length - 1].competencia)}
            y1={y(trechos[i][trechos[i].length - 1].vinculos)}
            x2={x(trecho[0].competencia)}
            y2={y(trecho[0].vinculos)}
            stroke="var(--color-acento)"
            strokeWidth={2}
            strokeDasharray="2 6"
            strokeLinecap="round"
            opacity={0.45}
            vectorEffect="non-scaling-stroke"
          />
        ))}

        {serie.map((ponto) => (
          <circle
            key={ponto.competencia}
            cx={x(ponto.competencia)}
            cy={y(ponto.vinculos)}
            /*
              O alvo do ponteiro e' bem maior que o ponto visivel: o raio de 10 no sistema
              do viewBox da' uma area confortavel, e `fill-transparent` a deixa invisivel.
              Ponto de 3px que exige mira e' o defeito que isto evita.
            */
            r={12}
            className="fill-transparent"
          >
            <title>
              {`${formatarCompetencia(ponto.competencia)}: ${formatarNumero(ponto.vinculos)} vínculos, ` +
                `${formatarNumero(ponto.comFuncaoDeConfianca)} em função de confiança`}
            </title>
          </circle>
        ))}

        <circle
          cx={x(ultimo.competencia)}
          cy={y(ultimo.vinculos)}
          r={4}
          fill="var(--color-acento)"
          stroke="var(--color-fundo)"
          strokeWidth={2}
        />
      </svg>

      <p className="mt-2 flex justify-between text-xs text-texto-suave">
        <span>{formatarCompetenciaCurta(primeiro.competencia)}</span>
        <span>
          {/*
            A escala comprimida dita em voz alta. Sem isto, uma variacao de 1% desenhada
            entre o minimo e o maximo parece um salto — e o leitor conclui algo que o dado
            nao diz.
          */}
          escala entre {formatarNumero(menor)} e {formatarNumero(maior)}
          {temVao ? " · pontilhado = meses não coletados" : ""}
        </span>
        <span>{formatarCompetenciaCurta(ultimo.competencia)}</span>
      </p>

      <details className="mt-3">
        <summary className="flex min-h-11 cursor-pointer items-center text-sm font-semibold text-titulo">
          Ver a série em números
        </summary>

        <div className="mt-2 overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-borda-estrutural text-left">
                <th scope="col" className="py-1.5 pe-3 font-semibold text-texto">
                  Mês
                </th>
                <th scope="col" className="py-1.5 pe-3 text-right font-semibold text-texto">
                  Vínculos
                </th>
                <th scope="col" className="py-1.5 text-right font-semibold text-texto">
                  Em função de confiança
                </th>
              </tr>
            </thead>
            <tbody>
              {serie.map((ponto) => (
                <tr key={ponto.competencia} className="border-b border-borda-suave">
                  <td className="py-1.5 pe-3 text-texto">{formatarCompetenciaCurta(ponto.competencia)}</td>
                  {/* Aqui SIM tabular: sao colunas de numeros que precisam alinhar. */}
                  <td className="numeros py-1.5 pe-3 text-right text-texto">
                    {formatarNumero(ponto.vinculos)}
                  </td>
                  <td className="numeros py-1.5 text-right text-texto-suave">
                    {formatarNumero(ponto.comFuncaoDeConfianca)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </figure>
  );
}
