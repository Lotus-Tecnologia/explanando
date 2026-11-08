/**
 * Tabela larga que rola **dentro do próprio contêiner**, nunca a página. O contêiner é uma
 * região rotulada e focável, para que quem usa teclado consiga rolar de lado.
 */
export function TabelaDeDados({
  legenda,
  colunas,
  children,
}: {
  legenda: string;
  colunas: readonly { rotulo: string; numerica?: boolean }[];
  children: React.ReactNode;
}) {
  return (
    <div
      role="region"
      aria-label={legenda}
      tabIndex={0}
      className="overflow-x-auto rounded-cartao border border-borda-suave bg-surface"
    >
      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">{legenda}</caption>
        <thead>
          <tr className="border-b border-borda-estrutural">
            {colunas.map((c) => (
              <th
                key={c.rotulo}
                scope="col"
                className={`px-4 py-3 font-semibold whitespace-nowrap text-texto-suave ${c.numerica ? "text-right" : "text-left"}`}
              >
                {c.rotulo}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="[&>tr]:border-b [&>tr]:border-borda-suave [&>tr:last-child]:border-b-0">
          {children}
        </tbody>
      </table>
    </div>
  );
}
