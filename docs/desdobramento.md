# Desdobramento — contas, segredos e a ordem que importa

Guia operacional do primeiro deploy. Os nomes de variável aqui foram lidos do código, não
escritos de memória — errar um faz o serviço subir e não funcionar.

> **Ordem curta:** domínio → Neon → migrations pelo worker → API no Cloud Run → Cloudflare na
> frente → frontend no Cloudflare Workers → agendador. O passo das migrations vem antes da
> API de propósito; ver §6.1.

---

## 1. As contas

| Serviço | Para quê | Plano | Observação |
|---|---|---|---|
| **Registro.br** | O domínio `explanando.com.br` | ~R$ 40/ano | Hoje **não resolve em DNS**, e ele já está nos canônicos e no `sitemap.xml`. Exige CPF ou CNPJ brasileiro |
| ~~**Neon**~~ | PostgreSQL | ~~Gratuito (512 MB)~~ | ⚠️ **Substituído pelo [ADR-0019](decisoes/ADR-0019-tudo-numa-vps.md).** Os 317 MB registrados aqui são de antes das Etapas 11, 12 e 14: medido em 2026-09-24, a base local já ocupa **364 MB só com a Câmara**. O banco passa a rodar na VPS |
| **Google Cloud** | Cloud Run (API), Cloud Run Jobs (worker), Artifact Registry (imagens), Cloud Scheduler | Gratuito na faixa de uso | Exige cartão cadastrado mesmo dentro da cota gratuita |
| **Cloudflare** | DNS, CDN e WAF | Gratuito | É ela que absorve o pico viral (ADR-0001). Sem ela, a premissa de custo não se sustenta |
| **Cloudflare Workers** | Frontend Next.js, via `@opennextjs/cloudflare` | Gratuito | Mesma conta do DNS. Permite uso comercial e não se importa com quem é dono do repositório — ver [ADR-0015](decisoes/ADR-0015-frontend-no-cloudflare.md) |
| **Cloudflare R2** | O cache incremental do ISR | Gratuito (10 GB) | Balde `explanando-cache-incremental`. **Sem ele o `revalidate` não tem onde guardar a página** |

**Ainda não precisa:** Upstash (o `HybridCache` roda em memória sem ele), coletor de OTel,
Mercado Pago, Google AI Studio e Resend. Entram com os blocos que dependem deles.

---

## 2. Os segredos da API — Cloud Run *service*

| Variável | Obrigatória | Valor |
|---|---|---|
| `EXPLANANDO_CONEXAO` | **sim** | A string do Neon, com `Ssl Mode=Require` |
| `RateLimit__AtrasDeProxy` | **sim**, atrás da Cloudflare | `true` |
| `RateLimit__ChaveInterna` | **sim** | Segredo compartilhado com o Worker do frontend. Gere com `openssl rand -hex 32` |
| `RateLimit__Capacidade` | não | Padrão 120 |
| `RateLimit__PorMinuto` | não | Padrão 120 |
| `EXPLANANDO_REDIS` | não | Só se quiser cache distribuído; sem ela roda em memória |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | não | Sem ela, **nenhum exportador é registrado** — de propósito |
| `Observabilidade__Amostragem` | não | Padrão `1.0`. Baixe num pico, sem redeploy |
| `PORT` | injetada | O Cloud Run põe sozinho; a imagem obedece |

> O duplo sublinhado **não é typo**: é como o .NET mapeia `RateLimit:Capacidade` para variável
> de ambiente. Um sublinhado só não funciona.

**Onde colocar:** Cloud Run → serviço → *Variables & Secrets*. A string do Neon e a chave
interna vão em **Secret Manager** e são referenciadas ali; as demais podem ser variáveis
simples.

## 3. Os segredos do worker — Cloud Run *job*

| Variável | Obrigatória | Valor |
|---|---|---|
| `EXPLANANDO_CONEXAO` | **sim** | A mesma string, mas do endpoint **direto** do Neon (§5) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | não | Idem |

**Tempo limite da tarefa: no mínimo 14 horas.** A coleta completa dos 513 leva **~12,6 h**, e
quase tudo é espera pelo limitador da BrasilAPI. O padrão do Cloud Run Jobs é 10 minutos — com
ele, a tarefa morre e o checkpoint faz a próxima recomeçar de onde parou, mas o ciclo nunca
fecha. O teto da plataforma é 24 h.

## 4. Os segredos do frontend — Cloudflare Workers

O frontend deixou de ir para a Vercel ([ADR-0015](decisoes/ADR-0015-frontend-no-cloudflare.md)):
o plano Hobby não deploya repositório de organização **e** proíbe uso comercial, e o Pro custa
US$ 20 por assento/mês antes de haver receita.

| Variável | Obrigatória | Valor |
|---|---|---|
| `NEXT_PUBLIC_API` | **sim** | `https://api.explanando.com.br` |
| `NEXT_PUBLIC_SITE_URL` | **sim** | `https://explanando.com.br` |
| `EXPLANANDO_CHAVE_INTERNA` | **sim** | **O mesmo valor** de `RateLimit__ChaveInterna` |
| `NEXT_PUBLIC_INDEXAVEL` | não | **Ausente = não indexa**, que é o padrão seguro. Ligue com a grafia exata `true` só depois que a API estiver no ar e um deploy tiver gerado as 513 páginas |

