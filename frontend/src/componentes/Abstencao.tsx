import type { CoberturaDeRegra } from "@/lib/contrato";

/**
 * "Não avaliado" — o componente que o ADR-0013 §2 chama de pedra angular.
 *
 * Zero por falta de dado tem de ser visualmente distinto de zero por nada encontrado. Na base
 * real, `REG_TX_002` está em zero nas 711.796 despesas porque a tabela de limites da CEAP só
 * cobre 2026; renderizar isso como aprovado seria o produto afirmando mais do que sabe.
 *
 * Exibe o **motivo**, não só o estado. A frase vem da API justamente para que nenhum
 * consumidor precise redigir a própria — que é como "nada irregular encontrado" acabaria
 * aparecendo em algum lugar.
 */
export function TagDeAbstencao({ motivo }: { motivo?: string | null }) {
  return (
    <span
      className="inline-flex items-center gap-1.5 border border-borda-suave bg-abstencao-fundo px-2 py-1 text-xs font-semibold text-abstencao-texto"
      role="status"
      aria-label={motivo ? `Não avaliado: ${motivo}` : "Não avaliado"}
    >
      <span
        aria-hidden="true"
        className="flex size-3.5 shrink-0 items-center justify-center rounded-full border border-dashed border-current text-[9px] leading-none"
      >
        –
      </span>
      Não avaliado
    </span>
  );
}

/**
 * A linha de uma regra no bloco de cobertura.
 *
 * O contrato manda `silencioEhConclusivo`, e é ele que decide o que se desenha — não a
 * contagem de flags. Duas regras com `flagsEmitidas: 0` significam coisas opostas.
 */
function LinhaDeRegra({ regra }: { regra: CoberturaDeRegra }) {
  const avaliouTudo = regra.silencioEhConclusivo && regra.despesasSemDado === 0;

  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-borda-suave py-2 last:border-b-0">
      <span className="numeros w-32 shrink-0 text-sm font-semibold text-titulo">
        {regra.codigo}
      </span>

      {regra.silencioEhConclusivo ? (
        <span className="text-sm">
          {regra.flagsEmitidas > 0 ? (
            <>
              <strong className="numeros text-titulo">{regra.flagsEmitidas}</strong>{" "}
              {regra.flagsEmitidas === 1 ? "indicador" : "indicadores"}
            </>
          ) : (
            <span className="text-texto-suave">Sem indicadores</span>
          )}
        </span>
      ) : (
        <TagDeAbstencao motivo={regra.motivoDaAbstencao} />
      )}

      {/* O motivo aparece mesmo quando a regra emitiu: ela pode ter avaliado só parte das
          despesas, e o leitor precisa saber sobre quantas o resultado vale. */}
      {!avaliouTudo && regra.motivoDaAbstencao ? (
        <span className="w-full text-xs text-texto-suave sm:w-auto sm:flex-1">
          {regra.motivoDaAbstencao}
        </span>
      ) : null}
    </li>
  );
}

/**
 * O bloco de cobertura do perfil.
 *
 * Vai **ao lado** dos achados, e não em rodapé: publicar as flags sem ele deixaria o leitor
 * supor que o silêncio de uma regra significa aprovação.
 */
export function BlocoDeCobertura({
  cobertura,
  regrasSemDado,
}: {
  cobertura: CoberturaDeRegra[];
  regrasSemDado: number;
}) {
  return (
    <section
      aria-labelledby="titulo-cobertura"
      className="border border-borda-suave bg-fundo p-4"
    >
      <h2 id="titulo-cobertura" className="text-base font-bold text-titulo">
        O que foi analisado
      </h2>

      <p className="mt-1 text-sm text-texto-suave">
        {regrasSemDado === 0 ? (
          <>
            As <strong className="numeros">{cobertura.length}</strong> regras puderam ser
            aplicadas a todas as despesas deste parlamentar.
          </>
        ) : (
          <>
            <strong className="numeros">{regrasSemDado}</strong> de{" "}
            <strong className="numeros">{cobertura.length}</strong> regras não puderam ser
            aplicadas integralmente por ausência de dado público.{" "}
            <strong>A ausência de indicador nelas não significa ausência de achado.</strong>
          </>
        )}
      </p>

      <ul className="mt-3">
        {cobertura.map((regra) => (
          <LinhaDeRegra key={regra.codigo} regra={regra} />
        ))}
      </ul>
    </section>
  );
}
