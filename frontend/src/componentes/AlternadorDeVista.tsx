import Link from "next/link";

/**
 * O par de vistas do mesmo contexto: as **instituições** e as **pessoas**.
 *
 * É o `Graph | Power map` da referência (`docs/referencias/civlab.md`), e o motivo de
 * existir é o mesmo: as duas telas respondem à mesma pergunta por ângulos opostos, e
 * separadas no menu de cima elas parecem duas seções sem relação. Um controle único, em
 * cima do próprio desenho, diz que são duas faces de uma coisa só.
 *
 * **Os rótulos dizem o conteúdo, e não a forma.** "Grafo" e "Rede" descreveriam o tipo de
 * desenho — que é o mesmo nos dois — e não o que se vai ver. Quem chega quer saber se a
 * tela fala de órgãos ou de gente.
 *
 * São dois `<a href>` de verdade, sem estado no cliente: a vista ativa é a página em que
 * se está, e não uma variável.
 */
export function AlternadorDeVista({ atual }: { atual: "instituicoes" | "pessoas" }) {
  return (
    <div
      className="inline-flex rounded-selo border border-borda-suave bg-surface p-0.5"
      role="group"
      aria-label="Vista do mapa"
    >
      <Botao
        href="/estado"
        rotulo="Instituições"
        descricao="Os órgãos e entidades da administração federal"
        ativo={atual === "instituicoes"}
      />
      <Botao
        href="/rede"
        rotulo="Pessoas"
        descricao="Os parlamentares que dividem fornecedores entre si"
        ativo={atual === "pessoas"}
      />
    </div>
  );
}

function Botao({
  href,
  rotulo,
  descricao,
  ativo,
}: {
  href: string;
  rotulo: string;
  descricao: string;
  ativo: boolean;
}) {
  return (
    <Link
      href={href}
      title={descricao}
      /*
        `aria-current` e nao `aria-pressed`: nao e' um botao de liga-desliga, e' a pagina em
        que o leitor esta'. E' o que o leitor de tela precisa ouvir.
      */
      aria-current={ativo ? "page" : undefined}
      className={
        ativo
          ? "rounded-selo bg-realce px-3.5 py-1.5 text-sm font-semibold text-titulo"
          : "rounded-selo px-3.5 py-1.5 text-sm font-semibold text-texto-suave hover:text-titulo focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      }
    >
      {rotulo}
    </Link>
  );
}
