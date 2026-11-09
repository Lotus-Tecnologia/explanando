# Decisões arquiteturais (ADRs)

Cada arquivo registra **uma** decisão: o contexto que a forçou, o que foi decidido, as
alternativas descartadas e o preço que se paga por ela.

Quando um ADR contradiz um documento de `docs/especificacao/`, **o ADR vence** — as
especificações são o material de partida; os ADRs são as decisões tomadas em cima delas.

| ADR | Decisão | Data | Situação |
|---|---|---|---|
| [0001](ADR-0001-frontend-nextjs.md) | Frontend em Next.js, não Blazor | 2026-08-18 | Aceita — **host atualizado pela [0015](ADR-0015-frontend-no-cloudflare.md)** |
| [0002](ADR-0002-escopo-v1-fatia-vertical.md) | V1 é uma fatia vertical só da Câmara | 2026-08-18 | **Substituída pela [0011](ADR-0011-v1-e-o-plano-inteiro.md)** |
| [0003](ADR-0003-monorepo-com-git.md) | Monorepo, com git desde o commit inicial | 2026-08-18 | Aceita |
| [0004](ADR-0004-nomenclatura-tecnica.md) | Nomenclatura técnica em todo o produto | 2026-08-18 | Aceita |
| [0005](ADR-0005-tdd-e-niveis-de-teste.md) | TDD obrigatório, quatro níveis de teste | 2026-08-18 | Aceita |
| [0006](ADR-0006-arquitetura-verificada-por-teste.md) | Camadas verificadas por teste, não por revisão | 2026-08-18 | Aceita |
| [0007](ADR-0007-postura-permissiva-do-motor.md) | Motor permissivo: na dúvida, silêncio | 2026-08-19 | Aceita |
| [0008](ADR-0008-repositorio-privado.md) | O repositório permanece privado | 2026-08-19 | Superada (0022) |
| [0009](ADR-0009-dois-numeros-nao-um.md) | Dois números: volume para o B2C, densidade para o B2B | 2026-08-20 | Aceita |
| [0010](ADR-0010-resolucoes-produto-infraestrutura.md) | Resoluções de produto e infraestrutura | 2026-08-20 | Aceita — **§2 e §4 atualizados pela [0014](ADR-0014-lacuna-camada-2-e-base-legal.md)** |
| [0011](ADR-0011-v1-e-o-plano-inteiro.md) | O V1 é o plano de execução inteiro | 2026-08-20 | Aceita |
| [0012](ADR-0012-assimetria-camara-senado.md) | A assimetria entre Câmara e Senado é exibida, não escondida | 2026-08-21 | Aceita |
| [0013](ADR-0013-identidade-visual-e-taxonomia-de-risco.md) | Identidade visual: a taxonomia de risco é o ADR-0004 em pixels | 2026-08-21 | Aceita |
| [0014](ADR-0014-lacuna-camada-2-e-base-legal.md) | A lacuna como informação, o dossiê como produto, e a base legal corrigida | 2026-08-22 | Aceita |
| [0015](ADR-0015-frontend-no-cloudflare.md) | O frontend roda no Cloudflare Workers, não na Vercel | 2026-08-22 | Aceita |
| [0016](ADR-0016-grafo-do-estado.md) | O grafo do Estado é outro contexto, não outro ranking | 2026-09-19 | Aceita |
| [0017](ADR-0017-mapa-de-fornecedores.md) | O mapa de pessoas usa a aresta que temos, não a que a referência usa | 2026-09-19 | Aceita |
| [0018](ADR-0018-cadeiras-de-comando.md) | Nome de pessoa no banco, uma vez, com endereço de origem | 2026-09-21 | Aceita |
| [0019](ADR-0019-tudo-numa-vps.md) | Tudo numa VPS, e o banco deixa de caber em 512 MB | 2026-09-24 | Aceita |
| [0020](ADR-0020-participacao-publica-adiada.md) | Discussão pública e notas da comunidade esperam audiência e parecer jurídico | 2026-09-24 | **Adiada** |
| [0021](ADR-0021-ia-espera-receita.md) | A narrativa por IA espera a receita pagar por ela | 2026-09-24 | **Adiada** |
| [0022](ADR-0022-encerramento.md) | Encerramento: repositório aberto, sem hospedagem | 2026-11-08 | Aceita |

## Questões abertas

As questões de negócio e as respostas a elas saíram do repositório público no encerramento
([ADR-0022](ADR-0022-encerramento.md)).

[**analise-do-adr-de-resolucoes.md**](analise-do-adr-de-resolucoes.md) — avaliação do
ADR-0010. Aponta que o escopo do V1 se desfez e propõe recorte em três marcos, e desmente com
medição o item de reconstrução de URL.
