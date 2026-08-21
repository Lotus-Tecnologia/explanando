import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import userEvent from "@testing-library/user-event";
import { TagDeGravidade } from "./TagDeGravidade";
import { BlocoDeCobertura, TagDeAbstencao } from "./Abstencao";
import { Comprovante, ValoresDaDespesa } from "./Comprovante";
import { Aviso } from "./Aviso";
import { TabelaDoRanking } from "./TabelaDoRanking";
import { Paginacao } from "./Paginacao";
import { BarraDeFiltros } from "./BarraDeFiltros";
import { ListaDeDespesas } from "./ListaDeDespesas";
import { SerieMensal } from "./SerieMensal";
import type { MesDoParlamentar } from "@/lib/contrato";
import { lerFiltros } from "@/lib/filtros";
import type { ItemDoRanking } from "@/lib/contrato";
import type { CoberturaDeRegra, ItemDeDespesa } from "@/lib/contrato";

/**
 * Os quatro componentes que o ADR-0013 tornou obrigatórios.
 *
 * O que se prova aqui não é aparência — é que a interface **não afirma o que o motor não
 * afirmou**. Cada teste corresponde a uma forma de a tela mentir sem que uma palavra esteja
 * errada.
 */

function despesa(ajustes: Partial<ItemDeDespesa> = {}): ItemDeDespesa {
  return {
    id: "d1",
    ano: 2026,
    mes: 7,
    tipoDespesa: "COMBUSTÍVEIS E LUBRIFICANTES.",
    dataDocumento: "2026-07-06T00:00:00Z",
    nomeFornecedor: "AUTO POSTO BOMTEMPO LTDA",
    documentoFornecedor: "36.826.246/0001-64",
    valorLiquido: 385.92,
    valorDocumento: 385.92,
    valorGlosa: 0,
    temDocumentoPublicado: true,
    urlDocumento: "https://www.camara.leg.br/nota.pdf",
    ehEstorno: false,
    flags: [],
    maiorGravidade: null,
    ...ajustes,
  };
}

function regra(ajustes: Partial<CoberturaDeRegra> = {}): CoberturaDeRegra {
  return {
    codigo: "REG_KYB_001",
    situacao: "SemAchados",
    despesasComDado: 100,
    despesasSemDado: 0,
    flagsEmitidas: 0,
    silencioEhConclusivo: true,
    motivoDaAbstencao: null,
    ...ajustes,
  };
}

describe("TagDeGravidade", () => {
  it.each([
    ["Baixa", "baixa relevância"],
    ["Media", "relevância média"],
    ["Alta", "alta relevância"],
  ] as const)("descreve %s para leitor de tela", (gravidade, esperado) => {
    render(<TagDeGravidade gravidade={gravidade} />);

    expect(screen.getByRole("status")).toHaveAccessibleName(
      expect.stringContaining(esperado) as unknown as string,
    );
  });

  it("inclui o código da regra no nome acessível", () => {
    // Ícone sem rótulo não existe para leitor de tela — e é quem mais depende da forma.
    render(<TagDeGravidade gravidade="Alta" codigoRegra="REG_KYB_002" />);

    expect(screen.getByRole("status")).toHaveAccessibleName(
      expect.stringContaining("REG_KYB_002") as unknown as string,
    );
  });

  it("nao usa caixa alta", () => {
    // Caixa alta em texto curto é lida como ênfase, e ênfase sobre achado descritivo é
    // imputação pela tipografia.
    const { container } = render(<TagDeGravidade gravidade="Alta" />);

    expect(container.innerHTML).not.toContain("uppercase");
  });

  it("so a gravidade alta usa vermelho", () => {
    // 43,3% dos achados são de gravidade baixa. Pintá-los de vermelho afirmaria sobre 20 mil
    // deles uma severidade que o motor não atribuiu.
    const alta = render(<TagDeGravidade gravidade="Alta" />).container.innerHTML;
    const media = render(<TagDeGravidade gravidade="Media" />).container.innerHTML;
    const baixa = render(<TagDeGravidade gravidade="Baixa" />).container.innerHTML;

    expect(alta).toContain("alta-fundo");
    expect(media).not.toContain("alta-fundo");
    expect(baixa).not.toContain("alta-fundo");
  });

  it("cada gravidade tem forma propria, e nao so cor", () => {
    // Matiz sozinho falha para ~8% dos homens, que têm deficiência de visão de cor.
    const alta = render(<TagDeGravidade gravidade="Alta" />).container.innerHTML;
    const media = render(<TagDeGravidade gravidade="Media" />).container.innerHTML;
    const baixa = render(<TagDeGravidade gravidade="Baixa" />).container.innerHTML;

    expect(alta).toContain("clip-path");
    expect(media).toContain("clip-path");
    expect(baixa).toContain("rounded-full");
    expect(alta).not.toBe(media);
  });
});

