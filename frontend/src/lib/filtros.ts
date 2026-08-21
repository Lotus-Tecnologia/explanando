/**
 * Os filtros da listagem de despesas, lidos da URL e devolvidos para ela.
 *
 * A regra que organiza este arquivo é uma só: **um filtro nunca pode produzir lista vazia
 * por engano**. Lista vazia numa página de gastos é lida como "não gastou", e essa é a mesma
 * classe de erro que fez a coleta reportar `Falhas: 0` enquanto 497 deputados vinham zerados
 * — ausência apresentada como resultado.
 *
 * Daí as duas escolhas centrais:
 *
 * - Valor que este parlamentar não tem é **descartado**, e o formulário volta a exibir "todos".
 *   Assim o que a tela mostra é sempre o filtro que de fato foi aplicado.
 * - Mudar um filtro **volta para a primeira página**. Sem isso, quem está na página 7 e
 *   escolhe um ano com duas páginas cai numa página vazia por aritmética, não por ausência
 *   de gasto.
 */

export type Indicador = "todas" | "com" | "sem";

export interface FiltrosDeDespesas {
  ano: number | null;
  tipo: string | null;
  indicador: Indicador;
  pagina: number;
}

export const ITENS_POR_PAGINA = 25;

const INDICADORES: readonly Indicador[] = ["todas", "com", "sem"];

/** Parâmetro repetido na URL vira lista; vale o primeiro, e não a concatenação. */
function primeiro(valor: string | string[] | undefined): string | undefined {
  return Array.isArray(valor) ? valor[0] : valor;
}

export function lerFiltros(
  params: Record<string, string | string[] | undefined>,
  anosDisponiveis: readonly number[],
  tiposDisponiveis: readonly string[],
): FiltrosDeDespesas {
  const anoBruto = Number(primeiro(params.ano));
  const ano =
    Number.isInteger(anoBruto) && anosDisponiveis.includes(anoBruto) ? anoBruto : null;

  const tipoBruto = primeiro(params.tipo);
  const tipo = tipoBruto !== undefined && tiposDisponiveis.includes(tipoBruto)
    ? tipoBruto
    : null;

  const indicadorBruto = primeiro(params.indicador) as Indicador | undefined;
  const indicador =
    indicadorBruto !== undefined && INDICADORES.includes(indicadorBruto)
      ? indicadorBruto
      : "todas";

  const paginaBruta = Number(primeiro(params.pagina));
  const pagina = Number.isInteger(paginaBruta) && paginaBruta >= 1 ? paginaBruta : 1;

  return { ano, tipo, indicador, pagina };
}

/**
 * A query string de um conjunto de filtros, com as alterações aplicadas por cima.
 *
 * Alterar qualquer coisa que não seja a página zera a página, pelo motivo do cabeçalho.
 * O que está no padrão sai fora da URL: o endereço de uma despesa é feito para ser colado
 * em algum lugar, e `?indicador=todas&pagina=1` só adiciona ruído a ele.
 */
export function montarConsulta(
  filtros: FiltrosDeDespesas,
  alteracoes: Partial<FiltrosDeDespesas> = {},
): string {
  const mudouAlgoAlemDaPagina = Object.keys(alteracoes).some((chave) => chave !== "pagina");

  const efetivo: FiltrosDeDespesas = {
    ...filtros,
    ...alteracoes,
    pagina: mudouAlgoAlemDaPagina ? 1 : (alteracoes.pagina ?? filtros.pagina),
  };

  const consulta = new URLSearchParams();

  if (efetivo.ano !== null) {
    consulta.set("ano", String(efetivo.ano));
  }

  if (efetivo.tipo !== null) {
    consulta.set("tipo", efetivo.tipo);
  }

  if (efetivo.indicador !== "todas") {
    consulta.set("indicador", efetivo.indicador);
  }

  if (efetivo.pagina > 1) {
    consulta.set("pagina", String(efetivo.pagina));
  }

  return consulta.toString();
}

/** `comFlag` como a API o espera: verdadeiro, falso ou ausente. */
export function comFlagDe(indicador: Indicador): boolean | undefined {
  if (indicador === "com") {
    return true;
  }

  return indicador === "sem" ? false : undefined;
}
