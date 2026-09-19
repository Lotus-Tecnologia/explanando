import { afterEach, describe, expect, it, vi } from "vitest";
import {
  MAXIMO_DE_ITENS_POR_PAGINA,
  coletarIdsParaPreRender,
  ranquearTudo,
} from "./api";
import { metadadosDoParlamentar } from "./metadados";
import { URL_BASE, ehIndexavel, rotasDoSitemap } from "./site";
import { CASAS } from "./casas";
import { ORDENACOES, caminhoDoRanking, ordenacaoPorChave } from "./ordenacoes";
import { lerFiltros, montarConsulta } from "./filtros";
import { corDoPoder } from "./grafo";
import type { ItemDoRanking, Pagina, PerfilDoParlamentar } from "./contrato";

/**
 * O que se prova aqui sustenta duas premissas que o produto assumiu por escrito.
 *
 * A primeira é de custo: o ADR-0001 aposta que é a **CDN**, e não o Redis, que absorve o pico
 * viral. Isso só é verdade se as 513 páginas de perfil existirem como arquivo antes do pico —
 * e a coleta dos ids é o que decide se elas existem.
 *
 * A segunda é de linguagem: o título e a descrição são o que o Google mostra a quem nunca
 * abriu a página. Se a interface toda descreve sem imputar (ADR-0004) mas a meta description
 * imputa, o produto imputa.
 */

// ── Coleta de ids para a pré-renderização ────────────────────────────────────────────────

function itemDoRanking(indice: number, prefixo = "parlamentar"): ItemDoRanking {
  return {
    id: `${prefixo}-${indice}`,
    idExterno: 100000 + indice,
    nomeParlamentar: `${prefixo.toUpperCase()} ${indice}`,
    casa:
      prefixo === "senador" ? "Senado Federal" : "Câmara dos Deputados",
    siglaPartido: "XX",
    uf: "SP",
    fotoUrl: null,
    volumeTotalGasto: 1000,
    despesasAvaliadas: 100,
    indiceDeRisco: 1,
    indiceDeAltas: 0,
    flagsAltas: 0,
    flagsMedias: 0,
    flagsBaixas: 0,
    amostraSuficiente: true,
  };
}

function paginaDoRanking(
  numero: number,
  tamanho: number,
  total: number,
  prefixo = "parlamentar",
): Pagina<ItemDoRanking> {
  const inicio = (numero - 1) * tamanho;
  const quantidade = Math.max(0, Math.min(tamanho, total - inicio));

  return {
    itens: Array.from({ length: quantidade }, (_, i) => itemDoRanking(inicio + i, prefixo)),
    total,
    numeroDaPagina: numero,
    itensPorPagina: tamanho,
    totalDePaginas: Math.ceil(total / tamanho),
  };
}

function respostaOk(corpo: unknown) {
  return { ok: true, status: 200, json: async () => corpo } as unknown as Response;
}

/**
 * A API falsa **replica o teto do servidor**: `ConsultasPublicasEfCore.Normalizar` faz
 * `Math.Clamp(itens, 1, 200)` e devolve 200 sem erro nenhum a quem pediu 513. Um dublê que
 * atendesse ao pedido literal deixaria passar exatamente o bug que estes testes existem para
 * impedir — o build ficaria verde com 313 parlamentares fora da CDN.
 */
function apiFalsa(deputados: number, senadores = 0) {
  const chamadas: string[] = [];

  const fetchFalso = vi.fn(async (url: string | URL) => {
    const endereco = new URL(String(url));
    chamadas.push(endereco.pathname + endereco.search);

    const pedido = Number(endereco.searchParams.get("itens") ?? 50);
    const tamanho = Math.min(Math.max(pedido, 1), 200);
    const numero = Math.max(1, Number(endereco.searchParams.get("pagina") ?? 1));

    // A casa muda o resultado, como na API de verdade. Uma falsa que ignorasse o parâmetro
    // devolveria as mesmas pessoas para as duas casas — e um filtro quebrado passaria.
    const senado = endereco.searchParams.get("casa") === "senadores";
    const total = senado ? senadores : deputados;

    return respostaOk(paginaDoRanking(numero, tamanho, total, senado ? "senador" : "parlamentar"));
  });

  vi.stubGlobal("fetch", fetchFalso);
  return { chamadas, fetchFalso };
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
});

