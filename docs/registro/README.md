# Registro de execução

Diário do projeto. Cada etapa tem um arquivo com **o que foi definido, o que foi planejado, o
que foi de fato executado, o que foi descoberto no caminho e o que ficou pendente**.

A diferença entre isto e os [ADRs](../decisoes/README.md): o ADR guarda **uma decisão** e o
raciocínio dela, e é estável. O registro guarda **o andamento** — inclusive o que deu errado,
o que contrariou o plano e o que sobrou para depois.

> **Se você está retomando o projeto depois de um tempo — ou é um agente começando uma sessão
> nova — leia este índice e o arquivo da última etapa antes de mexer em qualquer coisa.**

## Convenção

Um arquivo por etapa, nomeado `etapa-NN-nome.md`, com as seções:

1. **Objetivo** — o que essa etapa precisava entregar
2. **Definido** — decisões tomadas dentro da etapa
3. **Executado** — o que existe em disco ao fim dela
4. **Testes** — o que foi coberto, em que nível, e o resultado
5. **Descobertas** — o que só apareceu ao executar, incluindo o que contrariou o plano
6. **Pendências** — o que ficou para depois, e por quê
7. **Próxima etapa** — o que vem a seguir e o que ela pressupõe

## Etapas

| # | Etapa | Situação | Registro |
|---|---|---|---|
| 00 | Fundação e git | ✅ Concluída | [etapa-00-fundacao.md](etapa-00-fundacao.md) |
| 01 | Spike de contratos das APIs | ✅ Concluída | [etapa-01-contratos.md](etapa-01-contratos.md) |
| 02 | Domínio e EF Core | ✅ Concluída | [etapa-02-dominio-e-efcore.md](etapa-02-dominio-e-efcore.md) |
| 03 | Ingestão (Câmara + BrasilAPI) | ✅ Concluída | [etapa-03-ingestao.md](etapa-03-ingestao.md) |
| 04 | Motor de regras determinísticas | ✅ Concluída | [etapa-04-motor-de-regras.md](etapa-04-motor-de-regras.md) |
| 04b | Calibragem contra dado real | ✅ Concluída | [etapa-04b-calibragem-real.md](etapa-04b-calibragem-real.md) |
| 04c | Armazenamento antes da coleta | ✅ Concluída | [etapa-04c-armazenamento.md](etapa-04c-armazenamento.md) |
| 05 | API pública | ⏳ Próxima | — |
| 06 | Frontend Next.js | ⬜ Não iniciada | — |
| 07 | Observabilidade e deploy | ⬜ Não iniciada | — |

## Escopo do V1

Definido no [ADR-0011](../decisoes/ADR-0011-v1-e-o-plano-inteiro.md): **o V1 é o plano de
execução inteiro** — Câmara e Senado, motor determinístico e Camada 2, página pública, API
B2B, painel Pro, contestação do gabinete, varredura paga e cards virais.

O [ADR-0002](../decisoes/ADR-0002-escopo-v1-fatia-vertical.md), que recortava o V1 numa
fatia vertical só da Câmara, foi substituído em 2026-08-20. O recorte não foi abandonado por
otimismo: ele foi **construído e verificado** primeiro. A fatia rodou contra 28.540 despesas
reais de 20 deputados antes de o escopo se alargar, e é esse risco já pago que torna a
decisão defensável.

**O que continua fora, e não por escolha:** quatro regras da especificação não têm fonte de
dado pública que as sustente hoje — `REG_NET_001` (doação cruzada) depende de carga do TSE,
`REG_NET_002` respondeu 405 em quatro rotas, `REG_NET_003` não tem fonte, e `REG_GEO_001`
exige local e hora que a despesa não traz. Alargar o escopo não cria dado que não existe; o
ADR-0011 registra cada uma com o motivo.
