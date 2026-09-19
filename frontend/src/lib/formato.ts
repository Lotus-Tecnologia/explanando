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
