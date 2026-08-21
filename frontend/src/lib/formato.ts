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
