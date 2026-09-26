import { listarMudancasDaUnidade } from "@/lib/api";

/**
 * O histórico de uma unidade, servido pelo próprio site para a ilha do navegador.
 *
 * **Existe porque o navegador não pode falar com a API diretamente.** `lib/api` resolve o
 * endereço por variável de ambiente, e no navegador só existem as `NEXT_PUBLIC_`: a imagem de
 * produção não recebe `NEXT_PUBLIC_API` (o compose passa `EXPLANANDO_API`, que é o endereço
 * interno `http://api:8080`), e a ilha caía no padrão `http://localhost:5199` — conteúdo misto
 * e host inexistente para todo visitante. Medido na auditoria de 25/09.
 *
 * Mesma origem, então: o navegador pede aqui, e o servidor pede à API pela rede interna, com a
 * chave do renderizador, que nunca sai da máquina.
 */
export async function GET(_pedido: Request, { params }: { params: Promise<{ codigo: string }> }) {
  const { codigo } = await params;
  const numero = Number(codigo);

  if (!Number.isInteger(numero) || numero <= 0) {
    return Response.json({ erro: "Código de unidade inválido." }, { status: 404 });
  }

  const mudancas = await listarMudancasDaUnidade(numero);

  // Nulo é "a API não respondeu", e a ilha precisa distinguir isso de lista vazia.
  return mudancas === null
    ? Response.json({ erro: "A API não respondeu." }, { status: 502 })
    : Response.json(mudancas);
}
