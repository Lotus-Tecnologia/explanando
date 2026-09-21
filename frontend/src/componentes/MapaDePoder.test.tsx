import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { MapaDePoder, ProcedenciaDoMapa } from "./MapaDePoder";
import type { CadeiraDoMapa, MapaDePoder as Mapa } from "@/lib/contrato";

/**
 * O mapa de poder.
 *
 * **Esta é a única tela do produto que mostra nome de pessoa**, e os testes guardam o que
 * torna isso defensável: que a procedência apareça, que a data seja a do pior caso, que
 * cadeira vaga não vire pessoa, e que um poder sem ocupantes explique por quê em vez de
 * simplesmente sumir.
 */
describe("MapaDePoder", () => {
  const cadeira = (parcial: Partial<CadeiraDoMapa> = {}): CadeiraDoMapa => ({
    cargo: "Ministro de Estado da Fazenda",
    ocupante: "Fulano de Tal",
    orgao: "Ministério da Fazenda",
    partido: null,
    uf: null,
    fotoUrl: null,
    codigoSiorg: null,
    fonte: "https://www.gov.br/planalto/exemplo",
    verificadoEm: "2026-09-20T12:00:00Z",
    vaga: false,
    curada: false,
    ...parcial,
  });

  const mapa = (parcial: Partial<Mapa> = {}): Mapa => ({
    chaveDoPoder: "executivo",
    poder: "Executivo",
    camadas: [
      {
        chave: "chefia",
        rotulo: "Chefia de Estado e de Governo",
        ordem: 0,
        cadeiras: [cadeira({ cargo: "Presidente da República", ocupante: "Sicrana" })],
      },
      {
        chave: "cupula",
        rotulo: "Ministros de Estado",
        ordem: 1,
        cadeiras: [cadeira()],
      },
    ],
    totalDeCadeiras: 2,
    vagas: 0,
    verificadoDesde: "2026-09-20T12:00:00Z",
    porQueFalta: null,
    ...parcial,
  });

  it("desenha as camadas com o nome do vocabulario DESTE poder", () => {
    // "Ministros de Estado" e nao "Cupula". No Brasil "ministro" significa duas coisas, e
    // o mapa do Estado existe justamente para desfazer isso.
    render(<MapaDePoder mapa={mapa()} />);

    expect(screen.getByText(/Chefia de Estado e de Governo/)).toBeInTheDocument();
    expect(screen.getByText(/Ministros de Estado/)).toBeInTheDocument();
    expect(screen.queryByText(/Cúpula/)).toBeNull();
  });

  it("mostra o ocupante e o cargo dele", () => {
    render(<MapaDePoder mapa={mapa()} />);

    expect(screen.getByText("Sicrana")).toBeInTheDocument();
    expect(screen.getByText("Presidente da República")).toBeInTheDocument();
  });

  it("cadeira VAGA nao vira pessoa", () => {
    // ADR-0009 aplicado a gente: "vaga" e' um fato sobre a cadeira, e desenhar um retrato
    // com as iniciais do CARGO faria parecer que ha' alguem ali.
    render(
      <MapaDePoder
        mapa={mapa({
          camadas: [
            {
              chave: "cupula",
              rotulo: "Ministros de Estado",
              ordem: 1,
              cadeiras: [cadeira({ ocupante: null, vaga: true })],
            },
          ],
        })}
      />,
    );

    expect(screen.getByText("Vaga")).toBeInTheDocument();
    expect(screen.getByText("Sem ocupante")).toBeInTheDocument();
  });

  it("sem retrato, aparecem as iniciais do NOME — e nao um buraco", () => {
    render(<MapaDePoder mapa={mapa()} />);

    expect(screen.getByText("FT")).toBeInTheDocument();
  });

  it("a cadeira transcrita a mao vem marcada como tal", () => {
    // Uma linha que uma API atualiza sozinha e uma que alguem copiou de um portal tem
    // confiabilidades diferentes, e o leitor tem direito de saber qual esta' olhando.
    render(
      <MapaDePoder
        mapa={mapa({
          camadas: [
            {
              chave: "cupula",
              rotulo: "Ministros do Supremo",
              ordem: 1,
              cadeiras: [cadeira({ curada: true })],
            },
          ],
        })}
      />,
    );

    expect(screen.getByText(/transcrito à mão/i)).toBeInTheDocument();
  });

  it("poder sem cadeira EXPLICA a ausencia, em vez de sumir", () => {
    // Omitir o Judiciario faria parecer que ele nao tem comando. O que existe e' uma fonte
    // que nao publica, e isso e' outra afirmacao.
    render(
      <MapaDePoder
        mapa={mapa({
          poder: "Judiciário",
          camadas: [],
          totalDeCadeiras: 0,
          verificadoDesde: null,
          porQueFalta: "O SIORG não publica quem ocupa cada cargo.",
        })}
      />,
    );

    expect(screen.getByText(/Não sabemos quem ocupa/)).toBeInTheDocument();
    expect(screen.getByText(/O SIORG não publica/)).toBeInTheDocument();
  });
});

