import type { NoDoEstado } from "@/lib/contrato";
import { caminhoDeArco, corDoPoder, montarDesenho } from "@/lib/grafo";
import { formatarNumero } from "@/lib/formato";

/**
 * O mapa da estrutura do Estado, em anéis.
 *
 * **É SVG puro, montado no servidor.** Nenhum JavaScript acompanha esta página: o desenho
 * chega pronto, com cada fatia sendo um link de verdade. É a mesma aposta do ADR-0001 — a
 * página que um link viral abre precisa ser um arquivo, não um aplicativo que baixa dados e
 * calcula um layout.
 *
 * **A cor nunca informa sozinha.** Cada fatia carrega `<title>` com o nome, o poder e a
 * contagem de unidades; a legenda nomeia os quatro poderes; e a lista abaixo do desenho
 * repete tudo em texto. Quem não distingue as cores lê a mesma coisa — exigência do
 * ADR-0013.
 */
export function GrafoDoEstado({ nos }: { nos: readonly NoDoEstado[] }) {
  const desenho = montarDesenho(nos);

  if (!desenho.centro || desenho.fatias.length === 0) {
    return null;
  }

  // O miolo é grande porque carrega o rótulo da raiz; o raio externo é fixo para que o
  // desenho ocupe sempre o mesmo espaço na página, com quantos anéis a hierarquia tiver.
  // A espessura se ajusta — sem isso, uma árvore de oito níveis viraria um alvo gigante e
  // uma de três, um disco perdido no meio do quadro.
  const miolo = 62;
  const raioMaximo = 340;
  const espessura = (raioMaximo - miolo) / Math.max(desenho.niveis, 1);

  // Uma folga de 4 unidades para o traço das fatias externas não ser cortado pela borda.
  const lado = (raioMaximo + 4) * 2;

  return (
    <figure className="m-0">
      <svg
        viewBox={`${-lado / 2} ${-lado / 2} ${lado} ${lado}`}
        className="mx-auto block h-auto w-full max-w-[36rem]"
        role="img"
        aria-label={
          `Mapa da estrutura do Estado: ${formatarNumero(desenho.fatias.length)} órgãos e ` +
          `entidades dispostos em anéis a partir de ${desenho.centro.nome}. ` +
          "A lista completa vem logo abaixo, em texto."
        }
      >
        {desenho.fatias.map((fatia) => {
          const raioInterno = miolo + espessura * (fatia.nivel - 1);

          return (
            /*
              `<a>` do próprio SVG, e não o `Link` do Next: dentro de um `<svg>` o
              componente do Next renderiza um âncora HTML, que o navegador não trata como
              parte do desenho. A navegação vira recarga de página inteira — e nesta
              página isso não custa nada, porque ela não carrega JavaScript nenhum.
            */
            <a key={fatia.codigo} href={`/estado/${fatia.codigo}`}>
              <path
                d={caminhoDeArco(raioInterno, raioInterno + espessura, fatia.inicio, fatia.fim)}
                fill={corDoPoder(fatia.poder)}
                stroke="var(--color-fundo)"
                strokeWidth={0.75}
                /*
                  A opacidade cresce com a profundidade ao contrário do esperado: os anéis
                  externos são os mais numerosos e finos, e precisam de mais peso visual
                  para não sumirem. Os internos são grandes e já dominam por área.
                */
                opacity={0.55 + fatia.nivel * 0.12}
              >
                <title>
                  {`${fatia.nome}${fatia.sigla ? ` (${fatia.sigla})` : ""} — ${fatia.tipo}` +
                    `${fatia.poder ? `, ${fatia.poder}` : ""}` +
                    `, ${formatarNumero(fatia.unidadesSubordinadas)} unidades subordinadas`}
                </title>
              </path>
            </a>
          );
        })}

        <circle r={miolo - 4} fill="var(--color-surface)" stroke="var(--color-borda-suave)" />

        <text
          textAnchor="middle"
          className="fill-titulo font-display text-[13px] font-bold"
          y={-6}
        >
          {desenho.centro.sigla ?? "Brasil"}
        </text>

        <text textAnchor="middle" className="fill-texto-suave text-[10px]" y={12}>
          {formatarNumero(desenho.centro.unidadesSubordinadas)} unidades
        </text>
      </svg>

      <figcaption className="mt-4 space-y-2 text-sm text-texto-suave">
        <p>
          Cada fatia é um órgão ou entidade, e a distância do centro é a hierarquia. A{" "}
          <strong className="font-semibold">largura</strong> acompanha o número de unidades
          que pendem daquele ramo — sem isso, um ministério e um conselho de três pessoas
          ocupariam o mesmo espaço. Clique para abrir qualquer um.
        </p>
        <p>
          A escala da largura é <strong className="font-semibold">comprimida</strong> (raiz
          quadrada), e dizemos isso porque muda a leitura: o Executivo tem mais de cem vezes
          as unidades do Judiciário, e numa escala direta os outros três poderes seriam
          fatias de menos de um grau. Os números exatos estão na legenda e na lista.
        </p>
      </figcaption>
    </figure>
  );
}

/**
 * A legenda dos poderes.
 *
 * Fica fora do SVG de propósito: dentro, o texto escala junto com o desenho e fica
 * minúsculo no celular. Aqui ele é texto da página, com o tamanho do corpo.
 */
export function LegendaDePoderes({
  poderes,
}: {
  poderes: readonly { rotulo: string; unidades: number }[];
}) {
  return (
    <ul className="flex flex-wrap gap-x-5 gap-y-2">
      {poderes.map((poder) => (
        <li key={poder.rotulo} className="flex items-center gap-2 text-sm">
          <span
            aria-hidden="true"
            className="size-3 shrink-0 rounded-[3px]"
            style={{ background: corDoPoder(poder.rotulo) }}
          />
          <span className="text-texto">{poder.rotulo}</span>
          <span className="numeros text-texto-suave">{formatarNumero(poder.unidades)}</span>
        </li>
      ))}
    </ul>
  );
}
