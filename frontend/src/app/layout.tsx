import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";

const inter = Inter({
  subsets: ["latin"],
  variable: "--fonte-inter",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Explanando — gastos da cota parlamentar",
  description:
    "Dados públicos de gastos da cota parlamentar, com indicadores de um motor de regras determinístico. Descreve inconsistências; não imputa conduta.",
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

        <footer className="mx-auto max-w-7xl border-t border-borda-suave px-4 py-6 text-xs text-texto-suave">
          <p>
            Dados obtidos das APIs de dados abertos da Câmara dos Deputados e da Receita
            Federal (via BrasilAPI). Os indicadores apontam inconsistências entre registros
            públicos e <strong>não caracterizam, por si, irregularidade</strong>.
          </p>
        </footer>
      </body>
    </html>
  );
}
