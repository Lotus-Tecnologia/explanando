import type { ContagemDoEstado, MudancaDoEstado } from "@/lib/contrato";
import { formatarNumero } from "@/lib/formato";

/**
 * Os números do grafo, em barra empilhada.
 *
 * Mesmo desenho do `PainelDaCasa`: uma barra por corte, com as fatias proporcionais e a
 * legenda em texto ao lado. Cartão com um número solto era o que o Gustavo chamou de
 * "meio pobre" — a barra mostra a **proporção**, que é a informação que um número sozinho
 * não carrega.
 */
export function DistribuicaoDoEstado({
  titulo,
  explicacao,
  contagens,
  cores,
}: {
  titulo: string;
  explicacao: string;
  contagens: readonly ContagemDoEstado[];

  /** Recebe o rótulo e a posição — a posição serve às escalas neutras. */
  cores: (rotulo: string, indice: number) => string;
}) {
  const total = contagens.reduce((soma, item) => soma + item.unidades, 0);

  if (total === 0) return null;

  return (
    <section className="rounded-cartao border border-borda-suave bg-surface p-5 shadow-cartao">
      <h3 className="font-display text-base font-bold text-titulo">{titulo}</h3>
      <p className="mt-1 text-sm text-texto-suave">{explicacao}</p>

      <div className="mt-4 flex h-3 gap-px overflow-hidden rounded-selo" aria-hidden="true">
        {contagens.map((item, indice) => (
          <span
            key={item.rotulo}
            className="block h-full first:rounded-l-selo last:rounded-r-selo"
            style={{
              /*
                Um piso de 0,6% na largura. Sem ele, um corte com 2 unidades em 96.658 some
                da barra — e some justamente o item que a barra deveria denunciar como
                pequeno. O número exato fica na lista logo abaixo.
              */
              width: `${Math.max((item.unidades / total) * 100, 0.6)}%`,
              background: cores(item.rotulo, indice),
            }}
          />
        ))}
      </div>

      <dl className="mt-4 space-y-2">
        {contagens.map((item, indice) => (
          <div key={item.rotulo} className="flex items-baseline gap-2">
            <span
              aria-hidden="true"
              className="size-2.5 shrink-0 translate-y-px rounded-[2px]"
              style={{ background: cores(item.rotulo, indice) }}
            />
            <dt className="min-w-0 flex-1 truncate text-sm text-texto">{item.rotulo}</dt>
            <dd className="numeros text-sm font-semibold text-titulo">
              {formatarNumero(item.unidades)}
              <span className="ms-1.5 text-xs font-normal text-texto-suave">
                {((item.unidades / total) * 100).toFixed(1).replace(".", ",")}%
              </span>
            </dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

/**
 * A linha do tempo da estrutura.
 *
 * **O estado vazio aqui tem dois significados opostos, e confundi-los seria o erro do
 * ADR-0009 de novo.** Sem histórico, vazio quer dizer "só coletamos uma vez, ainda não há
 * com o que comparar". Com histórico, vazio quer dizer "comparamos e nada mudou" — que é
 * notícia de verdade sobre um Estado estável. O componente diz qual dos dois é.
 */
export function LinhaDoTempoDoEstado({
  mudancas,
  temHistorico,
}: {
  mudancas: readonly MudancaDoEstado[] | null;
  temHistorico: boolean;
}) {
  if (mudancas === null) {
    return (
      <p className="rounded-cartao border border-borda-suave bg-surface p-5 text-sm text-texto-suave">
        A lista de mudanças não pôde ser carregada agora.
      </p>
    );
  }

  if (mudancas.length === 0) {
    return (
      <p className="rounded-cartao border border-borda-suave bg-surface p-5 text-sm text-texto-suave">
        {temHistorico
          ? "Nada mudou na estrutura desde a coleta anterior."
          : "Ainda não há com o que comparar. A fonte publica um retrato do agora e não guarda " +
            "histórico, então a primeira notícia só pode existir depois da segunda coleta."}
      </p>
    );
  }

  return (
    <ol className="space-y-px overflow-hidden rounded-cartao border border-borda-suave">
      {mudancas.map((mudanca, indice) => (
        <li
          key={`${mudanca.codigoUnidade}-${mudanca.detectadaEm}-${indice}`}
          className="bg-surface px-5 py-4"
        >
          <p className="text-sm text-texto">
            <strong className="font-semibold text-titulo">{mudanca.unidade}</strong>{" "}
            {mudanca.descricao}
            {mudanca.de && mudanca.para ? (
              <>
                : de <span className="text-texto-suave">{mudanca.de}</span> para{" "}
                <span className="text-texto-suave">{mudanca.para}</span>
              </>
            ) : null}
          </p>
          <p className="mt-1 text-xs text-texto-suave">
            {/*
              "Detectada em", e não "aconteceu em". A fonte não data a mudança — nós a
              deduzimos comparando duas coletas, então o que sabemos é quando VIMOS, e
              escrever outra coisa seria precisão inventada.
            */}
            Detectada em{" "}
            <time dateTime={mudanca.detectadaEm}>
              {new Date(mudanca.detectadaEm).toLocaleDateString("pt-BR")}
            </time>
          </p>
        </li>
      ))}
    </ol>
  );
}
