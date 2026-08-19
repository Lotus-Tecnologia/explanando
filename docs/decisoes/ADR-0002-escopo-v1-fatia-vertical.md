# ADR-0002 — V1 é uma fatia vertical só da Câmara

**Data:** 2026-08-18 · **Situação:** Aceita

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

**Fora do V1:** Senado, pagamento PIX, Camada 2 (LLM), API B2B, painel SaaS Pro, contestação
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
