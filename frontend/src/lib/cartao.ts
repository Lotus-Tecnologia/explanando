import type { PerfilDoParlamentar } from "./contrato";
import { formatarDensidade, formatarNumero, formatarReais } from "./formato";

/**
 * O texto do card de compartilhamento de um perfil (`opengraph-image.tsx`).
 *
 * Separado do desenho para ser testado sem gerar imagem. O card é o que aparece no WhatsApp e
 * no X quando alguém compartilha o perfil — muitas vezes a única coisa que a pessoa vê, sem
 * abrir a página. Três consequências:
 *
 * - **a ressalva do ADR-0004 vai na imagem**, e não só na página: o card circula sozinho;
 * - abaixo do piso amostral o índice sai como **não calculado**, nunca como zero (ADR-0009);
 * - o volume vem junto com o número de despesas, para que o valor não pareça um total de
 *   achados — é o que o parlamentar declarou, não o que foi apontado.
 */
export interface TextosDoCartao {
  nome: string;
  legenda: string;
  volume: string;
  volumeLegenda: string;
  indice: string | null;
  indiceLegenda: string;
  altas: string;
  altasLegenda: string;
  ressalva: string;
  endereco: string;
}

export function textosDoCartao(perfil: PerfilDoParlamentar): TextosDoCartao {
  const temIndice = perfil.amostraSuficiente && perfil.indiceDeRisco !== null;

  return {
    nome: perfil.nomeParlamentar,
    legenda: `${perfil.siglaPartido} · ${perfil.uf} · ${perfil.casa}`,
    volume: formatarReais(perfil.volumeTotalGasto),
    volumeLegenda: `em ${formatarNumero(perfil.despesasAvaliadas)} despesas da cota parlamentar`,
    indice: temIndice ? formatarDensidade(perfil.indiceDeRisco!) : null,
    indiceLegenda: temIndice
      ? "peso dos achados a cada 100 despesas"
      : "índice não calculado: menos de 100 despesas",
    altas: formatarNumero(perfil.flagsAltas),
    altasLegenda: perfil.flagsAltas === 1 ? "achado de alta relevância" : "achados de alta relevância",
    ressalva: "Achados técnicos sobre dados públicos. Não são juízo de conduta.",
    endereco: "explanando.com.br",
  };
}
