import type { Gravidade } from "@/lib/contrato";

/**
 * O peso visual de um achado, nos três níveis do ADR-0013.
 *
 * Cada nível carrega **cor, forma e rótulo**. A forma existe porque matiz sozinho falha para
 * cerca de 8% dos homens, que têm deficiência de visão de cor — e severidade é justamente o
 * atributo que menos pode se perder.
 *
 * O vermelho fica estritamente com a gravidade alta, a menor parte dos achados da base real (3,0% na auditoria de 25/09).
 * Pintar as 43,3% de gravidade baixa da mesma cor afirmaria sobre 20 mil achados uma
 * severidade que o motor não atribuiu.
 */

const APARENCIA: Record<
  Gravidade,
  { classe: string; rotulo: string; descricao: string }
> = {
  // 43,3% da base. Praticamente neutra: o achado aparece, mas não grita.
  Baixa: {
    classe: "bg-baixa-fundo text-baixa-texto border-borda-suave",
    rotulo: "Baixa",
    descricao: "indicador de baixa relevância",
  },
  // 53,4%.
  Media: {
    classe: "bg-media-fundo text-media-texto border-media-texto/25",
    rotulo: "Média",
    descricao: "indicador de relevância média",
  },
  // 3,3%. O único que usa vermelho.
  Alta: {
    classe: "bg-alta-fundo text-alta-texto border-alta-texto/25",
    rotulo: "Alta",
    descricao: "indicador de alta relevância",
  },
};

/**
 * A forma que acompanha cada nível, em SVG: círculo com "i" para baixa, triângulo com "!"
 * para média, octógono com "×" para alta. O traço interno usa a cor de fundo da própria tag,
 * então o ícone se lê nos dois temas sem cor nova.
 */
export function FormaDaGravidade({ gravidade }: { gravidade: Gravidade }) {
  const comum = { "aria-hidden": true, viewBox: "0 0 16 16", className: "size-3.5 shrink-0" } as const;

  if (gravidade === "Baixa") {
    return (
      <svg {...comum} data-forma="circulo">
        <circle cx="8" cy="8" r="7" fill="currentColor" />
        <rect x="7.1" y="7" width="1.8" height="5" fill="var(--color-baixa-fundo)" />
        <circle cx="8" cy="4.7" r="1.1" fill="var(--color-baixa-fundo)" />
      </svg>
    );
  }

  if (gravidade === "Media") {
    return (
      <svg {...comum} data-forma="triangulo">
        <path d="M8 1.2 15.2 14.5H.8Z" fill="currentColor" />
        <rect x="7.1" y="5.5" width="1.8" height="5" fill="var(--color-media-fundo)" />
        <circle cx="8" cy="12.4" r="1" fill="var(--color-media-fundo)" />
      </svg>
    );
  }

  return (
    <svg {...comum} data-forma="octogono">
      <path d="M5 .8h6L15.2 5v6L11 15.2H5L.8 11V5Z" fill="currentColor" />
      <path d="m5.3 5.3 5.4 5.4m0-5.4-5.4 5.4" stroke="var(--color-alta-fundo)" strokeWidth="1.8" />
    </svg>
  );
}

const NOME: Record<Gravidade, string> = { Baixa: "baixa", Media: "média", Alta: "alta" };

/**
 * A tag carrega o **peso**, não a identidade da regra.
 *
 * O código saiu daqui — do texto e do nome acessível. Ele dizia "indicador de alta
 * relevância, regra REG_KYB_002", e quem usa leitor de tela ouvia a sigla soletrada a cada
 * achado da lista. A frase que explica o achado vem logo ao lado, vinda da API, com o valor
 * e o fornecedor reais; é ela que informa.
 *
 * Com `contagem`, a tag mostra o número no lugar do rótulo (ranking e resumo do perfil):
 * a forma e a cor já dizem o nível, e o nome acessível diz os dois por extenso.
 */
export function TagDeGravidade({
  gravidade,
  contagem,
}: {
  gravidade: Gravidade;
  contagem?: number;
}) {
  const { classe, rotulo, descricao } = APARENCIA[gravidade];
  const nome =
    contagem === undefined
      ? descricao
      : `Gravidade ${NOME[gravidade]}, ${contagem.toLocaleString("pt-BR")} ${contagem === 1 ? "achado" : "achados"}`;

  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-md border px-2 py-1 text-[0.8125rem] leading-none font-semibold whitespace-nowrap ${classe}`}
      role="status"
      aria-label={nome}
    >
      <FormaDaGravidade gravidade={gravidade} />
      {/* Sem uppercase: caixa alta em texto curto é lida como ênfase, e ênfase sobre um
          achado descritivo é imputação pela tipografia. */}
      <span className={contagem === undefined ? undefined : "numeros"}>
        {contagem === undefined ? rotulo : contagem.toLocaleString("pt-BR")}
      </span>
    </span>
  );
}
