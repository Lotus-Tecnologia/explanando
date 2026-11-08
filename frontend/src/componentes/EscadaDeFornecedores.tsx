import type { PagamentosPorFornecedor } from "@/lib/contrato";
import { fraseDosMaioresFornecedores } from "@/lib/frases";
import { formatarMoedaCompacta, formatarNumero, formatarReais } from "@/lib/formato";

/**
 * Quem mais recebeu, ano a ano — a Fase 4 do plano de execução.
 *
 * A escada "2023 — 50 mil · 2024 — 200 mil · 2025 — 800 mil", por fornecedor. Duas travas
 * vêm prontas da API e esta tela só as torna visíveis:
 *
 * - **ano parcial** — o que não tem os doze meses com despesa — aparece marcado e em tom
 *   mais baixo, e não entra em variação;
 * - **variação** só entre os dois últimos anos completos seguidos, e só para quem recebeu nos
 *   dois. Fornecedor com um ano só não tem seta: tem começo.
 *
 * Sem cor de alerta em lugar nenhum. Crescer é fato, não achado — o ADR-0013 guarda a cor
 * para a gravidade, e uma seta vermelha aqui leria como acusação.
 */
export function EscadaDeFornecedores({ escada }: { escada: PagamentosPorFornecedor }) {
  if (escada.fornecedores.length === 0) {
    return null;
  }

  const parciais = escada.anos.filter((ano) => ano.parcial);

  return (
    <section aria-labelledby="titulo-escada" className="rounded-cartao border border-borda-suave bg-surface p-5">
      <h2 id="titulo-escada" className="font-display text-xl font-bold text-titulo">
        Quem mais recebeu, ano a ano
      </h2>
      <p className="mt-1 text-sm text-texto-suave">
        {fraseDosMaioresFornecedores(escada.fornecedores.length, escada.totalDeFornecedores)}
        {escada.anoDaVariacao ? (
          <>
            {" "}
            A variação compara {escada.anoBaseDaVariacao} com {escada.anoDaVariacao}, os dois últimos
            anos completos.
          </>
        ) : (
          " Não há dois anos completos seguidos, então não há variação a medir."
        )}
      </p>

      <div className="mt-4 overflow-x-auto">
        <table className="w-full min-w-[46rem] border-collapse text-sm">
          <caption className="sr-only">
            Pagamentos por fornecedor e por ano. Anos marcados como parciais não têm os doze meses
            com despesa.
          </caption>
          <thead>
            <tr className="border-b border-borda-suave text-left text-xs text-texto-suave">
              <th scope="col" className="py-2 pr-3 font-semibold">
                Fornecedor
              </th>
              <th scope="col" className="px-2 py-2 text-right font-semibold">
                Total
              </th>
              {escada.anoDaVariacao ? (
                <th scope="col" className="numeros whitespace-nowrap px-2 py-2 text-right font-semibold">
                  {escada.anoBaseDaVariacao}→{escada.anoDaVariacao}
                </th>
              ) : null}
              {escada.anos.map((ano) => (
                <th
                  key={ano.ano}
                  scope="col"
                  className="numeros whitespace-nowrap px-2 py-2 text-right font-semibold"
                >
                  {ano.ano}
                  {ano.parcial ? (
                    <span className="block font-normal">
                      parcial · {ano.mesesComDespesa} {ano.mesesComDespesa === 1 ? "mês" : "meses"}
                    </span>
                  ) : null}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {escada.fornecedores.map((fornecedor) => {
              const maior = Math.max(...fornecedor.porAno.map((valor) => valor.valor), 0);

              return (
                <tr key={fornecedor.documento} className="border-b border-borda-suave last:border-b-0">
                  <th scope="row" className="w-[14rem] max-w-[14rem] py-2.5 pr-3 text-left font-normal">
                    <span className="block truncate font-semibold text-titulo" title={fornecedor.nome}>
                      {fornecedor.nome}
                    </span>
                    <span className="numeros block text-xs text-texto-suave">
                      {fornecedor.documento}
                    </span>
                  </th>

                  <td
                    className="numeros whitespace-nowrap px-2 py-2.5 text-right align-top font-bold text-titulo"
                    title={formatarReais(fornecedor.total)}
                  >
                    {formatarMoedaCompacta(fornecedor.total)}
                  </td>

                  {escada.anoDaVariacao ? (
                    <td className="numeros whitespace-nowrap px-2 py-2.5 text-right align-top font-semibold text-titulo">
                      <Variacao valor={fornecedor.variacao} />
                    </td>
                  ) : null}

                  {fornecedor.porAno.map((valor) => {
                    const parcial = escada.anos.find((ano) => ano.ano === valor.ano)?.parcial;

                    return (
                      <td
                        key={valor.ano}
                        className={`numeros whitespace-nowrap px-2 py-2.5 text-right align-top ${
                          parcial ? "text-texto-suave" : "text-texto"
                        }`}
                        title={
                          valor.despesas > 0
                            ? `${formatarReais(valor.valor)} em ${formatarNumero(valor.despesas)} ${
                                valor.despesas === 1 ? "despesa" : "despesas"
                              }`
                            : undefined
                        }
                      >
                        {valor.despesas > 0 ? formatarMoedaCompacta(valor.valor) : "—"}
                        {/* O degrau da escada: comprimento relativo ao maior ano do próprio fornecedor. */}
                        <span aria-hidden="true" className="mt-1 block h-1 w-full bg-surface">
                          <span
                            className={`block h-full ${parcial ? "bg-borda-estrutural" : "bg-titulo"}`}
                            style={{
                              width:
                                maior > 0 && valor.valor > 0
                                  ? `max(2px, ${(valor.valor / maior) * 100}%)`
                                  : "0",
                            }}
                          />
                        </span>
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {parciais.length > 0 ? (
        <p className="mt-3 text-xs text-texto-suave">
          Ano parcial é o que não tem os doze meses com despesa — o primeiro ano do mandato, o
          ano corrente, uma licença. Ele aparece, mas não entra na variação: onze meses contra
          doze inflariam o salto sem que nada tivesse mudado.
        </p>
      ) : null}
    </section>
  );
}

/** "+200%", "−39%", ou um traço quando não há os dois anos para comparar. */
export function Variacao({ valor }: { valor: number | null }) {
  if (valor === null) {
    return (
      <span className="font-normal text-texto-suave" title="Não recebeu nos dois anos comparados">
        —
      </span>
    );
  }

  const porcentagem = Math.round(valor * 100);

  if (porcentagem === 0) {
    return <span>estável</span>;
  }

  const sobe = porcentagem > 0;

  return (
    <span aria-label={`${sobe ? "subiu" : "caiu"} ${Math.abs(porcentagem)}%`}>
      <span aria-hidden="true">{sobe ? "↑" : "↓"} </span>
      {sobe ? "+" : "−"}
      {formatarNumero(Math.abs(porcentagem))}%
    </span>
  );
}