describe("coletarIdsParaPreRender", () => {
  it("devolve os 513 parlamentares, e nao os 200 que cabem numa pagina", async () => {
    apiFalsa(513);

    const ids = await coletarIdsParaPreRender();

    expect(ids).toHaveLength(513);
    expect(new Set(ids).size).toBe(513);
  });

  it("junta AS DUAS CASAS, porque as duas precisam de pagina", async () => {
    // O ranking é separado porque comparar cotas distintas seria errado. A pré-renderização
    // não compara nada: buscar uma casa só deixaria a outra inteira fora do sitemap e das
    // páginas estáticas, e o build passaria verde relatando sucesso.
    apiFalsa(513, 118);

    const ids = await coletarIdsParaPreRender();

    expect(ids).toHaveLength(631);
    expect(new Set(ids).size).toBe(631);
    expect(ids.some((id) => id.startsWith("senador-"))).toBe(true);
  });

  it("nunca pede mais itens do que o servidor entrega, para nao truncar em silencio", async () => {
    const { chamadas } = apiFalsa(513);

    await coletarIdsParaPreRender();

    for (const chamada of chamadas) {
      const pedido = Number(new URL(`http://x${chamada}`).searchParams.get("itens"));
      expect(pedido).toBeLessThanOrEqual(MAXIMO_DE_ITENS_POR_PAGINA);
    }

    // Três páginas para os 513 deputados, mais uma para o Senado vazio nesta montagem.
    expect(chamadas).toHaveLength(4);
  });

  it("devolve lista vazia quando a API esta fora, para o build nao quebrar em CI", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("fetch failed");
      }),
    );

    await expect(coletarIdsParaPreRender()).resolves.toEqual([]);
  });

  it("devolve lista vazia quando a API responde erro", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({ ok: false, status: 500, json: async () => ({}) }) as unknown as Response),
    );

    await expect(coletarIdsParaPreRender()).resolves.toEqual([]);
  });

  it("para quando a pagina vem vazia, mesmo que o total prometa mais", async () => {
    // Um total mentiroso com paginas vazias travaria o build num laco infinito. O que manda
    // e o que chegou, nao o que o corpo prometeu.
    const chamadas: string[] = [];

    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string | URL) => {
        const endereco = new URL(String(url));
        chamadas.push(endereco.search);
        const numero = Number(endereco.searchParams.get("pagina") ?? 1);

        return respostaOk({
          itens: numero === 1 ? [itemDoRanking(0)] : [],
          total: 5000,
          numeroDaPagina: numero,
          itensPorPagina: 200,
          totalDePaginas: 25,
        } satisfies Pagina<ItemDoRanking>);
      }),
    );

    const ids = await coletarIdsParaPreRender();

    // Uma por casa: cada uma para na primeira página vazia, em vez de confiar no total.
    expect(ids).toEqual(["parlamentar-0", "parlamentar-0"]);
    expect(chamadas).toHaveLength(4);
  });
});

describe("ranquearTudo", () => {
  it("traz os 513 de uma vez, para a tabela nao esconder 463 atras de paginacao", async () => {
    apiFalsa(513);

    const itens = await ranquearTudo(CASAS[0], "volume");

    expect(itens).toHaveLength(513);
  });

  it("preserva a ordem que o servidor decidiu", async () => {
    // A ordenacao e' regra de negocio (ADR-0009): o indice exclui quem esta abaixo do piso
    // amostral. Reordenar aqui duplicaria essa regra no navegador, onde ela sairia de
    // sincronia sem ninguem perceber.
    apiFalsa(513);

    const itens = await ranquearTudo(CASAS[0], "volume");

    expect(itens!.slice(0, 3).map((i) => i.id)).toEqual([
      "parlamentar-0",
      "parlamentar-1",
      "parlamentar-2",
    ]);
  });

  it("pede ao servidor a ordenacao que recebeu", async () => {
    const { chamadas } = apiFalsa(300);

    await ranquearTudo(CASAS[0], "gravidade");

    expect(chamadas.every((c) => c.includes("ordenar=gravidade"))).toBe(true);
  });

  it("devolve nulo quando a API esta fora, em vez de derrubar o build", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("fetch failed");
      }),
    );

    await expect(ranquearTudo(CASAS[0], "volume")).resolves.toBeNull();
  });
});

