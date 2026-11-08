import type { Metadata } from "next";
import { Bricolage_Grotesque, Figtree } from "next/font/google";
import "./globals.css";
import { Cabecalho } from "@/componentes/Cabecalho";
import { Rodape } from "@/componentes/Rodape";
import { URL_BASE, ehIndexavel } from "@/lib/site";
import { SCRIPT_DE_TEMA } from "@/componentes/AlternadorDeTema";

/*
  `latin-ext` além de `latin`: sem ele, "ã", "õ" e "ç" caem no fallback e a linha aparece com
  duas fontes misturadas — num produto em português isso acontece já no primeiro título.
*/
const corpo = Figtree({
  subsets: ["latin", "latin-ext"],
  variable: "--fonte-corpo",
  display: "swap",
});

/* Destaque, só para título e número grande. */
const destaque = Bricolage_Grotesque({
  subsets: ["latin", "latin-ext"],
  weight: ["600", "700"],
  variable: "--fonte-destaque",
  display: "swap",
});

export const metadata: Metadata = {
  // O template deixa cada página nomear a si mesma sem repetir a marca à mão — e 513 páginas
  // com o mesmo título competiriam entre si no índice.
  title: {
    default: "Explanando — gastos da cota parlamentar",
    template: "%s · Explanando",
  },
  description:
    "Dados públicos de gastos da cota parlamentar, com indicadores de um motor de regras determinístico. Descreve inconsistências; não imputa conduta.",

  // Base absoluta: sem ela o Next resolve os canônicos contra localhost no build.
  metadataBase: new URL(URL_BASE),

  // O X só mostra a imagem grande com este tipo de card; a imagem em si vem do
  // `opengraph-image` de cada perfil, que o X lê como fallback de `twitter:image`.
  twitter: { card: "summary_large_image" },

  /*
    Cinto e suspensório com o robots.txt.

    O `Disallow` impede o rastreamento, mas uma URL bloqueada ainda pode aparecer no índice
    sem conteúdo se alguém a linkar de fora. O `noindex` fecha esse caso. Os dois saem da
    mesma trava, então nunca discordam.
  */
  robots: ehIndexavel() ? undefined : { index: false, follow: false },
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html
      lang="pt-BR"
      className={`${corpo.variable} ${destaque.variable}`}
      /*
        O script abaixo mexe no `<html>` antes da hidratação, de propósito. Sem isto o React
        acusa divergência entre o que o servidor mandou e o que o navegador tem.
      */
      suppressHydrationWarning
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: SCRIPT_DE_TEMA }} />
      </head>
      <body className="antialiased">
        {/*
          Pular para o conteúdo: numa página que é essencialmente uma tabela densa, quem
          navega por teclado não deveria atravessar o cabeçalho a cada visita.
        */}
        <a
          href="#conteudo"
          className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-titulo focus:px-3 focus:py-2 focus:text-sobre-titulo"
        >
          Pular para o conteúdo
        </a>

        <Cabecalho />

        <div id="conteudo">{children}</div>

        <Rodape />
      </body>
    </html>
  );
}
