# ADR-0011 — O V1 é o plano de execução inteiro

**Data:** 2026-08-20 · **Situação:** Aceita
**Substitui:** [ADR-0002](ADR-0002-escopo-v1-fatia-vertical.md)

## Contexto

O [ADR-0002](ADR-0002-escopo-v1-fatia-vertical.md) definiu o V1 como uma fatia vertical
restrita à Câmara, excluindo sete itens. As decisões de produto de 2026-08-20 trouxeram os
sete de volta, e a análise apontou que não havia mais definição acordada de escopo.

A decisão é fechar essa indefinição pelo caminho oposto ao que a análise sugeria: em vez de
recortar em três marcos, **assumir o plano de execução inteiro como V1**.

## Decisão

**V1 = as sete fases do [Plano de Execução](../especificacao/Explanando_Plano_de_Execucao.md).**

Não há mais recorte de "fora do escopo". Senado, pagamento PIX, Camada 2, API B2B, painel
SaaS Pro, contestação do gabinete, cards virais, favoritos e alertas são todos V1.

## Por que isso é defensável agora, e não era antes

O ADR-0002 existia para responder a um risco concreto: *"executado em ordem, o projeto só
produziria algo visível muito tarde — e nenhuma das hipóteses de negócio teria sido testada
até lá."*

**Esse risco já foi pago.** A [calibragem contra dado real](../registro/etapa-04b-calibragem-real.md)
rodou o motor sobre 28.540 despesas e provou a hipótese central: as regras determinísticas
encontram inconsistências sobre dado público real, com 2,7% de taxa de sinalização depois de
calibradas.

A fatia vertical cumpriu sua função. Ampliar o escopo agora é decisão informada, não aposta.

## O que a decisão **não** resolve

Ampliar escopo não cria fonte de dado. Quatro regras seguem sem como rodar, e isso independe
de decisão de produto:

| Regra | Bloqueio | Situação |
|---|---|---|
| `REG_NET_002` — vínculo com secretário de gabinete | A API da Câmara não tem endpoint de servidores (verificado: `405` em quatro rotas) | **Sem fonte** |
| `REG_NET_003` — mesmo endereço do escritório de apoio | Não há fonte do endereço do escritório | **Sem fonte** |
| `REG_GEO_001` — teletransporte | A despesa não traz local nem hora | **Impossível com o dado atual** |
| `REG_NET_001` — doação cruzada | Depende da base bruta do TSE | **Possível, com custo** — ver abaixo |

As três primeiras precisam de fonte nova (raspagem do portal da Câmara, dados do TSE de
pessoal, ou outra origem) ou permanecem fora — **por limitação de dado, não de escopo**.

## O TSE muda a premissa de infraestrutura

`REG_NET_001` entra no escopo, e ela exige a base de prestação de contas eleitorais do TSE —
que é distribuída como CSV bruto na casa de gigabytes.

Isso conflita diretamente com o [ADR-0010](ADR-0010-resolucoes-produto-infraestrutura.md) §1,
cujo objetivo declarado é caber nos 0,5 GB do plano gratuito do Neon. A medição mostrou que
só a Câmara ocupa 500–750 MB antes da otimização.

**Encaminhamento:** os dados do TSE não entram no Postgres. Entram como índice reduzido —
apenas CPF parcial e nome de doadores dos parlamentares em exercício — com o volume bruto no
R2. É exatamente o *tiered storage* que o ADR-0010 postergou, mas por outro motivo e antes de
2027.

## Consequências

- **A Etapa 03 (ingestão) volta a ficar incompleta.** Senado, TSE e os crawlers de contingência
  da Fase 3 do plano são fontes novas, cada uma com contrato próprio. Precisam de spike como o
  que a Etapa 01 fez para a Câmara — **modelar sem medir foi justamente o erro que a Etapa 01
  existiu para evitar**.
- **A Etapa 02 ganha as entidades que havia adiado:** `ContestacaoDespesa`, `Usuario`,
  `Favorito`, `SolicitacaoVarredura`, `HistoricoScore` e a camada de chaves de API do B2B.
- **A Etapa 04 ganha** `REG_NET_004`, a versão do motor na flag e os metadados de cobertura.
- **O risco que o ADR-0002 mitigava volta.** Com sete fases antes do lançamento, a data de
  publicação afasta. Isso agora é escolha consciente, e está registrado aqui.
- Os registros das etapas 01 a 04 tinham pendências justificadas por "fora do V1". Todas
  foram reclassificadas.
