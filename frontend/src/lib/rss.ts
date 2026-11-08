import type { AchadoRecente, Gravidade } from "./contrato";
import { formatarDataDoDocumento, formatarReais } from "./formato";
import { URL_BASE } from "./site";
import { documentoPublicavel } from "./frases";

/**
 * O feed RSS de alertas — a Fase 3 do plano de execução.
 *
 * **Por que RSS primeiro.** Não guarda dado de ninguém, não depende de provedor de e-mail e
 * funciona antes da VPS. É o alerta que custa quase nada e já mede se há demanda.
 *
 * Três decisões que o feed carrega:
 *
 * - **O `guid` é a despesa**, e não a flag. A varredura recria as flags a cada passada, e um
 *   `guid` por flag faria o leitor de RSS receber "novidades" que são o mesmo achado de sempre.
 * - **A data é a de entrada da despesa na base.** Um achado produzido por regra alterada sobre
 *   despesa antiga fica com a data antiga — e só despesa nova é notícia sobre o parlamentar.
 * - **`lastBuildDate` é a do item mais novo, e não "agora".** Um ciclo sem achado novo gera um
 *   feed idêntico ao anterior, byte a byte, em vez de um feed "atualizado" sem nada de novo.
 */

const RELEVANCIA: Record<Gravidade, string> = {
  Alta: "alta",
  Media: "média",
  Baixa: "baixa",
};

/** Escapa o que o XML não aceita cru. Aspas também, porque vão dentro de atributo. */
export function escaparXml(texto: string): string {
  return texto
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;")
    // Caracteres de controle quebram o parser do leitor de RSS e não carregam significado.
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, "");
}

/** Data no formato do RSS 2.0 (RFC 822). `toUTCString` já entrega nesse formato. */
function dataRfc822(iso: string): string {
  return new Date(iso).toUTCString();
}

/** "Combustíveis e lubrificantes", e não "COMBUSTÍVEIS E LUBRIFICANTES." — um título não grita. */
function tipoLegivel(tipo: string): string {
  const semPonto = tipo.trim().replace(/\.$/, "");
  const minusculo = semPonto.toLocaleLowerCase("pt-BR");
  return minusculo.charAt(0).toLocaleUpperCase("pt-BR") + minusculo.slice(1);
}

export function tituloDoItem(achado: AchadoRecente): string {
  const { parlamentar } = achado;
  const quantos = achado.regras.length;
  const principal = achado.regras[0];
  const relevancia = principal ? ` de ${RELEVANCIA[principal.gravidade]} relevância` : "";

  return (
    `${parlamentar.nomeParlamentar} (${parlamentar.siglaPartido}-${parlamentar.uf}): ` +
    `${formatarReais(achado.valorLiquido)} em ${tipoLegivel(achado.tipoDespesa)} — ` +
    `${quantos} ${quantos === 1 ? "indicador" : "indicadores"}${quantos === 1 ? relevancia : ""}`
  );
}

export function descricaoDoItem(achado: AchadoRecente): string {
  const fornecedor = achado.nomeFornecedor ?? "Fornecedor não identificado";
  const data = formatarDataDoDocumento(achado.dataDocumento);

  const linhas = [
    `${fornecedor} (${documentoPublicavel(achado.documentoFornecedor)}), documento de ${data}, ` +
      `competência ${String(achado.mes).padStart(2, "0")}/${achado.ano}.`,
    ...achado.regras.map(
      (regra) =>
        `[${RELEVANCIA[regra.gravidade]} relevância] ` +
        `${regra.descricaoDaRegra ?? regra.codigo} ${regra.detalhe}`,
    ),
    // A ressalva viaja com o item. O leitor de RSS mostra o item fora do site, sem o bloco de
    // cobertura e sem a página ao lado — o ADR-0004 vale aqui com mais força, não com menos.
    "Indicador descreve inconsistência entre registros públicos; não é acusação. " +
      "A resposta do gabinete, quando houver, aparece na página da despesa.",
  ];

  return linhas.join("\n\n");
}

export interface CanalDoFeed {
  titulo: string;
  descricao: string;

  /** A página que o feed acompanha — o perfil, ou a home para o feed geral. */
  caminhoDaPagina: string;

  /** O próprio endereço do feed, para o `atom:link rel="self"` que os validadores pedem. */
  caminhoDoFeed: string;
}

export function montarFeed(canal: CanalDoFeed, achados: AchadoRecente[]): string {
  const datas = achados
    .map((achado) => achado.registradaEm)
    .filter((data): data is string => data !== null)
    .sort();

  const maisRecente = datas.at(-1);

  const itens = achados.map((achado) => {
    const link = `${URL_BASE}/despesas/${achado.despesaId}`;

    return [
      "    <item>",
      `      <title>${escaparXml(tituloDoItem(achado))}</title>`,
      `      <link>${escaparXml(link)}</link>`,
      `      <guid isPermaLink="false">explanando:despesa:${escaparXml(achado.despesaId)}</guid>`,
      achado.registradaEm ? `      <pubDate>${dataRfc822(achado.registradaEm)}</pubDate>` : null,
      `      <description>${escaparXml(descricaoDoItem(achado))}</description>`,
      "    </item>",
    ]
      .filter((linha) => linha !== null)
      .join("\n");
  });

  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom">',
    "  <channel>",
    `    <title>${escaparXml(canal.titulo)}</title>`,
    `    <link>${escaparXml(URL_BASE + canal.caminhoDaPagina)}</link>`,
    `    <description>${escaparXml(canal.descricao)}</description>`,
    "    <language>pt-BR</language>",
    `    <atom:link href="${escaparXml(URL_BASE + canal.caminhoDoFeed)}" rel="self" type="application/rss+xml"/>`,
    maisRecente ? `    <lastBuildDate>${dataRfc822(maisRecente)}</lastBuildDate>` : null,
    // Seis horas: o dado muda uma vez por ciclo de coleta, não de minuto em minuto.
    "    <ttl>360</ttl>",
    ...itens,
    "  </channel>",
    "</rss>",
    "",
  ]
    .filter((linha) => linha !== null)
    .join("\n");
}

/** A resposta HTTP de um feed, com o tipo que leitor de RSS e navegador reconhecem. */
export function respostaDoFeed(xml: string): Response {
  return new Response(xml, {
    headers: {
      "Content-Type": "application/rss+xml; charset=utf-8",
      "Cache-Control": "public, max-age=300, s-maxage=300",
    },
  });
}
