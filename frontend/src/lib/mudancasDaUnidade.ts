import type { MudancaDoEstado } from "./contrato";

/**
 * O histórico de uma unidade, pedido **pelo navegador ao próprio site**.
 *
 * Não passa por `lib/api` de propósito: lá o endereço vem de variável de ambiente, e no
 * navegador só chegam as `NEXT_PUBLIC_`. A imagem de produção não recebe `NEXT_PUBLIC_API`, e a
 * ilha caía em `http://localhost:5199` para todo visitante (auditoria de 25/09). O caminho
 * relativo vai à rota `app/estado/[codigo]/mudancas`, que pede à API pela rede interna.
 *
 * Nulo quando falha, e a tela distingue isso de lista vazia.
 */
export async function buscarMudancasDaUnidade(
  codigo: number,
): Promise<MudancaDoEstado[] | null> {
  try {
    const resposta = await fetch(`/estado/${codigo}/mudancas`, {
      headers: { Accept: "application/json" },
    });

    return resposta.ok ? ((await resposta.json()) as MudancaDoEstado[]) : null;
  } catch {
    return null;
  }
}
