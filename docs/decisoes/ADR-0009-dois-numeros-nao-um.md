# ADR-0009 — Dois números, não um: volume para o B2C, densidade para o B2B

**Data:** 2026-08-20 · **Situação:** Aceita

## Contexto

A calibragem contra dado real ([etapa-04b](../registro/etapa-04b-calibragem-real.md)) expôs
uma distorção no ranking:

| | Score | Flags vermelhas | Despesas |
|---|---|---|---|
| Afonso Hamm | 107 | **1** | 3.575 |
| Albuquerque | 97 | **14** | 752 |

Hamm aparecia **acima** de Albuquerque tendo uma flag vermelha contra quatorze. A diferença
era ter emitido quase cinco vezes mais notas.

O `ScoreCalculado` era a soma bruta dos pesos das flags. Somar sem dividir mede **atividade
administrativa**, não risco: quem emite mais notas acumula mais chances de ser marcado, e
sobe no ranking por isso.

O problema de fundo é anterior ao cálculo. O [Business Model Canvas](../especificacao/Explanando_Analise_Lacunas_B2B.md)
descreve **dois compradores com necessidades opostas**, e havia um número só tentando servir
aos dois.

## Decisão

**Dois números independentes, cada um honesto sobre o que mede.**

### `VolumeTotalGasto` — o número do B2C

Soma do que o erário efetivamente pagou, em reais. É o "Ranking de Volume de Gastos"
([ADR-0004](ADR-0004-nomenclatura-tecnica.md)), a peça pública e viral.

É o número mais **seguro juridicamente** de todo o produto: gastar a cota inteira é legal, e
publicar quanto se gastou não imputa conduta nenhuma. E funciona melhor como moeda social —
"R$ 1,2 milhão" se compartilha; "score 235" não significa nada para o leitor.

Estornos abatem: usa `ValorParaScore`, que é o líquido com sinal.

### `IndiceDeRisco` — o número do B2B

Peso das flags dividido pelas despesas avaliadas, em pontos por 100 despesas. É o ativo da
API de *due diligence* de PEPs, sob a marca Lotustech.

É o que justifica a cobrança. O ranking de volume qualquer um extrai dos dados abertos numa
tarde; a **densidade de indicadores** exige o motor de regras. Um banco que recebesse a soma
bruta veria na hora que o PEP mais bem pontuado é apenas o que apresenta mais recibos, e
descartaria a API.

### `PesoDasFlags` e `TotalFlagsVermelhas` — contexto, nunca ordenação

Continuam existindo e aparecem na página do parlamentar. **Não ordenam ranking nenhum.**

Ordenar por contagem absoluta de flags seria o pior dos dois mundos: mais arriscado
juridicamente que o volume — é "quem tem mais irregularidades" — e menos informativo que a
densidade.

### Piso amostral

Densidade pura cria a distorção inversa: um parlamentar com 10 despesas e 2 flags marca 20%
e lidera o ranking com prova fina.

Abaixo de `PisoAmostralParaIndiceDeRisco` despesas avaliadas, o parlamentar recebe
`AmostraSuficiente = false` e **fica fora do ranking de risco** — embora continue no ranking
de volume, que não depende de amostra.

É a mesma lógica do [ADR-0007](ADR-0007-postura-permissiva-do-motor.md): na dúvida, o motor
se cala. Amostra pequena é dúvida.

## Alternativas descartadas

**Um score só, normalizado** — resolveria a distorção mas perderia o ativo viral. O público
não compartilha índice; compartilha valor em reais. E o ranking de volume é o único número do
produto que não corre risco jurídico nenhum.

**Um score só, por volume** — é o que existia. Serve ao B2C e é inútil para o B2B, que é onde
está a receita recorrente.

**Ranking por contagem absoluta de flags** — descartado pelo motivo da seção anterior.

## Consequências

- `ScoreRiscoParlamentar` deixa de ter `ScoreCalculado` e passa a guardar `VolumeTotalGasto`,
  `DespesasAvaliadas`, `PesoDasFlags` e `IndiceDeRisco`. O nome antigo era honesto sobre o
  cálculo e desonesto sobre o significado.
- `Recalcular` passa a receber as **despesas**, não só as flags — sem o denominador não há
  índice.
- A API pública (Etapa 05) expõe dois endpoints de ranking, não um. O frontend escolhe qual
  é a manchete; hoje a resposta é o volume.
- O piso amostral vira parâmetro auditável em `ParametrosDoMotor`, junto dos demais limiares.
