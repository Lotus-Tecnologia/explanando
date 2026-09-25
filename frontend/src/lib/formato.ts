/**
 * Formatação de números para leitura humana.
 *
 * Mora em `lib/` e não em `componentes/` porque a meta description também precisa dela, e a
 * página indexada é montada antes de qualquer componente existir.
 */

export function formatarReais(valor: number): string {
  return valor.toLocaleString("pt-BR", {
    style: "currency",
    currency: "BRL",
    minimumFractionDigits: 2,
  });
}

export function formatarNumero(valor: number): string {
  return valor.toLocaleString("pt-BR");
}

export function formatarDensidade(valor: number): string {
  return valor.toLocaleString("pt-BR", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
}

/**
 * Reais em forma curta: `R$ 1,9 mi`, `R$ 340 mil`, `R$ 820`.
 *
 * Existe para caber onde o valor por extenso não cabe — o cabeçalho de um quadro de grafo,
 * um rótulo dentro de um desenho. **Não substitui `formatarReais`** em tabela, extrato ou
 * qualquer lugar onde o leitor possa querer conferir o centavo: arredondar um valor que a
 * pessoa vai comparar com a nota fiscal é esconder a diferença.
 */
export function formatarMoedaCompacta(valor: number): string {
  const absoluto = Math.abs(valor);
  const sinal = valor < 0 ? "-" : "";

  if (absoluto >= 1_000_000) {
    const milhoes = absoluto / 1_000_000;

    return `${sinal}R$ ${milhoes.toLocaleString("pt-BR", {
      minimumFractionDigits: milhoes < 10 ? 1 : 0,
      maximumFractionDigits: milhoes < 10 ? 1 : 0,
    })} mi`;
  }

  if (absoluto >= 1_000) {
    return `${sinal}R$ ${Math.round(absoluto / 1_000).toLocaleString("pt-BR")} mil`;
  }

  return `${sinal}R$ ${Math.round(absoluto).toLocaleString("pt-BR")}`;
}

/**
 * Porcentagens inteiras que **somam exatamente 100**.
 *
 * Arredondar cada fatia por conta própria dá 99% ou 101% com frequência — três fatias de um
 * terço viram 33 + 33 + 33. Numa tela cuja promessa é "é daqui que o índice vem", um total
 * que não fecha é a primeira coisa que um leitor atento aponta. O método é o do maior resto:
 * arredonda tudo para baixo e distribui os pontos que faltam para as maiores sobras.
 *
 * Fatias todas zero (ou lista vazia) devolvem zeros: não há 100% a distribuir.
 */
export function porcentagensInteiras(fatias: number[]): number[] {
  const total = fatias.reduce((soma, fatia) => soma + fatia, 0);

  if (total <= 0) {
    return fatias.map(() => 0);
  }

  const brutas = fatias.map((fatia) => (fatia / total) * 100);
  const inteiras = brutas.map(Math.floor);
  let faltam = 100 - inteiras.reduce((soma, valor) => soma + valor, 0);

  const porSobra = brutas
    .map((bruta, indice) => ({ indice, sobra: bruta - Math.floor(bruta) }))
    .sort((a, b) => b.sobra - a.sobra || a.indice - b.indice);

  for (const { indice } of porSobra) {
    if (faltam <= 0) {
      break;
    }

    inteiras[indice] += 1;
    faltam -= 1;
  }

  return inteiras;
}

/**
 * A data de um documento: dia do calendário, sem hora.
 *
 * A API entrega `2026-07-27T00:00:00+00:00` — meia-noite em UTC, que é como uma data sem hora
 * chega em JSON. Formatar isso no fuso de Brasília dá **26/07**: a meia-noite UTC ainda é dia
 * 26 às 21h aqui. Era o que o perfil exibia — um dia antes da nota fiscal. Lida em UTC, a
 * data é a do documento.
 */
export function formatarDataDoDocumento(iso: string): string {
  return new Date(iso).toLocaleDateString("pt-BR", { timeZone: "UTC" });
}

/**
 * Um instante — uma varredura, uma coleta, uma resposta enviada —, no horário de Brasília.
 *
 * O fuso é explícito porque quem renderiza não está no Brasil: o servidor roda em UTC, e uma
 * varredura às 22h de Brasília viraria "amanhã" na página. Explícito também evita que o
 * servidor e o navegador escrevam datas diferentes na mesma tela.
 */
export function formatarMomento(iso: string): string {
  return new Date(iso).toLocaleDateString("pt-BR", { timeZone: "America/Sao_Paulo" });
}

/** Uma fração de 0 a 1 como porcentagem com uma casa: `0.033` vira `3,3%`. */
export function formatarPorcentagem(fracao: number): string {
  return `${(fracao * 100).toLocaleString("pt-BR", {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  })}%`;
}

const MESES = [
  "janeiro", "fevereiro", "março", "abril", "maio", "junho",
  "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
];

/**
 * "2026-07" como leitor lê: "julho de 2026". A competência chega da API no formato do banco,
 * e ele aparecia cru no cabeçalho do mapa do Estado. Formato inesperado volta como veio.
 */
export function formatarCompetencia(competencia: string | null): string {
  if (!competencia) {
    return "";
  }

  const casamento = /^(\d{4})-(\d{2})$/.exec(competencia);

  if (!casamento) {
    return competencia;
  }

  const mes = MESES[Number(casamento[2]) - 1];
  return mes ? `${mes} de ${casamento[1]}` : competencia;
}

/** "2026-07" como "07/2026" — o formato curto que a tabela do perfil já usa, para eixo e tabela. */
export function formatarCompetenciaCurta(competencia: string): string {
  const casamento = /^(\d{4})-(\d{2})$/.exec(competencia);
  return casamento ? `${casamento[2]}/${casamento[1]}` : competencia;
}

const PARTICULAS = new Set(["da", "das", "de", "do", "dos", "e"]);

/**
 * "CAXIAS DO SUL" como se escreve: "Caxias do Sul". O TSE publica o município em maiúsculas; a
 * partícula fica em minúscula, menos no começo ("Dois Irmãos", mas "Barra do Garças").
 */
export function formatarNomeDeLugar(nome: string): string {
  return nome
    .toLocaleLowerCase("pt-BR")
    .split(/(\s+|-)/)
    .map((parte, indice) =>
      indice > 0 && PARTICULAS.has(parte)
        ? parte
        : parte.charAt(0).toLocaleUpperCase("pt-BR") + parte.slice(1),
    )
    .join("");
}
