import { describe, expect, it } from "vitest";
import type { AchadoRecente } from "./contrato";
import { descricaoDoItem, escaparXml, montarFeed, tituloDoItem } from "./rss";

/**
 * O feed de alertas — a Fase 3 do plano de execução.
 *
 * "Pronto quando o feed é válido, e um ciclo sem achado novo produz feed sem item novo — e não
 * um item dizendo 'nada encontrado'." As duas coisas estão aqui.
 */

function achado(ajustes: Partial<AchadoRecente> = {}): AchadoRecente {
  return {
    despesaId: "01a0247f-45ba-768c-be73-4f13b3d25e40",
    registradaEm: "2026-08-21T13:25:15.834+00:00",
    parlamentar: {
      id: "01a0227b-c8c2-78dc-8258-40ec43c00f65",
      nomeParlamentar: "Pompeo de Mattos",
      siglaPartido: "PDT",
      uf: "RS",
      casa: "Câmara dos Deputados",
      fotoUrl: null,
    },
    tipoDespesa: "COMBUSTÍVEIS E LUBRIFICANTES.",
    nomeFornecedor: "PARADOURO & CHURRASCARIA <P.W.L> LTDA",
    documentoFornecedor: "43.727.357/0001-89",
    valorLiquido: 132.78,
    dataDocumento: "2026-02-09T00:00:00+00:00",
    ano: 2026,
    mes: 2,
    regras: [
      {
        codigo: "REG_KYB_001",
        gravidade: "Media",
        descricaoDaRegra: "A atividade registrada da empresa não corresponde ao tipo do gasto.",
        detalhe: "Ramo registrado (CNAE 5611201) pertence a outra atividade econômica.",
      },
    ],
    ...ajustes,
  };
}

const CANAL = {
  titulo: "Explanando — Pompeo de Mattos (PDT-RS)",
  descricao: "Despesas com indicador.",
  caminhoDaPagina: "/parlamentares/x",
  caminhoDoFeed: "/parlamentares/x/feed.xml",
};

function analisar(xml: string): Document {
  const documento = new DOMParser().parseFromString(xml, "application/xml");
  // O DOMParser não lança: devolve um documento com <parsererror> quando o XML é inválido.
  expect(documento.getElementsByTagName("parsererror")).toHaveLength(0);
  return documento;
}

describe("escaparXml", () => {
  it("escapa o que o XML não aceita cru", () => {
    expect(escaparXml(`A & B <c> "d" 'e'`)).toBe("A &amp; B &lt;c&gt; &quot;d&quot; &apos;e&apos;");
  });

  it("remove caractere de controle, que derruba o leitor de RSS", () => {
    expect(escaparXml("a\u0001b\u0008c\td")).toBe("abc\td");
  });
});

describe("montarFeed", () => {
  it("é XML bem formado mesmo com & e < no nome do fornecedor", () => {
    const documento = analisar(montarFeed(CANAL, [achado()]));

    expect(documento.querySelector("rss")?.getAttribute("version")).toBe("2.0");
    expect(documento.querySelectorAll("item")).toHaveLength(1);
  });

  it("tem os elementos que o RSS 2.0 exige, e o self que os validadores pedem", () => {
    const documento = analisar(montarFeed(CANAL, [achado()]));
    const canal = documento.querySelector("channel")!;

    expect(canal.querySelector(":scope > title")?.textContent).toBe(CANAL.titulo);
    expect(canal.querySelector(":scope > link")?.textContent).toMatch(/\/parlamentares\/x$/);
    expect(canal.querySelector(":scope > description")?.textContent).toBeTruthy();

    const self = documento.getElementsByTagNameNS("http://www.w3.org/2005/Atom", "link")[0];
    expect(self.getAttribute("rel")).toBe("self");
    expect(self.getAttribute("href")).toMatch(/\/parlamentares\/x\/feed\.xml$/);
  });

  it("o guid é a despesa e não muda entre ciclos", () => {
    const documento = analisar(montarFeed(CANAL, [achado()]));
    const guid = documento.querySelector("item > guid")!;

    expect(guid.textContent).toBe("explanando:despesa:01a0247f-45ba-768c-be73-4f13b3d25e40");
    expect(guid.getAttribute("isPermaLink")).toBe("false");
  });

  it("a data do item é a de entrada da despesa, em RFC 822", () => {
    const documento = analisar(montarFeed(CANAL, [achado()]));

    expect(documento.querySelector("item > pubDate")?.textContent).toBe(
      "Fri, 21 Aug 2026 13:25:15 GMT",
    );
  });

  it("um ciclo sem achado novo produz o mesmo feed, byte a byte", () => {
    // lastBuildDate é a do item mais novo, e não "agora": nada muda, nada muda.
    const antes = montarFeed(CANAL, [achado()]);
    const depois = montarFeed(CANAL, [achado()]);

    expect(depois).toBe(antes);
    expect(antes).toContain("<lastBuildDate>Fri, 21 Aug 2026 13:25:15 GMT</lastBuildDate>");
  });

  it("sem achado, o feed fica sem item — e não com um item dizendo 'nada encontrado'", () => {
    const documento = analisar(montarFeed(CANAL, []));

    expect(documento.querySelectorAll("item")).toHaveLength(0);
    expect(documento.querySelector("lastBuildDate")).toBeNull();
  });
});

describe("o texto do item", () => {
  it("o título diz quem, quanto, em quê e quantos indicadores — sem gritar", () => {
    expect(tituloDoItem(achado())).toBe(
      "Pompeo de Mattos (PDT-RS): R$ 132,78 em Combustíveis e lubrificantes — " +
        "1 indicador de média relevância",
    );
  });

  it("a descrição traz a data do documento sem voltar um dia no fuso", () => {
    expect(descricaoDoItem(achado())).toContain("documento de 09/02/2026");
  });

  it("a descrição carrega a ressalva do ADR-0004, porque o item é lido fora do site", () => {
    const texto = descricaoDoItem(achado());

    expect(texto).toContain("não é acusação");
    expect(texto).toContain("resposta do gabinete");
  });
});
