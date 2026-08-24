import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import { Rodape } from "@/componentes/Rodape";
import { URL_BASE, ehIndexavel } from "@/lib/site";

const inter = Inter({
  subsets: ["latin"],
  variable: "--fonte-inter",
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
    <html lang="pt-BR" className={inter.variable}>
      <body className="antialiased">
        {/*
          Pular para o conteúdo: numa página que é essencialmente uma tabela densa, quem
          navega por teclado não deveria atravessar o cabeçalho a cada visita.
        */}
        <a
          href="#conteudo"
          className="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:bg-titulo focus:px-3 focus:py-2 focus:text-white"
        >
          Pular para o conteúdo
        </a>

        <div id="conteudo">{children}</div>

        <Rodape />
      </body>
    </html>
  );
}