// ── Titulo e descricao da pagina indexada ────────────────────────────────────────────────

function perfil(ajustes: Partial<PerfilDoParlamentar> = {}): PerfilDoParlamentar {
  return {
    id: "9f1c2b3a",
    idExterno: 204554,
    nomeParlamentar: "MARIA DA SILVA",
    nomeCivil: "MARIA DA SILVA SOUZA",
    siglaPartido: "PT",
    uf: "SP",
    casa: "Camara",
    fotoUrl: null,
    volumeTotalGasto: 1234567.89,
    despesasAvaliadas: 1234,
    indiceDeRisco: 3.21,
    indiceDeAltas: 0.16,
    amostraSuficiente: true,
    flagsAltas: 2,
    flagsMedias: 20,
    flagsBaixas: 18,
    dataUltimaVarredura: "2026-08-21T19:38:14Z",
    versaoDoMotor: 1,
    serieMensal: [],
    cobertura: [],
    regrasConclusivas: 8,
    regrasSemDado: 3,
    tiposDeDespesa: ["COMBUSTÍVEIS E LUBRIFICANTES.", "TELEFONIA"],
    ...ajustes,
  };
}

/**
 * A lista do ADR-0004, dobrada aqui em minusculas sem acento. O casamento e por palavra
 * inteira porque "descricao" contem "cri" e "discriminado" contem "crim" — o mesmo tropeco
 * que ja custou bug no backend.
 */
const TERMOS_PROIBIDOS = [
  "fraude", "fraudulenta", "fraudulento", "crime", "crimes", "criminoso", "criminosa",
  "corrupcao", "corrupto", "corrupta", "lavagem", "desvio", "desviou", "desviado",
  "roubo", "roubou", "propina", "caixa dois",
];

function termoAcusatorio(texto: string): string | null {
  const dobrado = texto.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase();

  for (const termo of TERMOS_PROIBIDOS) {
    if (new RegExp(`(^|[^\\p{L}])${termo}($|[^\\p{L}])`, "u").test(dobrado)) {
      return termo;
    }
  }

  return null;
}

describe("metadadosDoParlamentar", () => {
  it("identifica o parlamentar no titulo, com partido e UF", () => {
    const { title } = metadadosDoParlamentar(perfil());

    expect(title).toContain("MARIA DA SILVA");
    expect(title).toContain("PT-SP");
  });

  it("descreve volume e despesas analisadas", () => {
    const { description } = metadadosDoParlamentar(perfil());

    expect(description).toContain("1.234.567,89");
    expect(description).toContain("1.234 despesas");
  });

  it("nao imputa conduta — o vocabulario do ADR-0004 vale na meta description", () => {
    const { description, title } = metadadosDoParlamentar(perfil());

    expect(termoAcusatorio(description)).toBeNull();
    expect(termoAcusatorio(title)).toBeNull();
  });

  it("nao publica densidade zero quando a amostra e insuficiente", () => {
    // Zero diria que a densidade foi medida e deu nada. O ADR-0009 exige nulo, e nulo aqui
    // vira frase, nao numero.
    const { description } = metadadosDoParlamentar(
      perfil({ indiceDeRisco: null, indiceDeAltas: null, amostraSuficiente: false, despesasAvaliadas: 42 }),
    );

    expect(description).not.toMatch(/0[.,]00 indicadores/);
    expect(description).toMatch(/publicavel|publicável/i);
  });

  it("declara as regras sem dado, porque o silencio delas nao e aprovacao", () => {
    const { description } = metadadosDoParlamentar(perfil({ regrasConclusivas: 8, regrasSemDado: 3 }));

    expect(description).toContain("3 de 11 regras");
  });

  it("nao inventa ressalva quando todas as regras puderam avaliar", () => {
    const { description } = metadadosDoParlamentar(
      perfil({ regrasConclusivas: 11, regrasSemDado: 0 }),
    );

    expect(description).not.toMatch(/regras/i);
  });

  it("cabe no que o buscador exibe, mesmo no perfil mais extenso da base", () => {
    const { description } = metadadosDoParlamentar(
      perfil({
        nomeParlamentar: "ANTONIO CARLOS DE OLIVEIRA RODRIGUES JUNIOR",
        volumeTotalGasto: 802407895.78,
        despesasAvaliadas: 711796,
      }),
    );

    expect(description.length).toBeLessThanOrEqual(200);
  });
});

