import type { Gravidade } from "@/lib/contrato";

/**
 * O peso visual de um achado, nos três níveis do ADR-0013.
 *
 * Cada nível carrega **cor, forma e rótulo**. A forma existe porque matiz sozinho falha para
 * cerca de 8% dos homens, que têm deficiência de visão de cor — e severidade é justamente o
 * atributo que menos pode se perder.
 *
 * O vermelho fica estritamente com a gravidade alta, que é 3,3% dos achados da base real.
 * Pintar as 43,3% de gravidade baixa da mesma cor afirmaria sobre 20 mil achados uma
 * severidade que o motor não atribuiu.
 */

const APARENCIA: Record<
  Gravidade,
  { classe: string; icone: string; rotulo: string; descricao: string }
> = {
  // 43,3% da base. Praticamente neutra: o achado aparece, mas não grita.
  Baixa: {
    classe: "bg-baixa-fundo text-baixa-texto border-borda-suave",
    icone: "i",
    rotulo: "Baixa",
    descricao: "indicador de baixa relevância",
  },
  // 53,4%.
  Media: {
    classe: "bg-media-fundo text-media-texto border-media-texto/25",
    icone: "!",
    rotulo: "Média",
    descricao: "indicador de relevância média",
  },
  // 3,3%. O único que usa vermelho.
  Alta: {
    classe: "bg-alta-fundo text-alta-texto border-alta-texto/25",
    icone: "×",
    rotulo: "Alta",
    descricao: "indicador de alta relevância",
  },
};

/** A forma que acompanha cada nível. Octógono para alta, triângulo para média, círculo para baixa. */
const FORMA: Record<Gravidade, string> = {
  Baixa: "rounded-full",
  Media: "[clip-path:polygon(50%_0%,100%_100%,0%_100%)]",
  Alta: "[clip-path:polygon(30%_0%,70%_0%,100%_30%,100%_70%,70%_100%,30%_100%,0%_70%,0%_30%)]",
};

/**
 * A tag carrega o **peso**, não a identidade da regra.
 *
 * O código saiu daqui — do texto e do nome acessível. Ele dizia "indicador de alta
 * relevância, regra REG_KYB_002", e quem usa leitor de tela ouvia a sigla soletrada a cada
 * achado da lista. A frase que explica o achado vem logo ao lado, vinda da API, com o valor
 * e o fornecedor reais; é ela que informa. O código continua no JSON, onde é identificador
 * estável de metodologia para quem integra.
 */
export function TagDeGravidade({ gravidade }: { gravidade: Gravidade }) {
  const { classe, icone, rotulo, descricao } = APARENCIA[gravidade];

  return (
    <span
      className={`inline-flex items-center gap-1.5 border px-2 py-1 text-xs font-semibold ${classe}`}
      role="status"
      // Nome acessível explícito: ícone sem rótulo não existe para leitor de tela, e o
      // leitor de tela é justamente quem mais depende da forma.
      aria-label={descricao}
    >
      <span
        aria-hidden="true"
        className={`flex size-3.5 shrink-0 items-center justify-center text-[9px] leading-none font-bold ${FORMA[gravidade]} bg-current`}
      >
        <span className="text-white">{icone}</span>
      </span>
      {/* Sem uppercase: caixa alta em texto curto é lida como ênfase, e ênfase sobre um
          achado descritivo é imputação pela tipografia. */}
      <span>{rotulo}</span>
    </span>
  );
}
