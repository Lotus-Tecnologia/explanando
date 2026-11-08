import Link from "next/link";
import { classeDeBotao } from "./base/Botao";
import type { FiltrosDeDespesas } from "@/lib/filtros";
import { caixaDeFrase } from "@/lib/frases";

/**
 * A linha de filtros acima da lista de despesas (direção C da Etapa 2): um "Filtrar"
 * nativo que abre os três campos, aberto sozinho quando há filtro aplicado.
 *
 * É um **formulário GET de verdade**, não um punhado de `onChange`. Três consequências, e as
 * três importam num produto de utilidade pública: funciona sem JavaScript, o resultado tem
 * endereço próprio que se cola em qualquer lugar, e o botão de voltar do navegador desfaz o
 * filtro como qualquer pessoa espera.
 *
 * As opções vêm do parlamentar, nunca de uma lista fixa aqui. Uma opção que devolve lista
 * vazia faz o leitor concluir que não houve gasto — e o que houve foi opção mal montada.
 * Pelo mesmo motivo os campos exibem o filtro **aplicado**, e não o que veio na URL: se a
 * URL trouxe um ano impossível, o resultado o ignorou, e a tela precisa dizer isso.
 */
export function BarraDeFiltros({
  base,
  anos,
  tipos,
  filtros,
}: {
  base: string;
  anos: readonly number[];
  tipos: readonly string[];
  filtros: FiltrosDeDespesas;
}) {
  const temFiltro =
    filtros.ano !== null || filtros.tipo !== null || filtros.indicador !== "todas";

  const classeDoCampo =
    "mt-1.5 min-h-11 w-full rounded-campo border-[1.5px] border-borda-suave bg-surface px-3 text-[0.9375rem] text-texto";

  return (
    <details open={temFiltro} className="group rounded-cartao border border-borda-suave bg-surface">
      {/*
        Uma linha de filtros acima da lista, atrás de um "Filtrar" nativo. Abre sozinha
        quando há filtro aplicado, para que o resultado nunca apareça sem a pergunta.
      */}
      <summary className="flex min-h-12 cursor-pointer list-none items-center gap-3 px-4 [&::-webkit-details-marker]:hidden">
        <svg aria-hidden="true" viewBox="0 0 20 20" className="size-5 text-texto-suave">
          <path d="M3 5h14M6 10h8M8.5 15h3" fill="none" stroke="currentColor" strokeWidth="2" />
        </svg>
        <span className="font-semibold text-texto">Filtrar</span>
        {temFiltro ? (
          <span className="truncate text-sm text-texto-suave">
            {[filtros.ano, filtros.tipo, filtros.indicador === "com" ? "com indicador" : filtros.indicador === "sem" ? "sem indicador" : null]
              .filter(Boolean)
              .join(", ")}
          </span>
        ) : null}
        <svg
          aria-hidden="true"
          viewBox="0 0 20 20"
          className="ms-auto size-5 shrink-0 text-texto-suave transition-transform group-open:rotate-180"
        >
          <path d="m5 8 5 5 5-5" fill="none" stroke="currentColor" strokeWidth="2" />
        </svg>
      </summary>

      <form method="get" action={base} className="border-t border-borda-suave px-4 py-4">
        <div className="grid gap-3 md:grid-cols-[8rem_minmax(0,1fr)_minmax(0,1fr)]">
          <div>
            <label htmlFor="filtro-ano" className="block text-sm font-semibold text-texto">
              Ano
            </label>
            <select
              id="filtro-ano"
              name="ano"
              defaultValue={filtros.ano === null ? "" : String(filtros.ano)}
              className={classeDoCampo}
            >
              <option value="">Todos</option>
              {anos.map((ano) => (
                <option key={ano} value={ano}>
                  {ano}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filtro-tipo" className="block text-sm font-semibold text-texto">
              Tipo de despesa
            </label>
            <select id="filtro-tipo" name="tipo" defaultValue={filtros.tipo ?? ""} className={classeDoCampo}>
              <option value="">Todos os tipos</option>
              {tipos.map((tipo) => (
                <option key={tipo} value={tipo}>
                  {caixaDeFrase(tipo)}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label htmlFor="filtro-indicador" className="block text-sm font-semibold text-texto">
              Indicadores
            </label>
            <select
              id="filtro-indicador"
              name="indicador"
              defaultValue={filtros.indicador}
              className={classeDoCampo}
            >
              <option value="todas">Todas as despesas</option>
              <option value="com">Só as que receberam indicador</option>
              <option value="sem">Só as que não receberam</option>
            </select>
          </div>
        </div>

        {/*
          A ausência de indicador não é atestado de regularidade: pode ser que nenhuma regra
          tenha achado nada, e pode ser que as regras não tenham tido dado para avaliar. Quem
          escolhe "só as que não receberam" precisa ler isso antes do resultado, não depois.
        */}
        <p className="mt-3 text-sm text-texto-suave">
          Despesa sem indicador não é despesa aprovada — parte das regras não pôde ser aplicada
          por ausência de dado público. O bloco <strong>“O que foi analisado”</strong>, no
          perfil, diz quais.
        </p>

        <div className="mt-4 flex flex-wrap items-center gap-3">
          <button type="submit" className={classeDeBotao("primario")}>
            Aplicar
          </button>

          {temFiltro ? (
            <Link href={base} className={classeDeBotao("texto")}>
              Limpar
            </Link>
          ) : null}
        </div>
      </form>
    </details>
  );
}
