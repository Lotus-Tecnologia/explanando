/** Um assunto, num cartão: fundo de superfície, borda suave, canto de 18px, sem sombra. */
export function Superficie({
  como: Elemento = "section",
  titulo,
  idDoTitulo,
  className = "",
  children,
}: {
  como?: "section" | "div";
  titulo?: React.ReactNode;
  idDoTitulo?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <Elemento
      aria-labelledby={titulo && idDoTitulo ? idDoTitulo : undefined}
      className={`rounded-cartao border border-borda-suave bg-surface p-4 sm:p-5 ${className}`}
    >
      {titulo ? (
        <h2 id={idDoTitulo} className="mb-3 text-[0.9375rem] font-semibold text-texto-suave">
          {titulo}
        </h2>
      ) : null}
      {children}
    </Elemento>
  );
}
