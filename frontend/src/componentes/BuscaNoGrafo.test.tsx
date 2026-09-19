import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import userEvent from "@testing-library/user-event";
import { BuscaNoGrafo } from "./BuscaNoGrafo";
import { contem, dobrar, ondeCasa } from "@/lib/texto";
import type { NoDoEstado } from "@/lib/contrato";

const no = (
  codigo: number,
  nome: string,
  sigla: string | null = null,
  unidadesSubordinadas = 0,
): NoDoEstado => ({
  codigo,
  codigoPai: null,
  codigoPaiNoMapa: null,
  nome,
  sigla,
  tipo: "Órgão",
  poder: "Executivo",
  chaveDoTipo: "orgao",
  chaveDoPoder: "executivo",
  anel: 1,
  natureza: null,
  baseLegal: null,
  unidadesSubordinadas,
  ativa: true,
  servidores: null,
  emFuncaoDeConfianca: null,
  servidoresNoRamo: null,
});

const base = [
  no(1, "Ministério da Educação", "MEC", 47946),
  no(2, "Ministério da Saúde", "MS", 2828),
  no(3, "Fundação Nacional dos Povos Indígenas", "FUNAI", 120),
  no(4, "Agência Nacional de Águas e Saneamento Básico", "ANA", 12),
];

describe("dobrar e contem", () => {
  it("acento e caixa nao distinguem", () => {
    // Quem digita "orgao" esta' procurando "órgão", e uma busca que nao acha e'
    // indistinguivel de um cadastro que nao tem.
    expect(dobrar("Órgão")).toBe("orgao");
    expect(contem("Ministério da Saúde", "saude")).toBe(true);
    expect(contem("Fundação", "FUNDACAO")).toBe(true);
  });

  it("termo que nao esta' no texto nao casa", () => {
    expect(contem("Ministério da Saúde", "educação")).toBe(false);
  });

  it("ondeCasa devolve indices sobre o texto ORIGINAL", () => {
    // E' o que permite destacar o trecho sem deslocar o acento.
    const faixa = ondeCasa("Ministério da Saúde", "saude")!;

    expect("Ministério da Saúde".slice(faixa[0], faixa[1])).toBe("Saúde");
  });

  it("termo vazio nao casa em lugar nenhum", () => {
    expect(ondeCasa("qualquer", "  ")).toBeNull();
  });
});

describe("BuscaNoGrafo", () => {
  function montar() {
    const aoEscolher = vi.fn();

    render(<BuscaNoGrafo nos={base} aoEscolher={aoEscolher} />);

    return { aoEscolher, usuario: userEvent.setup() };
  }

  it("comeca fechada, com o atalho a vista", async () => {
    montar();

    expect(screen.getByRole("button", { name: /Buscar um órgão/ })).toBeInTheDocument();
    expect(screen.queryByRole("searchbox")).toBeNull();
  });

  it("abre ao clicar e diz quantos orgaos ha' no mapa", async () => {
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));

    expect(screen.getByRole("searchbox")).toBeInTheDocument();
    expect(screen.getByText(/4 órgãos e entidades no mapa/)).toBeInTheDocument();
  });

  it("acha sem acento e sem caixa", async () => {
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "saude");

    expect(screen.getByText(/Ministério da/)).toBeInTheDocument();
  });

  it("acha pela sigla", async () => {
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "funai");

    expect(screen.getByText("FUNAI")).toBeInTheDocument();
  });

  it("uma letra so' nao busca", async () => {
    // Com uma letra, quase tudo casa, e a lista vira ruido.
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "m");

    expect(screen.getByText(/Digite ao menos duas letras/)).toBeInTheDocument();
  });

  it("o que comeca com o termo vem primeiro", async () => {
    // Quem digita "min" quer os ministerios, nao uma entidade com "min" no meio.
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "minist");

    const itens = screen.getAllByRole("button").filter((b) => b.textContent?.includes("MEC"));

    expect(itens.length).toBe(1);
  });

  it("escolher devolve o codigo e fecha", async () => {
    const { aoEscolher, usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "educa");
    await usuario.click(screen.getByText("MEC"));

    expect(aoEscolher).toHaveBeenCalledWith(1);
    expect(screen.queryByRole("searchbox")).toBeNull();
  });

  it("o teclado percorre e confirma", async () => {
    const { aoEscolher, usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "minist");
    await usuario.keyboard("{ArrowDown}{Enter}");

    // Dois ministerios na base; a seta desce para o segundo.
    expect(aoEscolher).toHaveBeenCalledWith(2);
  });

  it("sem resultado, diz que o recorte do mapa e' a causa possivel", async () => {
    // "Nao encontrado" faria o leitor concluir que o orgao nao existe. As 94 mil
    // unidades administrativas nao estao no mapa, e a ausencia e' do recorte.
    const { usuario } = montar();

    await usuario.click(screen.getByRole("button", { name: /Buscar um órgão/ }));
    await usuario.type(screen.getByRole("searchbox"), "zzzzz");

    expect(screen.getByText(/unidades administrativas não aparecem/)).toBeInTheDocument();
  });
});