// ── Sitemap ──────────────────────────────────────────────────────────────────────────────

describe("rotasDoSitemap", () => {
  const agora = new Date("2026-08-21T00:00:00Z");

  it("lista a home e os SEIS rankings — tres ordenacoes vezes duas casas", () => {
    const rotas = rotasDoSitemap([], agora);
    const enderecos = rotas.map((r) => r.url);

    expect(enderecos).toContain(`${URL_BASE}/`);

    for (const casa of CASAS) {
      for (const ordenacao of ORDENACOES) {
        expect(enderecos).toContain(`${URL_BASE}${caminhoDoRanking(casa, ordenacao)}`);
      }
    }
  });

  it("nao privilegia uma casa sobre a outra", () => {
    // O tamanho desigual — 513 deputados contra 118 senadores — e' real e nao vira
    // hierarquia: as raizes das duas casas tem a mesma prioridade no sitemap.
    const rotas = rotasDoSitemap([], agora);
    const prioridades = CASAS.map(
      (casa) => rotas.find((r) => r.url === `${URL_BASE}${casa.caminho}`)!.priority,
    );

    expect(new Set(prioridades).size).toBe(1);
  });

  it("inclui um perfil por parlamentar", () => {
    const rotas = rotasDoSitemap(["a1", "b2", "c3"], agora);

    expect(rotas.map((r) => r.url)).toEqual(
      expect.arrayContaining([
        `${URL_BASE}/parlamentares/a1`,
        `${URL_BASE}/parlamentares/b2`,
        `${URL_BASE}/parlamentares/c3`,
      ]),
    );
  });

  it("nunca devolve sitemap vazio quando a API esta fora", () => {
    // Um sitemap vazio pede ao buscador que desindexe o site inteiro. Sem ids, as rotas
    // fixas continuam valendo — elas existem independentemente da API responder no build.
    expect(rotasDoSitemap([], agora).length).toBeGreaterThan(0);
  });

  it("nao repete endereco", () => {
    const rotas = rotasDoSitemap(["a1", "a1", "b2"], agora);
    const enderecos = rotas.map((r) => r.url);

    expect(new Set(enderecos).size).toBe(enderecos.length);
  });

  it("so emite endereco absoluto sob o dominio do site", () => {
    for (const rota of rotasDoSitemap(["a1"], agora)) {
      expect(rota.url.startsWith(`${URL_BASE}/`)).toBe(true);
    }
  });

  it("declara a home como a rota de maior prioridade", () => {
    const rotas = rotasDoSitemap(["a1"], agora);
    const home = rotas.find((r) => r.url === `${URL_BASE}/`)!;

    for (const outra of rotas.filter((r) => r.url !== home.url)) {
      expect(home.priority).toBeGreaterThanOrEqual(outra.priority!);
    }
  });
});

// ── As tres ordenacoes ───────────────────────────────────────────────────────────────────

