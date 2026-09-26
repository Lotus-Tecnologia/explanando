import type { PerfilDoParlamentar } from "./contrato";
import { formatarDensidade, formatarNumero, formatarReais } from "./formato";

/**
 * O título e a descrição da página de perfil.
 *
 * Isto não é enfeite de SEO: é a **única parte do produto que muita gente vai ler**. O
 * resultado do buscador aparece para quem nunca abriu a página, e é dele que a pessoa tira a
 * conclusão. Tudo o que o ADR-0004 exige do texto na tela vale aqui com mais força, porque
 * aqui não há ao lado um bloco de cobertura para corrigir a impressão.
 *
 * Daí as duas ressalvas embutidas:
 *
 * - densidade insuficiente vira **frase**, nunca `0,00` — zero afirmaria que se mediu e não
 *   se achou nada (ADR-0009);
 * - regra sem dado é **declarada no próprio resumo**, porque o silêncio de uma regra que não
 *   pôde rodar não é aprovação (ADR-0007).
 *
 * O nome fica só no título. Repeti-lo na descrição custaria os caracteres que o buscador
 * corta — e o que se perde no corte é sempre a ressalva, que vem por último.
 */
export function metadadosDoParlamentar(perfil: PerfilDoParlamentar): {
  title: string;
  description: string;
} {
  const title = `${perfil.nomeParlamentar} (${perfil.siglaPartido}-${perfil.uf}) — gastos da cota parlamentar`;

  const frases = [
    `${formatarReais(perfil.volumeTotalGasto)} declarados em ${formatarNumero(perfil.despesasAvaliadas)} despesas da cota parlamentar.`,
    perfil.indiceDeRisco === null
      ? "Menos de 100 despesas: a densidade de indicadores não é publicável."
      : `Índice de ${formatarDensidade(perfil.indiceDeRisco)}: peso dos indicadores a cada 100 despesas.`,
  ];

  if (perfil.regrasSemDado > 0) {
    const total = perfil.regrasConclusivas + perfil.regrasSemDado;
    frases.push(`${perfil.regrasSemDado} de ${total} regras sem dado público para avaliar.`);
  }

  return { title, description: frases.join(" ") };
}

/** Quanto do tipo de despesa cabe no título antes de abreviar. */
const TIPO_NO_TITULO = 45;

/**
 * O título da página de uma despesa: quem, quanto e em quê — nessa ordem.
 *
 * Medido na auditoria de 25/09: o tipo vinha primeiro e inteiro (o do Senado passa de 180
 * caracteres), o título chegava a 239, e a aba do navegador mostrava "… — R$ 1.50", sem o
 * nome. O tipo é abreviado numa fronteira de palavra; a página mostra ele inteiro.
 */
export function tituloDaDespesa(nome: string, valor: string, tipo: string): string {
  let emQue = tipo.trim().replace(/\.$/, "").toLocaleLowerCase("pt-BR");

  if (emQue.length > TIPO_NO_TITULO) {
    const corte = emQue.slice(0, TIPO_NO_TITULO);
    emQue = `${corte.slice(0, corte.lastIndexOf(" ")).replace(/[\s,;:—-]+$/, "")}…`;
  }

  return `${nome} — ${valor} em ${emQue}`;
}
