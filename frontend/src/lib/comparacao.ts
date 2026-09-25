import type { PerfilDoParlamentar } from "./contrato";
import { porcentagensInteiras } from "./formato";

/**
 * O comparador de políticos — a Fase 2 do plano de execução.
 *
 * Três travas moram aqui, e não na página, porque são regra e não desenho:
 *
 * 1. **Só compara dentro da mesma casa.** A CEAP e a CEAPS são cotas distintas, com regras e
 *    valores próprios (ADR-0012). Um deputado ao lado de um senador põe no mesmo eixo números
 *    que não medem a mesma coisa.
 * 2. **Ordem canônica.** São 131.328 pares só na Câmara, então a rota é dinâmica — e `a×b` e
 *    `b×a` precisam ser a mesma página, ou o buscador vê conteúdo duplicado e o link
 *    compartilhado se divide em dois.
 * 3. **Sem amostra, o índice é "não calculado", nunca zero.** Zero diria que foi medido.
 */

/** Os dois ids na ordem canônica: a do texto, que é estável e não depende de nome. */
export function parCanonico(a: string, b: string): [string, string] {
  return a <= b ? [a, b] : [b, a];
}

export function caminhoDaComparacao(a: string, b: string): string {
  const [primeiro, segundo] = parCanonico(a, b);
  return `/comparar/${primeiro}/${segundo}`;
}

export type SituacaoDaComparacao =
  | { tipo: "comparavel" }
  | { tipo: "mesmo-parlamentar" }
  | { tipo: "casas-diferentes"; casaA: string; casaB: string };

export function avaliarComparacao(
  a: PerfilDoParlamentar,
  b: PerfilDoParlamentar,
): SituacaoDaComparacao {
  if (a.id === b.id) {
    return { tipo: "mesmo-parlamentar" };
  }

  if (a.casa !== b.casa) {
    return { tipo: "casas-diferentes", casaA: a.casa, casaB: b.casa };
  }

  return { tipo: "comparavel" };
}

/** Um lado de uma regra na comparação. */
export interface LadoDaRegra {
  quantidade: number;

  /**
   * Nulo quando o parlamentar não tem índice publicável: porcentagem de um índice que não
   * existe não tem sentido.
   */
  porcentagem: number | null;

  /**
   * Falso quando a regra não teve dado nenhum para olhar este parlamentar. Aí o lado é
   * "não avaliado", e não 0% — zero diria que foi medido e não se achou nada (ADR-0007).
   */
  avaliada: boolean;
}

/** Uma regra na comparação, com os dois lados. */
export interface RegraComparada {
  codigo: string;
  descricao: string;
  a: LadoDaRegra;
  b: LadoDaRegra;
}

/**
 * As regras dos dois lados, numa lista só.
 *
 * A porcentagem de cada lado é calculada **sobre a composição inteira daquele lado**, pelo
 * maior resto, e só depois recortada para a união — assim a coluna de cada parlamentar fecha
 * 100, como no perfil. Regra que só um dos dois tem aparece com 0% do outro lado **se ela
 * olhou aquele lado**; se não teve dado, aparece como não avaliada.
 */
export function compararComposicoes(
  a: PerfilDoParlamentar,
  b: PerfilDoParlamentar,
): RegraComparada[] {
  const deA = mapaDoLado(a);
  const deB = mapaDoLado(b);
  const codigos = [...new Set([...deA.parcelas.keys(), ...deB.parcelas.keys()])];

  const pesoDeOrdenacao = (regra: RegraComparada) =>
    Math.max(regra.a.porcentagem ?? 0, regra.b.porcentagem ?? 0);

  return codigos
    .map((codigo) => ({
      codigo,
      descricao:
        deA.parcelas.get(codigo)?.descricao ?? deB.parcelas.get(codigo)?.descricao ?? codigo,
      a: ladoDa(codigo, deA, a),
      b: ladoDa(codigo, deB, b),
    }))
    .sort(
      (x, y) =>
        pesoDeOrdenacao(y) - pesoDeOrdenacao(x) ||
        y.a.quantidade + y.b.quantidade - (x.a.quantidade + x.b.quantidade) ||
        x.codigo.localeCompare(y.codigo),
    );
}

function mapaDoLado(perfil: PerfilDoParlamentar) {
  const composicao = perfil.composicao ?? [];
  const porcentagens = porcentagensInteiras(composicao.map((parcela) => parcela.fatia));

  return {
    parcelas: new Map(
      composicao.map((parcela, indice) => [
        parcela.codigo,
        {
          descricao: parcela.descricao ?? parcela.codigo,
          quantidade: parcela.quantidade,
          porcentagem: porcentagens[indice],
        },
      ]),
    ),
    // Regra fora da cobertura individual — a de rede — conta como avaliada: ela cruza a
    // base inteira e não depende do dado de um parlamentar só.
    semDado: new Set(
      perfil.cobertura
        .filter((regra) => regra.despesasComDado === 0 && !regra.silencioEhConclusivo)
        .map((regra) => regra.codigo),
    ),
  };
}

function ladoDa(
  codigo: string,
  mapa: ReturnType<typeof mapaDoLado>,
  perfil: PerfilDoParlamentar,
): LadoDaRegra {
  const parcela = mapa.parcelas.get(codigo);
  const avaliada = parcela !== undefined || !mapa.semDado.has(codigo);

  return {
    quantidade: parcela?.quantidade ?? 0,
    porcentagem:
      perfil.indiceDeRisco === null || !avaliada ? null : (parcela?.porcentagem ?? 0),
    avaliada,
  };
}

/** Quantos meses tiveram despesa — o que separa o mandato inteiro de quem assumiu no meio. */
export function mesesComDespesa(perfil: PerfilDoParlamentar): number {
  return perfil.serieMensal.filter((mes) => mes.quantidadeDeDespesas > 0).length;
}