describe("ORDENACOES", () => {
  it("cobre as tres que a API aceita, e nada alem", () => {
    expect(ORDENACOES.map((o) => o.chave)).toEqual(["volume", "indice", "gravidade"]);
  });

  it("tem uma rota propria por ordenacao DENTRO de cada casa", () => {
    for (const casa of CASAS) {
      const caminhos = ORDENACOES.map((o) => caminhoDoRanking(casa, o));

      expect(new Set(caminhos).size).toBe(3);
      expect(caminhos).toContain(casa.caminho);
    }
  });

  it("nenhuma rota de ranking colide entre as casas", () => {
    // O que impede `/senado/indice` de cair no ranking da Camara por descuido de sufixo.
    const todas = CASAS.flatMap((casa) =>
      ORDENACOES.map((ordenacao) => caminhoDoRanking(casa, ordenacao)),
    );

    expect(new Set(todas).size).toBe(todas.length);
  });

  it("aparece inteira no sitemap — a rota que nao esta la nao existe para o buscador", () => {
    // A invariante que impede o sitemap de divergir das rotas: acrescentar uma ordenacao
    // sem anuncia-la deixaria uma pagina publica fora do indice, em silencio.
    const enderecos = rotasDoSitemap([], new Date()).map((r) => r.url);

    for (const casa of CASAS) {
      for (const ordenacao of ORDENACOES) {
        expect(enderecos).toContain(`${URL_BASE}${caminhoDoRanking(casa, ordenacao)}`);
      }
    }
  });

  it("nao imputa conduta em nenhum rotulo, titulo ou explicacao", () => {
    for (const ordenacao of ORDENACOES) {
      for (const texto of [
        ordenacao.rotulo,
        ordenacao.titulo,
        ordenacao.descricao,
        ordenacao.explicacao,
      ]) {
        expect(termoAcusatorio(texto)).toBeNull();
      }
    }
  });

  it("explica que as duas densidades excluem quem esta abaixo do piso", () => {
    // Sem essa frase, o leitor conclui que os 15 ausentes nao tem indicador nenhum.
    for (const chave of ["indice", "gravidade"] as const) {
      expect(ordenacaoPorChave(chave)!.explicacao).toMatch(/100 despesas/);
    }
  });

  it("devolve nulo para chave desconhecida, em vez de cair no volume em silencio", () => {
    // A API devolve 400 para ordenacao inventada de proposito: cair no volume faria o
    // consumidor achar que estava ordenando por risco.
    expect(ordenacaoPorChave("inventada")).toBeNull();
  });
});

// ── Filtros da listagem de despesas ──────────────────────────────────────────────────────

const ANOS = [2023, 2024, 2025, 2026];
const TIPOS = ["COMBUSTÍVEIS E LUBRIFICANTES.", "TELEFONIA"];

function ler(params: Record<string, string | string[] | undefined>) {
  return lerFiltros(params, ANOS, TIPOS);
}

describe("lerFiltros", () => {
  it("le o que veio na URL", () => {
    const filtros = ler({ ano: "2025", tipo: "TELEFONIA", indicador: "com", pagina: "3" });

    expect(filtros).toEqual({
      ano: 2025,
      tipo: "TELEFONIA",
      indicador: "com",
      pagina: 3,
    });
  });

  it("sem nada na URL, nao filtra nada", () => {
    expect(ler({})).toEqual({ ano: null, tipo: null, indicador: "todas", pagina: 1 });
  });

  it("descarta ano que este parlamentar nao teve", () => {
    // Ano fora da serie devolveria lista vazia, e lista vazia e' lida como "nao gastou" —
    // quando o que houve foi filtro impossivel.
    expect(ler({ ano: "1998" }).ano).toBeNull();
    expect(ler({ ano: "abacaxi" }).ano).toBeNull();
  });

  it("descarta tipo que este parlamentar nao teve", () => {
    expect(ler({ tipo: "LOCAÇÃO DE AERONAVE" }).tipo).toBeNull();
  });

  it("aceita o tipo com a grafia exata que a API publicou, acento e ponto final incluidos", () => {
    expect(ler({ tipo: "COMBUSTÍVEIS E LUBRIFICANTES." }).tipo).toBe(
      "COMBUSTÍVEIS E LUBRIFICANTES.",
    );
  });

  it("indicador desconhecido vira 'todas', nunca um recorte silencioso", () => {
    expect(ler({ indicador: "vermelhas" }).indicador).toBe("todas");
  });

  it("pagina invalida ou fora da faixa volta para a primeira", () => {
    expect(ler({ pagina: "0" }).pagina).toBe(1);
    expect(ler({ pagina: "-4" }).pagina).toBe(1);
    expect(ler({ pagina: "nao" }).pagina).toBe(1);
  });

  it("ignora repeticao do mesmo parametro em vez de concatenar", () => {
    expect(ler({ ano: ["2025", "2024"] }).ano).toBe(2025);
  });
});

