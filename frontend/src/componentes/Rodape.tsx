import Link from "next/link";
import { ENCARREGADO } from "@/lib/site";

/**
 * O rodapé, com a ressalva e o Encarregado de Dados.
 *
 * Duas obrigações diferentes moram aqui, e nenhuma é enfeite.
 *
 * A **ressalva** existe porque a página é indexada e carrega nome e foto de pessoa real: ela
 * diz, em texto e não em letra miúda, que indicador não é irregularidade. É o ADR-0004 na
 * borda de saída.
 *
 * O **Encarregado** é exigência do ADR-0010 §2, e a LGPD art. 41 §1 pede identidade *e*
 * contato divulgados publicamente — só o e-mail não cumpre. O produto publica nome completo
 * de sócios do QSA, que é dado pessoal de gente que não é agente público; ter um canal com
 * responsável nomeado é condição para operar, não item de backlog.
 */
export function Rodape() {
  return (
    <footer className="mx-auto max-w-7xl border-t border-borda-suave px-4 py-6 text-xs text-texto-suave">
      <p>
        Dados obtidos das APIs de dados abertos da Câmara dos Deputados, do Senado Federal,
        da Receita Federal (via BrasilAPI) e do SIORG, a estrutura organizacional do Estado.
        Os indicadores são produzidos por um motor de regras determinístico, que diz o que
        procurou em cada achado, e{" "}
        <strong>não caracterizam, por si, irregularidade</strong>. A conclusão é de quem lê.
      </p>

      <p className="mt-2">
        Parte das regras depende de dado que nem toda fonte publica. Quando faltou dado, a
        página diz que faltou — <strong>ausência de indicador não é atestado de
        regularidade</strong>.
      </p>

      <p className="mt-3">
        {/*
          Aponta para a política, não para um `mailto`. O ADR-0010 §2 pedia o canal; a LGPD
          pede a política publicada, e um `mailto` sozinho não é política.
        */}
        <Link
          href="/privacidade"
          className="font-semibold text-titulo underline underline-offset-2"
        >
          Privacidade e Direitos LGPD
        </Link>{" "}
        · Encarregado de Dados: {ENCARREGADO.nome} —{" "}
        <a
          href={`mailto:${ENCARREGADO.email}`}
          className="numeros text-titulo underline underline-offset-2"
        >
          {ENCARREGADO.email}
        </a>
      </p>
    </footer>
  );
}
