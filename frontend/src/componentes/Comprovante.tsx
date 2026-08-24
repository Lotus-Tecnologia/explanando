import type { ItemDeDespesa } from "@/lib/contrato";
import { formatarReais } from "@/lib/formato";

/**
 * O botão de comprovante — ou a explicação de por que ele não existe.
 *
 * O ADR-0004 sustenta o produto inteiro num arranjo simples: o motor aponta o indicador, e o
 * **documento oficial** fala. Quando o documento não existe, a afirmação passa a ser
 * inteiramente nossa — e é por isso que a ausência precisa aparecer, atribuída a quem a
 * produziu.
 *
 * O Senado não publica comprovante em campo nenhum (ADR-0012 §1); a Câmara publica em 70,5%
 * das notas. Nos dois casos, o espaço vazio onde o link deveria estar seria lido como falha
 * da plataforma.
 */
export function Comprovante({
  despesa,
  casa,
}: {
  despesa: ItemDeDespesa;
  casa?: string;
}) {
  if (despesa.urlDocumento) {
    return (
      <a
        href={despesa.urlDocumento}
        target="_blank"
        rel="noopener noreferrer"
        className="inline-flex items-center gap-1.5 bg-titulo px-3 py-1.5 text-xs font-semibold text-white hover:bg-titulo-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-titulo"
      >
        Ver nota fiscal
        <span aria-hidden="true">↗</span>
      </a>
    );
  }

  // A frase muda com a casa, e a diferença não é cosmética.
  //
  // No Senado a ausência é **estrutural**: nenhum dos treze campos da CEAPS é URL, para nota
  // nenhuma. Na Câmara ela é **daquela nota**: 70,5% das despesas têm link. Escrever "a
  // Câmara não disponibiliza" seria falso, e escrever só "a fonte" no Senado devolve a
  // suspeita de falha para a plataforma — que é exatamente o que o ADR-0012 §1 evita.
  const frase = casa?.includes("Senado")
    ? "O Senado não disponibiliza o comprovante"
    : casa?.includes("Câmara")
      ? "A Câmara não publicou o comprovante desta nota"
      : "O governo não disponibilizou este comprovante";

  // Tag inativa: sem hover, sem cursor de link, sem prometer clique. Um link que dá 404
  // afirma que o documento sumiu.
  return (
    <span
      className="inline-flex items-center gap-1.5 border border-borda-suave bg-abstencao-fundo px-3 py-1.5 text-xs font-semibold text-abstencao-texto"
      role="note"
      aria-label={`${frase} — a ausência é da origem do dado, não da plataforma`}
    >
      <span aria-hidden="true">🚫</span>
      {frase}
    </span>
  );
}

/**
 * Os valores de uma despesa.
 *
 * A quebra "declarado × glosa" é **omitida por inteiro** quando a glosa é desconhecida.
 * Exibir `R$ 0,00` diria que a despesa passou por conferência e foi integralmente aprovada —
 * afirmação que ninguém fez, produzida por um valor que parece inocente (ADR-0012 §2).
 */
export function ValoresDaDespesa({ despesa }: { despesa: ItemDeDespesa }) {
  const glosaConhecida = despesa.valorGlosa !== null;
  const houveCorte = glosaConhecida && despesa.valorGlosa! > 0;

  return (
    <div className="numeros text-right">
      <div className="font-semibold text-titulo">
        {formatarReais(despesa.valorLiquido)}
        {despesa.ehEstorno ? (
          <span className="ml-2 text-xs font-normal text-texto-suave">estorno</span>
        ) : null}
      </div>

      {houveCorte ? (
        <div className="text-xs text-texto-suave">
          declarado {formatarReais(despesa.valorDocumento)} · glosa{" "}
          {formatarReais(despesa.valorGlosa!)}
        </div>
      ) : null}
    </div>
  );
}

// A formatação mora em `lib/formato.ts` porque a meta description também precisa dela, e ela
// é montada antes de qualquer componente existir. O reexporte preserva o ponto de importação
// que o resto da interface já usa.
export { formatarReais };
