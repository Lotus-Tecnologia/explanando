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
| 05 | API pública | ⏳ Próxima | — |
| 06 | Frontend Next.js | ⬜ Não iniciada | — |
| 07 | Observabilidade e deploy | ⬜ Não iniciada | — |

## Escopo do V1

Definido no [ADR-0002](../decisoes/ADR-0002-escopo-v1-fatia-vertical.md): fatia vertical
completa, **somente Câmara dos Deputados**, da ingestão à página pública de ranking.

Fora do V1: Senado, pagamento PIX, Camada 2 (LLM), API B2B, painel SaaS Pro, contestação do
gabinete e cards virais.
