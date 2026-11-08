/**
 * Campo de texto com rótulo sempre presente: visível, ou só para leitor de tela quando o
 * contexto já diz o que é (a busca da home). Só a busca leva ícone, porque ali ele informa.
 */
export function Campo({
  id,
  rotulo,
  rotuloVisivel = true,
  tipo = "texto",
  ...input
}: {
  id: string;
  rotulo: string;
  rotuloVisivel?: boolean;
  tipo?: "busca" | "texto";
} & Omit<React.InputHTMLAttributes<HTMLInputElement>, "id" | "type">) {
  return (
    <div>
      <label
        htmlFor={id}
        className={rotuloVisivel ? "mb-1.5 block text-sm font-semibold text-texto" : "sr-only"}
      >
        {rotulo}
      </label>
      <div className="flex min-h-13 items-center gap-2.5 rounded-campo border-[1.5px] border-borda-suave bg-surface px-4 focus-within:border-acento">
        {tipo === "busca" ? (
          <svg aria-hidden="true" viewBox="0 0 20 20" className="size-5 shrink-0 text-texto-suave">
            <circle cx="8.5" cy="8.5" r="6" fill="none" stroke="currentColor" strokeWidth="2" />
            <path d="m13 13 5 5" stroke="currentColor" strokeWidth="2" />
          </svg>
        ) : null}
        <input
          id={id}
          type={tipo === "busca" ? "search" : "text"}
          className="min-w-0 flex-1 bg-transparent py-3 text-base text-texto placeholder:text-texto-suave focus:outline-none"
          {...input}
        />
      </div>
    </div>
  );
}