describe("montarConsulta", () => {
  it("omite o que esta no padrao, para a URL compartilhada ficar limpa", () => {
    expect(montarConsulta(ler({}))).toBe("");
  });

  it("preserva os demais filtros ao trocar de pagina", () => {
    const filtros = ler({ ano: "2025", tipo: "TELEFONIA", indicador: "com", pagina: "1" });
    const consulta = montarConsulta(filtros, { pagina: 4 });

    expect(consulta).toContain("ano=2025");
    expect(consulta).toContain("indicador=com");
    expect(consulta).toContain("pagina=4");
    expect(consulta).toContain(`tipo=${encodeURIComponent("TELEFONIA")}`);
  });

  it("volta para a primeira pagina quando um filtro muda", () => {
    // Sem isto, quem esta na pagina 7 e filtra por um ano com 2 paginas cai numa pagina
    // vazia — e le a lista vazia como "nao ha despesa".
    const filtros = ler({ pagina: "7" });

    expect(montarConsulta(filtros, { ano: 2024 })).not.toContain("pagina=");
  });

  it("mantem a pagina quando so a pagina muda", () => {
    expect(montarConsulta(ler({ ano: "2024", pagina: "2" }), { pagina: 3 })).toContain(
      "pagina=3",
    );
  });

  it("limpar um filtro o tira da URL", () => {
    expect(montarConsulta(ler({ ano: "2024" }), { ano: null })).toBe("");
  });
});

// ── A chave interna do renderizador ──────────────────────────────────────────────────────

describe("cabecalho interno", () => {
  function cabecalhosDe(fetchFalso: ReturnType<typeof vi.fn>) {
    return fetchFalso.mock.calls.map(
      ([, opcoes]) => (opcoes as RequestInit | undefined)?.headers as Record<string, string>,
    );
  }

  it("sem chave configurada, nao inventa cabecalho nenhum", async () => {
    vi.stubEnv("EXPLANANDO_CHAVE_INTERNA", "");
    const { fetchFalso } = apiFalsa(10);

    await ranquearTudo(CASAS[0], "volume");

    for (const cabecalhos of cabecalhosDe(fetchFalso)) {
      expect(cabecalhos["X-Explanando-Interno"]).toBeUndefined();
    }
  });

  it("com chave, identifica-se em toda requisicao", async () => {
    // O build busca 516 paginas de um IP so. Sem se identificar, o nosso proprio
    // renderizador esbarra no teto publico — e a falha aparece como build quebrado.
    vi.stubEnv("EXPLANANDO_CHAVE_INTERNA", "segredo");
    const { fetchFalso } = apiFalsa(513);

    await ranquearTudo(CASAS[0], "volume");

    const cabecalhos = cabecalhosDe(fetchFalso);
    expect(cabecalhos).toHaveLength(3);

    for (const cabecalho of cabecalhos) {
      expect(cabecalho["X-Explanando-Interno"]).toBe("segredo");
    }
  });

  it("le a variavel a cada chamada, e nao uma vez na importacao", async () => {
    // Prova que a chave NAO e' embutida no pacote. Variavel lida na importacao seria
    // congelada no build — e uma chave congelada num pacote que vai para o navegador
    // deixaria de ser segredo.
    vi.stubEnv("EXPLANANDO_CHAVE_INTERNA", "primeira");
    const primeira = apiFalsa(10);
    await ranquearTudo(CASAS[0], "volume");
    expect(cabecalhosDe(primeira.fetchFalso)[0]!["X-Explanando-Interno"]).toBe("primeira");

    vi.unstubAllGlobals();
    vi.stubEnv("EXPLANANDO_CHAVE_INTERNA", "segunda");
    const segunda = apiFalsa(10);
    await ranquearTudo(CASAS[0], "volume");
    expect(cabecalhosDe(segunda.fetchFalso)[0]!["X-Explanando-Interno"]).toBe("segunda");
  });
});

