import { defineCloudflareConfig } from "@opennextjs/cloudflare";
import r2IncrementalCache from "@opennextjs/cloudflare/overrides/incremental-cache/r2-incremental-cache";

/**
 * O adaptador que traduz a saída do Next para o runtime do Cloudflare Workers.
 *
 * <b>O cache incremental não é opcional aqui.</b> Sem ele, `revalidate` não tem onde guardar a
 * página regenerada, e cada acesso depois da janela de 5 minutos volta a bater na API — que é
 * exatamente a premissa de custo do ADR-0001 se perdendo. Num Worker não existe disco local
 * entre invocações; o R2 é o disco.
 *
 * R2 e não KV de propósito: o KV é eventualmente consistente com janela de até 60 segundos
 * entre regiões, e uma página de gasto público que aparece diferente conforme o nó que
 * atendeu é o tipo de inconsistência que ninguém sabe explicar depois. O R2 é fortemente
 * consistente e tem egresso zero, que é o motivo de ele já estar no plano de infraestrutura.
 */
export default defineCloudflareConfig({
  incrementalCache: r2IncrementalCache,
});
