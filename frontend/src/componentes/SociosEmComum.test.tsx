import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { QuemBeneficiaQuem } from "@/lib/contrato";
import { SociosEmComum } from "./SociosEmComum";

/** "Quem beneficia quem" — a Fase 5. A ressalva e a ausência do nome do sócio são o contrato. */

const RESULTADO: QuemBeneficiaQuem = {
  totalDeCadeias: 1,
  parlamentaresMaximosPorSocio: 5,
  valorMinimoPorPonta: 50_000,
  ressalva: "Ter sócio em comum não é irregularidade.",
  cadeias: [
    {
      fornecedor: { documento: "43.783.350/0001-84", razaoSocial: "INSTITUTO A", valor: 519_700 },
      outraEmpresa: { documento: "55.292.471/0001-02", razaoSocial: "AGENCIA B", valor: 259_000 },
      sociosEmComum: 1,
      socios: [{ nome: "FULANO DE TAL", qualificacao: "Sócio-Administrador nas duas", doacoes: [] }],
      outrosParlamentares: [
        {
          parlamentar: {
            id: "b",
            nomeParlamentar: "Carlos Jordy",
            siglaPartido: "PL",
            uf: "RJ",
            casa: "Câmara dos Deputados",
            fotoUrl: null,
          },
          valor: 207_000,
        },
      ],
    },
  ],
};

describe("SociosEmComum", () => {
  it("desenha a cadeia inteira, com os dois CNPJs para conferir", () => {
    render(<SociosEmComum resultado={RESULTADO} parlamentarId="a" nome="Albuquerque" />);

    expect(screen.getByText("INSTITUTO A")).toBeInTheDocument();
    expect(screen.getByText("43.783.350/0001-84")).toBeInTheDocument();
    expect(screen.getByText("1 sócio em comum")).toBeInTheDocument();
    expect(screen.getByText("FULANO DE TAL")).toBeInTheDocument();
    expect(screen.getByText("Sócio-Administrador nas duas")).toBeInTheDocument();
    expect(screen.getByText("AGENCIA B")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Carlos Jordy" })).toHaveAttribute("href", "/parlamentares/b");
  });

  it("a ressalva vem antes das cadeias", () => {
    render(<SociosEmComum resultado={RESULTADO} parlamentarId="a" nome="Albuquerque" />);

    const ressalva = screen.getByText(RESULTADO.ressalva);
    const primeira = screen.getByText("INSTITUTO A");

    expect(ressalva.compareDocumentPosition(primeira) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("oferece a comparação com o parlamentar da outra ponta, na ordem canônica", () => {
    render(<SociosEmComum resultado={RESULTADO} parlamentarId="a" nome="Albuquerque" />);

    expect(screen.getByRole("link", { name: "comparar" })).toHaveAttribute("href", "/comparar/a/b");
  });

  it("sem cadeia, a seção não aparece", () => {
    const { container } = render(
      <SociosEmComum
        resultado={{ ...RESULTADO, cadeias: [], totalDeCadeias: 0 }}
        parlamentarId="a"
        nome="Albuquerque"
      />,
    );

    expect(container).toBeEmptyDOMElement();
  });
});