// ── A trava de indexacao ─────────────────────────────────────────────────────────────────

describe("ehIndexavel", () => {
  it("por padrao, NAO indexa", () => {
    // O padrao seguro e' o inverso do desejado no fim. Um site incompleto indexado gasta a
    // primeira impressao do buscador — que e' o canal de aquisicao principal do produto — e
    // desfazer isso e' caro. Esquecer de LIGAR e' visivel; indexar cedo, nao.
    vi.stubEnv("NEXT_PUBLIC_INDEXAVEL", "");

    expect(ehIndexavel()).toBe(false);
  });

  it("indexa quando alguem diz explicitamente que pode", () => {
    vi.stubEnv("NEXT_PUBLIC_INDEXAVEL", "true");

    expect(ehIndexavel()).toBe(true);
  });

  it("so aceita 'true', e nao qualquer coisa que pareca verdadeira", () => {
    // "1", "sim", "yes" ligariam a indexacao por acidente de digitacao. Uma unica grafia
    // aceita torna a intencao inequivoca.
    for (const valor of ["1", "sim", "yes", "TRUE", "verdadeiro"]) {
      vi.stubEnv("NEXT_PUBLIC_INDEXAVEL", valor);
      expect(ehIndexavel()).toBe(false);
    }
  });

  it("le a variavel a cada chamada, para o build decidir e nao a importacao", () => {
    vi.stubEnv("NEXT_PUBLIC_INDEXAVEL", "true");
    expect(ehIndexavel()).toBe(true);

    vi.stubEnv("NEXT_PUBLIC_INDEXAVEL", "false");
    expect(ehIndexavel()).toBe(false);
  });
});

// ── As cores do grafo do Estado ─────────────────────────────────────────────────────────

describe("corDoPoder", () => {
  it("cada poder tem token proprio, e o desconhecido nao vira Executivo", () => {
    // A raiz e os entes chegam sem poder porque a fonte nao os classifica. Pinta-los de
    // Executivo inventaria uma classificacao que o Estado nao fez sobre si mesmo.
    const cores = [
      "Executivo",
      "Legislativo",
      "Judiciário",
      "Funções essenciais à Justiça",
      null,
    ].map(corDoPoder);

    expect(new Set(cores).size).toBe(5);
  });

  it("nenhuma cor de poder e' o vermelho de gravidade alta", () => {
    // ADR-0013: o vermelho fica estritamente com os 3,3% de gravidade alta. Um poder
    // pintado de vermelho faria o leitor ler "risco" onde nao ha' juizo nenhum.
    for (const poder of ["Executivo", "Legislativo", "Judiciário", null]) {
      expect(corDoPoder(poder)).not.toContain("alta");
    }
  });
});

describe("o mapa do Estado no sitemap", () => {
  it("esta la — a rota que nao esta no sitemap nao existe para o buscador", () => {
    const enderecos = rotasDoSitemap([], new Date()).map((r) => r.url);

    expect(enderecos).toContain(`${URL_BASE}/estado`);
  });
});

describe("as unidades do Estado no sitemap", () => {
  it("cada orgao ganha endereco proprio", () => {
    const enderecos = rotasDoSitemap([], new Date(), [26, 244]).map((r) => r.url);

    expect(enderecos).toContain(`${URL_BASE}/estado/26`);
    expect(enderecos).toContain(`${URL_BASE}/estado/244`);
  });

  it("nao repete unidade", () => {
    const rotas = rotasDoSitemap([], new Date(), [26, 26, 244]);
    const enderecos = rotas.map((r) => r.url);

    expect(new Set(enderecos).size).toBe(enderecos.length);
  });

  it("sem unidades, o sitemap continua completo", () => {
    // O grafo pode nao ter subido ainda para a base de producao. As rotas fixas e os
    // perfis nao podem sumir por causa disso.
    const enderecos = rotasDoSitemap(["a1"], new Date()).map((r) => r.url);

    expect(enderecos).toContain(`${URL_BASE}/estado`);
    expect(enderecos).toContain(`${URL_BASE}/parlamentares/a1`);
  });
});