As demais `NEXT_PUBLIC_` são lidas **no build** e podem ser variáveis simples. A chave interna é
segredo: `npx wrangler secret put EXPLANANDO_CHAVE_INTERNA`.

O balde do R2 já está declarado em `wrangler.jsonc` como `NEXT_INC_CACHE_R2_BUCKET`; ele só
precisa existir na conta com o nome `explanando-cache-incremental`.

**Os comandos:**

```bash
npx wrangler login          # a autenticação é sua; nenhuma credencial passa por outro lugar
npm run cf:build            # monta o Worker
npm run cf:deploy           # publica
```

> **Não rode isso no Windows.** O adaptador avisa que *"could encounter unpredictable failures
> during runtime"*, e foi o que aconteceu na medição: a primeira execução completou, as
> seguintes penduraram em toda rota. Use WSL, ou deixe o CI publicar.

> **`EXPLANANDO_CHAVE_INTERNA` não leva `NEXT_PUBLIC_`, e isso é o ponto.** O Next embute as
> que levam o prefixo no pacote que vai para o navegador, e chave embutida em pacote público
> deixa de ser chave.
>
> **Sem ela o build quebra.** Não é hipótese: o build busca as três páginas de ranking e os 513
> perfis de um endereço só, com onze processos em paralelo, e bate no teto de 120/min. Medido —
> `ErroDaApi: A API respondeu 429` e `Next.js build worker exited with code: 1`.

**O cache do build precisa ir para o R2.** As 513 páginas não saem como HTML estático — saem
como ~1.557 arquivos de cache incremental. Sem populá-los, toda página é *miss* na primeira
visita: funciona, mas desperdiça a pré-renderização inteira.

## 5. GitHub Actions

**Nenhum segredo é necessário hoje.** O CI constrói as duas imagens mas não publica. Quando
publicar no Artifact Registry, use **Workload Identity Federation** em vez de chave de conta de
serviço em JSON — chave de serviço num repositório é credencial de longa vida que ninguém
lembra de girar.

---

## 6. A ordem, e por que ela importa

### 6.1 A API não roda migrations — de propósito

Decisão da Etapa 05: API de leitura não mexe em esquema. **Quem cria as tabelas é o worker**,
que roda `MigrateAsync` na subida de qualquer comando.

Consequência para o deploy: **rode o job do worker uma vez antes de publicar a API.** Publicar a
API primeiro contra um banco vazio dá HTTP 500 — que já aconteceu uma vez, e os 22 testes E2E
daquela época não pegaram, porque cada um tinha o próprio banco semeado.

```
gcloud run jobs execute explanando-worker --args=consolidar
```

### 6.2 Neon tem dois endpoints, e eles não são intercambiáveis

O endpoint **pooled** (`-pooler` no host) é o certo para a API: muitas conexões curtas. O
**direto** é o certo para o worker, porque migrations e transações longas precisam de sessão
de verdade.

Nos dois casos, `Ssl Mode=Require`.

### 6.3 A nuvem da Cloudflare precisa estar laranja

`RateLimit__AtrasDeProxy=true` faz a API confiar em `CF-Connecting-IP` e `X-Forwarded-For` para
saber quem é o leitor. **Isso só é seguro quando existe de fato um proxy na frente que
sobrescreve esses cabeçalhos.**

Se o DNS estiver em modo "DNS only" (nuvem cinza), o cabeçalho passa a vir do cliente — e
qualquer um ganha uma partição nova por requisição, tornando o limitador decoração. Ligue o
proxy **antes** de pôr a variável em `true`.

### 6.4 Deixe o Cloud Run em `min-instances=0`

É o *scale to zero* em que a premissa de custo se apoia. O primeiro acesso depois de um período
ocioso paga a partida a frio; o resto a CDN absorve.

---

## 7. Depois que estiver no ar

| | |
|---|---|
| **Agendar o ciclo mensal** | Cloud Scheduler → Cloud Run Job, `0 3 1 * *`. O comando checa o vencimento sozinho e sai em 0 se a base estiver fresca, então disparar demais não custa nada |
| **Conferir o `Cache-Control` no edge** | `curl -I https://explanando.com.br` tem de mostrar `cf-cache-status: HIT` na segunda chamada |
| **Conferir o sitemap** | `https://explanando.com.br/sitemap.xml` deve listar 517 endereços |
| **Google Search Console** | Sem ele, indexação é torcida. É o canal de aquisição principal do Business Model Canvas |

---

## 8. O que continua faltando, e não é conta

- **Avaliação de legítimo interesse documentada** — a ANPD pode solicitá-la (art. 7º, IX). A
  política está publicada; a avaliação que a sustenta, não.
- **Revisão jurídica** da política publicada.
- **Limites da CEAP 2023–2025 e da CEAPS** — curadoria humana, dos Atos da Mesa. Enquanto não
  vierem, `REG_TX_002` reporta `SemDado` honestamente, e a interface diz isso.
