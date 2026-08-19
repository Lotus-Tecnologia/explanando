# ADR-0004 — Nomenclatura técnica em todo o produto

**Data:** 2026-08-18 · **Situação:** Aceita

## Contexto

O documento de Requisitos de Negócio (§1) é categórico: para mitigar risco de litígio por
difamação movido por pessoa pública, a aplicação **nunca** acusa parlamentar de crime.
Lista termos proibidos — "Lavagem de Dinheiro", "Fraude", "Crime", "Corrupção", "Desvio" —
e termos obrigatórios: "Inconsistência Financeira", "Atipicidade de Volume Financeiro",
"Fator de Risco Elevado", "Alerta de Compliance".

Só que os outros documentos violam a própria regra. O documento de Visão e Escopo (§2) chama
a funcionalidade de ranking de **"Top Mamadores"**. A Fase 5 do Plano de Execução especifica um
botão **"Compartilhar Denúncia"**. "Denúncia" é termo com peso jurídico próprio, e "mamadores"
é imputação de conduta — as duas coisas que a §1 existe para evitar.

A blindagem jurídica não funciona por meio termo: numa ação, é o vocabulário do produto inteiro
que é lido, incluindo marketing e nome de funcionalidade.

## Decisão

**Vocabulário técnico em toda a superfície do produto** — UI, API, logs, e-mails, nomes de
código, redes sociais e material de divulgação.

| Em vez de | Usar |
|---|---|
| Top Mamadores | Ranking de Volume de Gastos |
| Compartilhar Denúncia | Compartilhar Análise |
| Suspeita / Irregularidade | Inconsistência Financeira |
| Gasto suspeito | Atipicidade de Volume Financeiro |
| Político corrupto | Fator de Risco Elevado |

O achado é sempre apresentado como resultado de um motor de regras, identificado pelo código
da regra que disparou, com a conclusão deixada para o leitor.

## Alternativas descartadas

**Linguagem forte no B2C, técnica no B2B** — maior potencial viral. Descartado: a exposição
jurídica está justamente no B2C, que é a superfície pública e indexada, e é ela que seria
citada numa notificação.

**Termo forte no marketing, técnico no produto** — a fronteira é impossível de policiar na
prática, e um print da rede social é anexo de processo tão válido quanto um print da tela.

## Consequências

- A viralidade passa a depender do **dado** provocar reação, não do adjetivo. Isso é consistente
  com o que a teoria de propagação prevê: emoção de alta excitação é gatilho de compartilhamento,
  e um valor concreto com a regra que ele quebrou produz essa emoção sozinho.
- Nomes de tipo, endpoint e evento no código seguem a mesma regra — `FlagAnomalia`,
  `ScoreRiscoParlamentar`, `Inconsistencia`, nunca `Fraude` ou `Suspeita`.
- Vale um teste automatizado varrendo a base atrás dos termos proibidos. Fica anotado para a
  etapa em que existir texto voltado ao usuário.
