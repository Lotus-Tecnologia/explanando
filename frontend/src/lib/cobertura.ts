/**
 * A cobertura de fornecedor: que fração das despesas tem o fornecedor identificado na Receita
 * — e portanto quanto do gasto o índice de fato descreve.
 *
 * Decisão de 26/09/2026. O índice divide o peso dos achados por TODAS as despesas, e a maior
 * parte das regras só examina despesa com fornecedor identificado. Na auditoria de 25/09, 92
 * dos 498 deputados ranqueados tinham menos da metade assim — quase sempre por passagem aérea,
 * que a Câmara publica sem fornecedor. Abaixo da metade, o parlamentar sai do ranking por
 * índice (a API já filtra) e a tela diz por quê. Não é juízo sobre ele: é o limite do dado.
 */

export function porcentagemDaCobertura(cobertura: number | null): string | null {
  return cobertura === null ? null : `${Math.round(cobertura * 100)}%`;
}

export function notaDaCobertura(cobertura: number | null, suficiente: boolean): string | null {
  const porcentagem = porcentagemDaCobertura(cobertura);

  if (porcentagem === null) {
    return null;
  }

  return suficiente
    ? `Fornecedor identificado em ${porcentagem} das despesas.`
    : `Fornecedor identificado em só ${porcentagem} das despesas: o índice descreve menos da metade do gasto e fica fora do ranking por índice.`;
}