describe("TagDeAbstencao", () => {
  it("diz nao avaliado, e carrega o motivo no nome acessivel", () => {
    render(<TagDeAbstencao motivo="não há teto de cota publicado para o período" />);

    expect(screen.getByText("Não avaliado")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAccessibleName(
      expect.stringContaining("teto de cota") as unknown as string,
    );
  });
});

describe("BlocoDeCobertura", () => {
  it("distingue regra abstida de regra limpa, ambas com zero achados", () => {
    // A distinção inteira do produto. As duas produzem zero e significam coisas opostas.
    render(
      <BlocoDeCobertura
        regrasSemDado={1}
        cobertura={[
          regra({ codigo: "REG_TX_001", situacao: "SemAchados", silencioEhConclusivo: true }),
          regra({
            codigo: "REG_TX_002",
            situacao: "SemDado",
            silencioEhConclusivo: false,
            despesasComDado: 0,
            despesasSemDado: 100,
            motivoDaAbstencao: "não há teto de cota publicado para o período",
          }),
        ]}
      />,
    );

    const limpa = screen.getByText("REG_TX_001").closest("li")!;
    const abstida = screen.getByText("REG_TX_002").closest("li")!;

    expect(within(limpa).getByText("Sem indicadores")).toBeInTheDocument();
    expect(within(limpa).queryByText("Não avaliado")).not.toBeInTheDocument();

    expect(within(abstida).getByText("Não avaliado")).toBeInTheDocument();
    expect(within(abstida).queryByText("Sem indicadores")).not.toBeInTheDocument();
  });

  it("avisa que ausencia de indicador nao e ausencia de achado", () => {
    render(<BlocoDeCobertura regrasSemDado={2} cobertura={[regra()]} />);

    expect(
      screen.getByText(/não significa ausência de achado/i),
    ).toBeInTheDocument();
  });

  it("quando tudo foi avaliado, nao inventa ressalva", () => {
    render(<BlocoDeCobertura regrasSemDado={0} cobertura={[regra()]} />);

    expect(
      screen.queryByText(/não significa ausência de achado/i),
    ).not.toBeInTheDocument();
    expect(screen.getByText(/puderam ser aplicadas a todas/i)).toBeInTheDocument();
  });
});

describe("Comprovante", () => {
  it("com documento publicado, oferece o link", () => {
    render(<Comprovante despesa={despesa()} />);

    const link = screen.getByRole("link", { name: /ver nota fiscal/i });
    expect(link).toHaveAttribute("href", "https://www.camara.leg.br/nota.pdf");
  });

  it("sem documento, atribui a ausencia a fonte e NAO oferece link", () => {
    // O espaço vazio onde o link deveria estar seria lido como falha da plataforma. E um
    // link que dá 404 afirma que o documento sumiu.
    render(
      <Comprovante
        despesa={despesa({ temDocumentoPublicado: false, urlDocumento: null })}
      />,
    );

    expect(screen.queryByRole("link")).not.toBeInTheDocument();
    expect(screen.getByText(/fonte oficial não disponibiliza o comprovante/i)).toBeInTheDocument();
  });
});

describe("ValoresDaDespesa", () => {
  it("com glosa desconhecida, OMITE a quebra de valores", () => {
    // ADR-0012 §2: exibir R$ 0,00 diria que houve conferência e nada foi cortado —
    // afirmação que ninguém fez.
    render(<ValoresDaDespesa despesa={despesa({ valorGlosa: null })} />);

    expect(screen.queryByText(/glosa/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/declarado/i)).not.toBeInTheDocument();
  });

  it("com glosa conhecida e zero, tambem omite", () => {
    // Zero é informação — houve conferência e nada foi cortado —, mas exibir a linha para
    // toda despesa da Câmara seria ruído em 99% dos casos.
    render(<ValoresDaDespesa despesa={despesa({ valorGlosa: 0 })} />);

    expect(screen.queryByText(/glosa/i)).not.toBeInTheDocument();
  });

  it("com corte de verdade, mostra declarado e glosa", () => {
    render(
      <ValoresDaDespesa
        despesa={despesa({ valorDocumento: 500, valorLiquido: 400, valorGlosa: 100 })}
      />,
    );

    expect(screen.getByText(/declarado/i)).toBeInTheDocument();
  });

  it("marca o estorno", () => {
    // Escondê-lo inflaria o gasto exibido: passagem comprada e cancelada apareceria como
    // gasta.
    render(
      <ValoresDaDespesa despesa={despesa({ valorLiquido: -500, ehEstorno: true })} />,
    );

    expect(screen.getByText("estorno")).toBeInTheDocument();
  });
});

describe("Aviso", () => {
  it("anuncia o titulo como cabecalho da pagina", () => {
    render(<Aviso titulo="Parlamentar não encontrado">Nada aqui.</Aviso>);

    expect(
      screen.getByRole("heading", { name: "Parlamentar não encontrado", level: 1 }),
    ).toBeInTheDocument();
  });

  it("sempre oferece a volta para o ranking", () => {
    // Um beco sem saída num site cujo tráfego vem de link compartilhado devolve a pessoa
    // para a aba anterior, e ela nunca descobre que o resto existe.
    render(<Aviso titulo="Algo falhou">Detalhe.</Aviso>);

    expect(screen.getByRole("link", { name: /ranking/i })).toHaveAttribute("href", "/");
  });

  it("nao afirma nada sobre o parlamentar quando a falha e nossa", () => {
    render(
      <Aviso titulo="Não conseguimos carregar esta página">
        A falha é da plataforma, não do dado.
      </Aviso>,
    );

    expect(screen.getByText(/falha é da plataforma/i)).toBeInTheDocument();
  });
});

function doRanking(ajustes: Partial<ItemDoRanking> = {}): ItemDoRanking {
  return {
    id: "p1",
    idExterno: 1,
    nomeParlamentar: "MARIA DA SILVA",
    siglaPartido: "PT",
    uf: "SP",
    fotoUrl: null,
    volumeTotalGasto: 1000,
    despesasAvaliadas: 150,
    indiceDeRisco: 3.21,
    indiceDeAltas: 0.5,
    flagsAltas: 1,
    flagsMedias: 2,
    flagsBaixas: 3,
    amostraSuficiente: true,
    ...ajustes,
  };
}

const TRES = [
  doRanking({ id: "p1", nomeParlamentar: "MARIA DA SILVA", siglaPartido: "PT", uf: "SP" }),
  doRanking({ id: "p2", nomeParlamentar: "JOÃO ANDRÉ", siglaPartido: "PL", uf: "AP" }),
  doRanking({ id: "p3", nomeParlamentar: "ANA PAULA", siglaPartido: "PSOL", uf: "MG" }),
];

describe("TabelaDoRanking", () => {
  it("mostra todos os parlamentares recebidos", () => {
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    expect(screen.getAllByRole("row")).toHaveLength(TRES.length + 1);
  });

  it("filtra por nome enquanto se digita", async () => {
    const usuario = userEvent.setup();
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    await usuario.type(screen.getByRole("searchbox"), "ana");

    expect(screen.getByText("ANA PAULA")).toBeInTheDocument();
    expect(screen.queryByText("MARIA DA SILVA")).not.toBeInTheDocument();
  });

  it("ignora acento na busca, porque ninguem digita JOAO com til", async () => {
    const usuario = userEvent.setup();
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    await usuario.type(screen.getByRole("searchbox"), "joao andre");

    expect(screen.getByText("JOÃO ANDRÉ")).toBeInTheDocument();
  });

  it("acha por partido e por UF, nao so por nome", async () => {
    const usuario = userEvent.setup();
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    await usuario.type(screen.getByRole("searchbox"), "psol");

    expect(screen.getByText("ANA PAULA")).toBeInTheDocument();
    expect(screen.queryByText("JOÃO ANDRÉ")).not.toBeInTheDocument();
  });

  it("preserva a posicao original no ranking ao filtrar", async () => {
    // A posicao e' a informacao; renumerar de 1 diria que a pessoa filtrada e' a primeira
    // colocada, que e' exatamente o oposto do que o numero significa.
    const usuario = userEvent.setup();
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    await usuario.type(screen.getByRole("searchbox"), "ana paula");

    const linha = screen.getByText("ANA PAULA").closest("tr")!;
    expect(within(linha).getAllByRole("cell")[0]).toHaveTextContent("3");
  });

  it("diz que a busca nao achou ninguem, e nao que nao ha gasto", async () => {
    const usuario = userEvent.setup();
    render(<TabelaDoRanking itens={TRES} rotulo="Volume gasto" />);

    await usuario.type(screen.getByRole("searchbox"), "zzzz");

    expect(screen.getByText(/nenhum parlamentar/i)).toBeInTheDocument();
  });

  it("nao escreve zero onde a densidade nao foi medida", () => {
    // Abaixo do piso amostral do ADR-0009 o valor vem nulo, e nulo nao e' zero: zero diria
    // que a densidade foi medida e deu nada.
    render(
      <TabelaDoRanking
        itens={[doRanking({ indiceDeRisco: null, indiceDeAltas: null, amostraSuficiente: false })]}
        rotulo="Volume gasto"
      />,
    );

    expect(screen.getByText(/amostra insuficiente/i)).toBeInTheDocument();
    expect(screen.queryByText("0,00")).not.toBeInTheDocument();
  });
});

describe("Paginacao", () => {
  const base = "/parlamentares/p1/despesas";
  const filtros = (pagina: number) =>
    lerFiltros({ ano: "2025", pagina: String(pagina) }, [2025], []);

  it("diz em que pagina se esta, e de quantas", () => {
    render(<Paginacao base={base} filtros={filtros(3)} totalDePaginas={7} />);

    expect(screen.getByText(/pagina 3 de 7|página 3 de 7/i)).toBeInTheDocument();
  });

  it("na primeira pagina, nao oferece anterior", () => {
    render(<Paginacao base={base} filtros={filtros(1)} totalDePaginas={7} />);

    expect(screen.queryByRole("link", { name: /anterior/i })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /pr[oó]xima/i })).toBeInTheDocument();
  });

  it("na ultima pagina, nao oferece proxima", () => {
    render(<Paginacao base={base} filtros={filtros(7)} totalDePaginas={7} />);

    expect(screen.getByRole("link", { name: /anterior/i })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /pr[oó]xima/i })).not.toBeInTheDocument();
  });

  it("carrega os filtros junto ao mudar de pagina", () => {
    // Perder o filtro ao paginar joga o leitor de volta na lista inteira sem avisar.
    render(<Paginacao base={base} filtros={filtros(2)} totalDePaginas={7} />);

    const proxima = screen.getByRole("link", { name: /pr[oó]xima/i });

    expect(proxima).toHaveAttribute("href", expect.stringContaining("ano=2025"));
    expect(proxima).toHaveAttribute("href", expect.stringContaining("pagina=3"));
  });

  it("some quando ha uma pagina so", () => {
    const { container } = render(
      <Paginacao base={base} filtros={filtros(1)} totalDePaginas={1} />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});

describe("BarraDeFiltros", () => {
  const base = "/parlamentares/p1/despesas";
  const anos = [2026, 2025, 2024];
  const tipos = ["COMBUSTÍVEIS E LUBRIFICANTES.", "TELEFONIA"];

  function montar(params: Record<string, string> = {}) {
    return (
      <BarraDeFiltros
        base={base}
        anos={anos}
        tipos={tipos}
        filtros={lerFiltros(params, anos, tipos)}
      />
    );
  }

  it("oferece so os anos em que este parlamentar gastou", () => {
    render(montar());

    const seletor = screen.getByLabelText(/ano/i);

    expect(within(seletor).getByRole("option", { name: "2024" })).toBeInTheDocument();
    expect(within(seletor).queryByRole("option", { name: "2019" })).not.toBeInTheDocument();
  });

  it("oferece so os tipos que este parlamentar teve", () => {
    // Uma opcao que devolve lista vazia faz o leitor concluir que nao houve gasto, quando o
    // que houve foi opcao mal montada. Por isso a lista vem da API, nao de constante local.
    render(montar());

    const seletor = screen.getByLabelText(/tipo de despesa/i);

    expect(within(seletor).getByRole("option", { name: "TELEFONIA" })).toBeInTheDocument();
    expect(within(seletor).getAllByRole("option")).toHaveLength(tipos.length + 1);
  });

  it("mostra selecionado o filtro que de fato foi aplicado", () => {
    render(montar({ ano: "2025", tipo: "TELEFONIA" }));

    expect(screen.getByLabelText(/ano/i)).toHaveValue("2025");
    expect(screen.getByLabelText(/tipo de despesa/i)).toHaveValue("TELEFONIA");
  });

  it("volta a exibir 'todos' quando a URL trouxe um valor impossivel", () => {
    // O que a tela mostra tem de ser o filtro aplicado. Exibir 1998 selecionado enquanto o
    // resultado ignora o ano seria a tela mentindo sobre o proprio estado.
    render(montar({ ano: "1998" }));

    expect(screen.getByLabelText(/ano/i)).toHaveValue("");
  });

  it("nao oferece limpar quando nao ha o que limpar", () => {
    render(montar());

    expect(screen.queryByRole("link", { name: /limpar/i })).not.toBeInTheDocument();
  });

  it("oferece limpar assim que ha filtro aplicado", () => {
    render(montar({ indicador: "com" }));

    expect(screen.getByRole("link", { name: /limpar/i })).toHaveAttribute("href", base);
  });

  it("funciona sem JavaScript, porque e um formulario GET de verdade", () => {
    const { container } = render(montar());
    const formulario = container.querySelector("form")!;

    expect(formulario).toHaveAttribute("method", "get");
    expect(formulario).toHaveAttribute("action", base);
  });
});

describe("Comprovante — a ausencia atribuida a fonte certa (ADR-0012 §1)", () => {
  it("atribui a ausencia ao Senado nominalmente", () => {
    // "A fonte nao disponibiliza" e' generico, e generico devolve a suspeita de falha para a
    // plataforma. O ADR-0012 §1 exige dizer de quem e' a opacidade.
    render(
      <Comprovante
        despesa={despesa({ urlDocumento: null, temDocumentoPublicado: false })}
        casa="Senado Federal"
      />,
    );

    expect(screen.getByText(/senado/i)).toBeInTheDocument();
  });

  it("na Camara, diz que aquela nota especifica nao foi publicada", () => {
    // A Camara publica 70,5% das notas: a ausencia aqui e' da nota, nao da casa. Dizer
    // "a Camara nao disponibiliza" seria falso.
    render(
      <Comprovante
        despesa={despesa({ urlDocumento: null, temDocumentoPublicado: false })}
        casa="Câmara dos Deputados"
      />,
    );

    expect(screen.queryByText(/senado/i)).not.toBeInTheDocument();
    expect(screen.getByText(/não (foi )?publicad/i)).toBeInTheDocument();
  });

  it("sem saber a casa, nao inventa culpado", () => {
    render(<Comprovante despesa={despesa({ urlDocumento: null })} />);

    expect(screen.getByText(/fonte oficial|fonte não/i)).toBeInTheDocument();
  });
});

describe("ListaDeDespesas", () => {
  it("leva cada despesa ao endereco proprio dela", () => {
    // E' o endereco que um link compartilhado abre, e onde o documento oficial fica ao
    // alcance do clique — a peca em que o ADR-0004 se apoia.
    render(<ListaDeDespesas despesas={[despesa({ id: "d9" })]} />);

    expect(screen.getByRole("link", { name: /detalhe|abrir|ver despesa/i })).toHaveAttribute(
      "href",
      "/despesas/d9",
    );
  });

  it("exibe o indicador com a tag do nivel certo", () => {
    render(
      <ListaDeDespesas
        despesas={[
          despesa({
            flags: [
              {
                codigoRegra: "REG_KYB_003",
                gravidade: "Alta",
                descricao: "Empresa com 43 dias de atividade.",
                grauDeConfianca: 0.9,
                versaoDoMotor: 3,
              },
            ],
            maiorGravidade: "Alta",
          }),
        ]}
      />,
    );

    expect(screen.getByLabelText(/alta relevância, regra REG_KYB_003/i)).toBeInTheDocument();
  });

  it("diz que a lista esta vazia sem afirmar que nao houve gasto", () => {
    render(<ListaDeDespesas despesas={[]} />);

    expect(screen.getByText(/nenhuma despesa corresponde/i)).toBeInTheDocument();
  });

  it("aceita outra explicacao para o vazio, porque nem todo vazio tem a mesma causa", () => {
    // Pagina fora da faixa devolve zero item com o filtro casando centenas. Dizer "nenhuma
    // despesa corresponde" ali seria falso sobre o filtro.
    render(
      <ListaDeDespesas despesas={[]} mensagemVazia="Esta página não existe para o filtro." />,
    );

    expect(screen.getByText(/esta página não existe/i)).toBeInTheDocument();
    expect(screen.queryByText(/nenhuma despesa corresponde/i)).not.toBeInTheDocument();
  });
});

function mes(ajustes: Partial<MesDoParlamentar> = {}): MesDoParlamentar {
  return {
    ano: 2026,
    mes: 1,
    totalLiquido: 10_000,
    quantidadeDeDespesas: 20,
    totalGlosa: 0,
    detalheDisponivel: true,
    ...ajustes,
  };
}

describe("SerieMensal", () => {
  it("desenha uma barra por competencia", () => {
    render(
      <SerieMensal
        serie={[mes({ mes: 1 }), mes({ mes: 2 }), mes({ mes: 3 })]}
        idDoParlamentar="p1"
      />,
    );

    expect(screen.getAllByRole("row")).toHaveLength(4);
  });

  it("tem alternativa textual, porque grafico sozinho nao existe para leitor de tela", () => {
    render(<SerieMensal serie={[mes({ mes: 5, totalLiquido: 12_345.67 })]} idDoParlamentar="p1" />);

    expect(screen.getByText(/12.345,67/)).toBeInTheDocument();
    expect(screen.getByText("05/2026")).toBeInTheDocument();
  });

  it("omite a glosa por inteiro quando nenhuma competencia a informa (ADR-0012 §2)", () => {
    // Exibir R$ 0,00 de glosa afirmaria que houve conferencia e nada foi cortado — o Senado
    // inteiro cai aqui, e ninguem fez essa afirmacao.
    render(
      <SerieMensal
        serie={[mes({ totalGlosa: null }), mes({ mes: 2, totalGlosa: null })]}
        idDoParlamentar="p1"
      />,
    );

    expect(screen.queryByText(/glosa/i)).not.toBeInTheDocument();
  });

  it("exibe a glosa quando ela e conhecida", () => {
    render(<SerieMensal serie={[mes({ totalGlosa: 250 })]} idDoParlamentar="p1" />);

    expect(screen.getAllByText(/glosa/i).length).toBeGreaterThan(0);
  });

  it("avisa quando o detalhe do mes nao existe mais", () => {
    // Sem esse aviso a tela ofereceria "ver as notas deste mes" para um mes ja podado.
    render(<SerieMensal serie={[mes({ detalheDisponivel: false })]} idDoParlamentar="p1" />);

    expect(screen.getByText(/detalhe não disponível|sem detalhe/i)).toBeInTheDocument();
  });

  it("nao desenha nada quando nao ha serie", () => {
    const { container } = render(<SerieMensal serie={[]} idDoParlamentar="p1" />);

    expect(container).toBeEmptyDOMElement();
  });
});
