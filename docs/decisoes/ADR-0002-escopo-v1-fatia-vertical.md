# ADR-0002 — V1 é uma fatia vertical só da Câmara

**Data:** 2026-08-18 · **Situação:** ⚠️ Superada em parte — ver nota abaixo

> [!warning] O escopo definido aqui não vale mais integralmente
> As decisões de produto tomadas em 2026-08-20 trouxeram de volta **todos os sete itens** que
> este ADR excluía do V1. Não há hoje uma definição acordada do que é o V1.
>
> A análise em [analise-do-adr-de-resolucoes.md](analise-do-adr-de-resolucoes.md) propõe um
> recorte em três marcos — V1 publicar, V1.1 monetizar, V2 comercial — que ainda **precisa de
> decisão**. Até lá, tratar este ADR como histórico da intenção original, não como escopo
> vigente.

## Contexto

O Plano de Execução tem 7 fases, indo de setup de infraestrutura até API B2B, painel SaaS Pro
e estratégia de lançamento. Executado em ordem, o projeto só produziria algo visível muito
tarde — e nenhuma das hipóteses de negócio teria sido testada até lá.

A hipótese central do produto não é "conseguimos construir isso". É: **o motor de regras
determinísticas, rodando sobre dado público real, encontra inconsistências suficientes para
sustentar um ranking que valha atenção.** Isso é falseável cedo e barato.

## Decisão

O V1 é uma **fatia vertical completa**, da ingestão à página pública, restrita a:

- **Somente Câmara dos Deputados.** 513 parlamentares, API REST estável e documentada em
  Swagger, com despesas já normalizadas.
- Ingestão CEAP + enriquecimento de CNPJ via BrasilAPI.
- Motor determinístico com as regras que os dados disponíveis sustentam.
- Score de risco e ranking público.

> [!IMPORTANT]
> **Esta decisão foi substituída pelo [ADR-0011](ADR-0011-v1-e-o-plano-inteiro.md)**
> em 2026-08-20. O V1 passou a ser o plano de execução inteiro. A lista abaixo deixou
> de valer; o registro fica porque o raciocínio que a produziu continua sendo o motivo
> de o ADR-0011 ser defensável — a fatia vertical foi construída e a hipótese central
> foi verificada contra 28.540 despesas reais antes de o escopo se alargar.

**Fora do V1 (histórico, não vale mais):** Senado, pagamento PIX, Camada 2 (LLM), API B2B, painel SaaS Pro, contestação
do gabinete e cards virais.

## Alternativas descartadas

**Fases 1 a 3 completas** (incluindo Senado, crawlers de contingência e snapshots) — mais
sólido como base, mas adia o momento em que existe algo para olhar, e investe em resiliência
antes de saber se o motor acha alguma coisa.

**MVP monetizável** (fatia vertical + PIX + Gemini) — testaria a disposição a pagar junto.
Descartado porque cobrar por uma varredura antes de saber se a varredura produz achado
relevante é testar a hipótese errada primeiro, e porque exige gateway, CNPJ e fiscal.

## Consequências

- O Senado entra depois: o modelo de domínio já nasce com `Parlamentar.Tipo`
  (1 = DeputadoFederal, 2 = Senador) para não exigir migração dolorosa.
- Nenhuma conta paga é necessária no V1: Câmara e BrasilAPI são públicas e sem autenticação;
  Postgres, Redis e fila rodam em Docker local.
- O critério de sucesso do V1 é factual: rodar o motor sobre a base real e medir quantas
  despesas recebem flag, por regra.
