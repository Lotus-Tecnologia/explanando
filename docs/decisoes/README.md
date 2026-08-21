# Decisões arquiteturais (ADRs)

Cada arquivo registra **uma** decisão: o contexto que a forçou, o que foi decidido, as
alternativas descartadas e o preço que se paga por ela.

Quando um ADR contradiz um documento de `docs/especificacao/`, **o ADR vence** — as
especificações são o material de partida; os ADRs são as decisões tomadas em cima delas.

| ADR | Decisão | Data | Situação |
|---|---|---|---|
| [0001](ADR-0001-frontend-nextjs.md) | Frontend em Next.js, não Blazor | 2026-08-18 | Aceita |
| [0002](ADR-0002-escopo-v1-fatia-vertical.md) | V1 é uma fatia vertical só da Câmara | 2026-08-18 | **Substituída pela [0011](ADR-0011-v1-e-o-plano-inteiro.md)** |
| [0003](ADR-0003-monorepo-com-git.md) | Monorepo, com git desde o commit inicial | 2026-08-18 | Aceita |
| [0004](ADR-0004-nomenclatura-tecnica.md) | Nomenclatura técnica em todo o produto | 2026-08-18 | Aceita |
| [0005](ADR-0005-tdd-e-niveis-de-teste.md) | TDD obrigatório, quatro níveis de teste | 2026-08-18 | Aceita |
| [0006](ADR-0006-arquitetura-verificada-por-teste.md) | Camadas verificadas por teste, não por revisão | 2026-08-18 | Aceita |
| [0007](ADR-0007-postura-permissiva-do-motor.md) | Motor permissivo: na dúvida, silêncio | 2026-08-19 | Aceita |
| [0008](ADR-0008-repositorio-privado.md) | O repositório permanece privado | 2026-08-19 | Aceita |
| [0009](ADR-0009-dois-numeros-nao-um.md) | Dois números: volume para o B2C, densidade para o B2B | 2026-08-20 | Aceita |
| [0010](ADR-0010-resolucoes-produto-infraestrutura.md) | Resoluções de produto e infraestrutura | 2026-08-20 | Aceita |
| [0011](ADR-0011-v1-e-o-plano-inteiro.md) | O V1 é o plano de execução inteiro | 2026-08-20 | Aceita |

## Questões abertas

[**questoes-abertas-de-negocio.md**](questoes-abertas-de-negocio.md) — fila de decisões de
produto que a implementação e a calibragem real levantaram e que ainda não têm ADR.

[**analise-das-respostas-parte-1.md**](analise-das-respostas-parte-1.md) — avaliação das
respostas dadas às oito questões, com medição da base real.

[**analise-do-adr-de-resolucoes.md**](analise-do-adr-de-resolucoes.md) — avaliação do
ADR-0010. Aponta que o escopo do V1 se desfez e propõe recorte em três marcos, e desmente com
medição o item de reconstrução de URL.
