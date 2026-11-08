import { Botao } from "./base/Botao";

/**
 * A tela de um caminho que não deu certo.
 *
 * Existe como componente, e não escrita duas vezes nas páginas de erro, porque as duas
 * situações — endereço que não existe e falha nossa — precisam dizer coisas diferentes com
 * a mesma estrutura. E porque as duas precisam da mesma saída: quase todo o tráfego chega
 * por link compartilhado, e um beco sem saída devolve a pessoa para a aba anterior sem que
 * ela descubra que o resto do site existe.
 */
export function Aviso({
  titulo,
  children,
  acao,
}: {
  titulo: string;
  children: React.ReactNode;
  acao?: React.ReactNode;
}) {
  return (
    <main className="mx-auto max-w-3xl px-4 py-16">
      <h1 className="font-display text-3xl font-bold text-titulo">{titulo}</h1>

      <div className="mt-3 text-texto">{children}</div>

      <div className="mt-6 flex flex-wrap items-center gap-3">
        <Botao href="/">Ir para o ranking</Botao>

        {acao}
      </div>
    </main>
  );
}
