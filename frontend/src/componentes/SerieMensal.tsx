import Link from "next/link";
import { formatarNumero, formatarReais } from "@/lib/formato";
import type { MesDoParlamentar } from "@/lib/contrato";

/**
 * A curva de gasto mês a mês.
 *
 * SVG escrito à mão, sem biblioteca de gráfico: são barras verticais sobre uma escala linear,
 * e um pacote de 40 kB para desenhar retângulos custaria mais no celular — que é o aparelho
 * da maior parte do tráfego vindo de rede social — do que entrega.
 *
 * Três decisões que não são estéticas:
 *
 * - **A tabela existe de verdade**, e não é enfeite de acessibilidade. Um gráfico sozinho não
 *   existe para leitor de tela, e num produto de utilidade pública o número é o conteúdo.
 *   O SVG é `aria-hidden`; a tabela é a fonte.
 * - **A glosa some por inteiro quando é desconhecida** (ADR-0012 §2). O Senado não a publica,
 *   e `R$ 0,00` afirmaria que houve conferência e nada foi cortado.
 * - **A barra usa `--color-acento`**, o verde que reprova para texto (3,77) e serve para
 *   elemento não textual. É exatamente o uso que o ADR-0013 reservou para ele.
 */

const ALTURA = 120;
const LARGURA_DA_BARRA = 14;
const ESPACO = 4;

export function SerieMensal({
  serie,
  idDoParlamentar,
}: {
  serie: MesDoParlamentar[];
  idDoParlamentar: string;
}) {
  if (serie.length === 0) {
    return null;
  }

  const glosaConhecida = serie.some((mes) => mes.totalGlosa !== null);

  // O teto da escala considera só valores positivos: um mês fechado em negativo — mais
  // estorno que gasto — existe na base real e não pode inverter o desenho.
  const teto = Math.max(...serie.map((mes) => Math.max(mes.totalLiquido, 0)), 1);
  const largura = serie.length * (LARGURA_DA_BARRA + ESPACO);

  const rotulo = (mes: MesDoParlamentar) =>
    `${String(mes.mes).padStart(2, "0")}/${mes.ano}`;

  return (
    <section aria-labelledby="titulo-serie" className="border border-borda-suave p-4">
      <h2 id="titulo-serie" className="text-base font-bold text-titulo">
        Gasto mês a mês
      </h2>

      <div className="mt-3 overflow-x-auto">
        <svg
          aria-hidden="true"
          role="presentation"
          width={largura}
          height={ALTURA}
          viewBox={`0 0 ${largura} ${ALTURA}`}
          className="block"
        >
          {serie.map((mes, indice) => {
            const proporcao = Math.max(mes.totalLiquido, 0) / teto;
            const altura = Math.max(proporcao * (ALTURA - 2), 1);

            return (
              <rect
                key={rotulo(mes)}
                x={indice * (LARGURA_DA_BARRA + ESPACO)}
                y={ALTURA - altura}
                width={LARGURA_DA_BARRA}
                height={altura}
                className="fill-acento"
              />
            );
          })}
        </svg>
      </div>

      <div className="mt-3 max-h-72 overflow-y-auto border border-borda-suave">
        <table className="w-full border-collapse">
          <caption className="sr-only">
            Gasto líquido por competência, com a quantidade de despesas de cada mês
          </caption>
          <thead>
            <tr>
              <th
                scope="col"
                className="sticky top-0 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-left text-xs font-bold text-titulo"
              >
                Competência
              </th>
              <th
                scope="col"
                className="sticky top-0 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-right text-xs font-bold text-titulo"
              >
                Gasto líquido
              </th>
              {glosaConhecida ? (
                <th
                  scope="col"
                  className="sticky top-0 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-right text-xs font-bold text-titulo"
                >
                  Glosa
                </th>
              ) : null}
              <th
                scope="col"
                className="sticky top-0 border-b-2 border-borda-estrutural bg-fundo px-3 py-2 text-right text-xs font-bold text-titulo"
              >
                Despesas
              </th>
            </tr>
          </thead>
          <tbody>
            {serie.map((mes) => (
              <tr key={rotulo(mes)} className="odd:bg-fundo even:bg-surface">
                <td className="numeros px-3 py-1.5 text-sm">
                  {mes.detalheDisponivel ? (
                    <Link
                      href={`/parlamentares/${idDoParlamentar}/despesas?ano=${mes.ano}`}
                      className="text-titulo underline-offset-2 hover:underline"
                    >
                      {rotulo(mes)}
                    </Link>
                  ) : (
                    <>
                      {rotulo(mes)}
                      {/*
                        Depois da poda o total do mês sobrevive, mas as notas não. Oferecer
                        o link mandaria o leitor para uma lista vazia, que ele leria como
                        "não houve despesa" — quando houve, e o total ao lado prova.
                      */}
                      <span className="ml-2 text-xs text-texto-suave">
                        detalhe não disponível
                      </span>
                    </>
                  )}
                </td>

                <td className="numeros px-3 py-1.5 text-right text-sm font-semibold text-titulo">
                  {formatarReais(mes.totalLiquido)}
                </td>

                {glosaConhecida ? (
                  <td className="numeros px-3 py-1.5 text-right text-sm text-texto-suave">
                    {mes.totalGlosa === null ? "—" : formatarReais(mes.totalGlosa)}
                  </td>
                ) : null}

                <td className="numeros px-3 py-1.5 text-right text-sm text-texto-suave">
                  {formatarNumero(mes.quantidadeDeDespesas)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
