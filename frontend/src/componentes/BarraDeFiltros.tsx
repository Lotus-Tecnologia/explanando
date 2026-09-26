import Link from "next/link";
import type { FiltrosDeDespesas } from "@/lib/filtros";

/**
 * A barra lateral de filtros, no padrão que a Identidade Visual pede — fixa à esquerda, com
 * a tabela fluindo à direita.
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
    "mt-1 w-full border border-borda-suave bg-fundo px-2 py-1.5 text-sm focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-titulo";

  return (
    <form method="get" action={base} className="border border-borda-suave p-4">
      <h2 className="text-sm font-bold text-titulo">Filtrar</h2>

      <div className="mt-3">
        <label htmlFor="filtro-ano" className="block text-xs font-semibold text-texto-suave">
          Ano
        </label>
        <select
          id="filtro-ano"
          name="ano"
          defaultValue={filtros.ano === null ? "" : String(filtros.ano)}
          className={classeDoCampo}
        >
          <option value="">Todos os anos</option>
          {anos.map((ano) => (
            <option key={ano} value={ano}>
              {ano}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-3">
        <label htmlFor="filtro-tipo" className="block text-xs font-semibold text-texto-suave">
          Tipo de despesa
        </label>
        <select
          id="filtro-tipo"
          name="tipo"
          defaultValue={filtros.tipo ?? ""}
          className={classeDoCampo}
        >
          <option value="">Todos os tipos</option>
          {tipos.map((tipo) => (
            <option key={tipo} value={tipo}>
              {tipo}
            </option>
          ))}
        </select>
      </div>

      <div className="mt-3">
        <label
          htmlFor="filtro-indicador"
          className="block text-xs font-semibold text-texto-suave"
        >
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

      {/*
        A ausência de indicador não é atestado de regularidade: pode ser que nenhuma regra
        tenha achado nada, e pode ser que as regras não tenham tido dado para avaliar. Quem
        escolhe "só as que não receberam" precisa ler isso antes do resultado, não depois.
      */}
      <p className="mt-2 text-xs text-texto-suave">
        Despesa sem indicador não é despesa aprovada — parte das regras não pôde ser aplicada
        por ausência de dado público. O bloco <strong>“O que foi analisado”</strong>, no
        perfil, diz quais.
      </p>

      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="submit"
          className="bg-titulo px-3 py-1.5 text-sm font-semibold text-sobre-titulo hover:bg-titulo-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
        >
          Aplicar
        </button>

        {temFiltro ? (
          <Link
            href={base}
            className="text-sm font-semibold text-titulo underline-offset-2 hover:underline"
          >
            Limpar
          </Link>
        ) : null}
      </div>
    </form>
  );
}
