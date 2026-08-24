import type { Metadata } from "next";
import { PoliticaDePrivacidade } from "@/componentes/PoliticaDePrivacidade";

export const metadata: Metadata = {
  title: "Privacidade e Direitos LGPD",
  description:
    "Que dados pessoais o Explanando trata, sob qual base legal, o que não é coletado, e como exercer seus direitos. Encarregado de Dados identificado.",
  alternates: { canonical: "/privacidade" },
};

/**
 * Estática e sem revalidação: o texto muda por decisão registrada em ADR, não por dado.
 *
 * É condição de publicação, não item de backlog — o produto publica nome completo de sócios
 * do QSA, que são pessoas físicas fora do serviço público (ADR-0014 §5).
 */
export default function Privacidade() {
  return <PoliticaDePrivacidade />;
}
