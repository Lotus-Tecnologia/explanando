import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { TagDeGravidade } from "./TagDeGravidade";
import { BlocoDeCobertura, TagDeAbstencao } from "./Abstencao";
import { Comprovante, ValoresDaDespesa } from "./Comprovante";
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
    expect(screen.getByText(/fonte não disponibiliza o comprovante/i)).toBeInTheDocument();
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