describe("ProcedenciaDoMapa", () => {
  const base: Mapa = {
    chaveDoPoder: "executivo",
    poder: "Executivo",
    camadas: [
      {
        chave: "cupula",
        rotulo: "Ministros de Estado",
        ordem: 1,
        cadeiras: [
          {
            cargo: "Ministro A",
            ocupante: "Fulano",
            orgao: null,
            partido: null,
            uf: null,
            fotoUrl: null,
            codigoSiorg: null,
            fonte: "https://www.gov.br/planalto/ministros",
            verificadoEm: "2026-09-20T12:00:00Z",
            vaga: false,
            curada: false,
          },
        ],
      },
    ],
    totalDeCadeiras: 1,
    vagas: 0,
    verificadoDesde: "2026-09-20T12:00:00Z",
    porQueFalta: null,
  };

  it("diz que a data e' a leitura mais ANTIGA, e nao a mais recente", () => {
    // O pior caso da tela e' o que o leitor precisa saber. A data mais nova mascararia uma
    // camada parada ha' meses.
    render(<ProcedenciaDoMapa mapa={base} ressalva="…" />);

    expect(screen.getByText(/mais antiga do conjunto/)).toBeInTheDocument();
  });

  it("lista as fontes, como links de verdade", () => {
    render(<ProcedenciaDoMapa mapa={base} ressalva="…" />);

    const fonte = screen.getByRole("link", { name: /gov\.br\/planalto/ });

    expect(fonte).toHaveAttribute("href", "https://www.gov.br/planalto/ministros");
    expect(fonte).toHaveAttribute("rel", expect.stringContaining("noopener"));
  });

  it("a ressalva da curadoria so' aparece quando ha' linha curada", () => {
    const { rerender } = render(<ProcedenciaDoMapa mapa={base} ressalva="Confira a data." />);

    expect(screen.queryByText("Confira a data.")).toBeNull();

    const comCurada: Mapa = {
      ...base,
      camadas: [
        {
          ...base.camadas[0],
          cadeiras: [{ ...base.camadas[0].cadeiras[0], curada: true }],
        },
      ],
    };

    rerender(<ProcedenciaDoMapa mapa={comCurada} ressalva="Confira a data." />);

    expect(screen.getByText("Confira a data.")).toBeInTheDocument();
  });

  it("poder vazio nao ganha rodape de procedencia", () => {
    const { container } = render(
      <ProcedenciaDoMapa
        mapa={{ ...base, camadas: [], totalDeCadeiras: 0, verificadoDesde: null }}
        ressalva="…"
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it("conta as vagas quando ha' alguma", () => {
    render(<ProcedenciaDoMapa mapa={{ ...base, vagas: 3 }} ressalva="…" />);

    const rodape = screen.getByText(/3 vagas/);

    expect(within(rodape).queryByText("0 vagas")).toBeNull();
  });
});
