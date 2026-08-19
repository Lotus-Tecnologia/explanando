# ADR-0007 — Motor permissivo: na dúvida, silêncio

**Data:** 2026-08-19 · **Situação:** Aceita

## Contexto

Duas regras do motor dependem de tabelas que **nenhuma API publica** e que precisam de
curadoria humana: `REG_KYB_001` (CNAE incompatível com o tipo de despesa) e `REG_TX_002`
(gasto cravado no teto da cota).

Toda tabela curada tem lacunas e imprecisões. A pergunta de projeto é o que fazer diante
delas — e a resposta muda o produto inteiro, porque define a razão entre achados e falsos
positivos.

O contexto que decide: **cada falso positivo é uma imputação indevida numa página pública
indexada, com nome e foto de uma pessoa real.** Um alerta a mais custa muito mais caro que
um alerta a menos. É a mesma lógica que sustenta o [ADR-0004](ADR-0004-nomenclatura-tecnica.md).

## Decisão

**O motor se abstém sempre que não tiver base para afirmar.** Nenhuma flag é emitida quando
falta dado.

Aplicado a `REG_KYB_001`, isso significa três escolhas concretas:

1. **Comparação por prefixo de CNAE, não por código exato.** O CNAE tem 7 dígitos
   (divisão-grupo-classe-subclasse); casar por 2 a 5 dígitos aceita a família inteira e
   dispensa acertar a subclasse, que muda com o tempo.
2. **Basta um CNAE bater** — principal ou secundário. Exigir que o principal batesse marcaria
   empresas legítimas de atividade mista: o posto de combustível da amostra real tem "bares"
   entre os secundários.
3. **Fornecedor sem CNAE, CNAE ilegível ou tipo de despesa fora da tabela → silêncio**, não
   suspeita.

Aplicado a `REG_TX_002`: despesa cuja competência não é coberta por nenhum limite vigente
**não é avaliada**. Os valores de 2023 a 2025 ainda não foram levantados, então a regra só
alcança 2026 por enquanto.

O resultado é um `ResultadoDeCompatibilidade` de três estados — `Compativel`, `Incompativel`,
`SemBaseParaAvaliar` — em vez de um booleano. Booleano forçaria a escolher entre acusar e
inocentar quando a resposta honesta é "não sei".

## Alternativas descartadas

**Postura restritiva** (marcar sempre que o CNAE não estiver numa lista branca estrita) —
produziria muito mais achados, e o produto pareceria mais potente. Descartada porque marcaria
supermercado que vende combustível, posto com CNAE de bar, e toda empresa de atividade mista.
O custo não é técnico: é uma acusação indevida com nome próprio.

**Lista branca por código exato de 7 dígitos** — mais precisa no papel. Descartada porque
exigiria enumerar milhares de subclasses e ficaria desatualizada a cada revisão da CNAE,
falhando justamente por omissão silenciosa.

## Consequências

- O motor vai achar **menos** do que acharia com postura restritiva. Isso é o desenho, não
  uma limitação a corrigir.
- A taxa de abstenção precisa ser medida e exibida: se `REG_KYB_001` se abstiver em 80% das
  despesas, a tabela está pobre demais e isso tem de ser visível, não silencioso.
- Afrouxar ou apertar a regra é editar uma tabela versionada em código, revisável linha a
  linha no diff — não mexer em lógica.
