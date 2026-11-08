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
  const link = "inline-flex min-h-11 items-center font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4 hover:decoration-titulo";

  return (
    <footer className="mt-12 border-t border-borda-suave">
      <div className="mx-auto max-w-6xl space-y-2 px-4 py-8 text-sm text-texto-suave">
        <p>
          Dados abertos da Câmara, do Senado, da Receita Federal (via BrasilAPI), do TSE e do
          SIORG.
        </p>
        <p>
          Os indicadores vêm de um motor de regras que diz o que procurou em cada achado, e{" "}
          <strong className="text-texto">não caracterizam, por si, irregularidade</strong>.
          Ausência de indicador também não é atestado de regularidade.
        </p>
        {/*
          Aponta para a política, não para um `mailto`. O ADR-0010 §2 pedia o canal; a LGPD
          pede a política publicada, e um `mailto` sozinho não é política.
        */}
        <p className="flex flex-wrap gap-x-4 gap-y-1">
          <Link href="/privacidade" className={link}>
            Privacidade e Direitos LGPD
          </Link>
          <span>
            Encarregado de Dados: {ENCARREGADO.nome},{" "}
            <a href={`mailto:${ENCARREGADO.email}`} className={link}>
              {ENCARREGADO.email}
            </a>
          </span>
          <a href="/feed.xml" className={link}>
            Novos indicadores por RSS
          </a>
        </p>
      </div>
    </footer>
  );
}
