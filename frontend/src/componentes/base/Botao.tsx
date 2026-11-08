import Link from "next/link";

export type VarianteDeBotao = "primario" | "secundario" | "texto";

const CLASSE: Record<VarianteDeBotao, string> = {
  primario:
    "inline-flex min-h-11 items-center justify-center gap-2 rounded-campo bg-titulo px-5 text-[0.9375rem] font-semibold text-sobre-titulo hover:bg-titulo-hover",
  secundario:
    "inline-flex min-h-11 items-center justify-center gap-2 rounded-campo border border-borda-suave bg-surface px-5 text-[0.9375rem] font-semibold text-titulo hover:border-borda-estrutural",
  texto:
    "inline-flex min-h-11 items-center gap-1 font-semibold text-titulo underline decoration-borda-estrutural underline-offset-4 hover:decoration-titulo",
};

/** A classe do botão, para quando o elemento é de outro componente (`<summary>`, `<button>` de formulário). */
export function classeDeBotao(variante: VarianteDeBotao = "primario"): string {
  return CLASSE[variante];
}

/**
 * O botão do produto. Com `href` vira link; `externo` abre em nova aba e diz isso a quem
 * usa leitor de tela, porque uma aba que abre sem aviso tira a pessoa do lugar.
 */
export function Botao({
  variante = "primario",
  href,
  externo,
  children,
  ...resto
}: {
  variante?: VarianteDeBotao;
  href?: string;
  externo?: boolean;
  children: React.ReactNode;
} & Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, "children">) {
  const classe = CLASSE[variante];

  if (href && externo) {
    return (
      <a href={href} target="_blank" rel="noopener noreferrer" className={classe}>
        {children}
        {" "}<span className="sr-only">(abre em nova aba)</span>
      </a>
    );
  }

  if (href) {
    return (
      <Link href={href} className={classe}>
        {children}
      </Link>
    );
  }

  return (
    <button type="button" className={classe} {...resto}>
      {children}
    </button>
  );
}
