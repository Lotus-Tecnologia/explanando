# Etapa 07 — Observabilidade e deploy

**Início:** 2026-08-21 · **Situação:** 🚧 Em andamento

---

## 1. Objetivo

Pôr o produto no ar. Até aqui tudo roda em `localhost`, e a interface inteira da
[Etapa 06](etapa-06-frontend.md) — 513 páginas estáticas, 516 endereços no sitemap, título e
descrição por parlamentar — existe para ser indexada. Indexação é o canal de aquisição
principal do Business Model Canvas; enquanto nada estiver publicado, nada disso entrega.

A etapa começa pela **limitação de taxa**, e não pelo deploy, por um motivo de ordem: a API é
pública e sem autenticação, e é ela que fica exposta primeiro.

---

## 2. Limitação de taxa — ✅ concluída

### 2.1 Por que agora

Até a Etapa 05, a única proteção da API era o teto de 200 itens por página. Isso bastava em
`localhost`. No Cloud Run cobra-se por tempo de contêiner e o Postgres é o do plano gratuito
do Neon: um raspador de terceiros deixa de ser incômodo e vira fatura. **"Custo fixo zero" é
premissa do projeto ([ADR-0001](../decisoes/ADR-0001-frontend-nextjs.md)), não meta.**

O objetivo **não** é dificultar o acesso ao dado — ele é público e queremos que circule. O
teto é generoso o bastante para um leitor nunca encostar nele, e baixo o bastante para que uma
varredura automatizada caiba no que o banco absorve.

### 2.2 O que existe

`LimitacaoDeTaxa` no projeto da API: balde de fichas por origem, **120 por minuto** com a
rajada inteira disponível de saída, sem fila.

| Configuração | Padrão | O quê |
|---|---|---|
| `RateLimit:Capacidade` | 120 | O tamanho do balde |
| `RateLimit:PorMinuto` | 120 | A reposição |
| `RateLimit:AtrasDeProxy` | `false` | Se há proxy confiável na frente |
| `RateLimit:ChaveInterna` | — | O segredo do nosso renderizador |

### 2.3 As quatro decisões que não são óbvias

**Fila zero.** Enfileirar prenderia a conexão esperando vaga, e no Cloud Run se paga por tempo
de contêiner — a fila transformaria uma rajada em conta, que é exatamente o que o limitador
existe para evitar. Recusa na hora.

**A partição é por leitor, e ler o IP do proxy destruiria isso.** Atrás de Cloudflare e Cloud
Run, o IP da conexão é o do **proxy**: todo mundo cairia na mesma partição e o teto viraria
global — a centésima pessoa a abrir a home levaria `429` por causa das noventa e nove
anteriores.

Só que confiar num cabeçalho que o cliente escreve é igualmente ruim: dá a qualquer um uma
partição nova por requisição, e o limitador vira decoração. Por isso `CF-Connecting-IP` e
`X-Forwarded-For` só são lidos quando `AtrasDeProxy` declara que há proxy na frente. **Quem
sabe disso é o desdobramento, não o código** — e há teste dos dois lados.

**A sonda `/saude` fica de fora.** Limitá-la faz a plataforma matar o contêiner exatamente no
momento de carga em que ele precisa continuar de pé.

**O `429` sai com `no-store`, e essa foi a armadilha da etapa.** O limitador recusa **antes**
de o endpoint rodar, então o filtro que carimba `Cache-Control` nas leituras nunca vê este
`429`. Sem cabeçalho, a CDN pode guardá-lo — e um `429` guardado no edge tranca todo mundo que
sai por aquele nó, por minutos, por causa de um raspador só. O cabeçalho é escrito no
`OnRejected`.

Junto vai `Retry-After`: sem ele, o cliente educado não tem como saber que basta esperar, e o
comportamento natural dele passa a ser tentar de novo na hora, em laço.

### 2.4 O nosso próprio frontend era o maior cliente — e isso foi medido

O build busca as três páginas de ranking e os **513 perfis de um endereço só**, com onze
processos em paralelo. Contra o teto público de 120, ele derruba o próprio limite.

Não foi suposição. Com a API rodando no teto padrão:

```
=== BUILD SEM A CHAVE ===
ErroDaApi: A API respondeu 429 para /api/v1/parlamentares/…/despesas?comFlag=true…
Export encountered an error on /parlamentares/[id]/page — exiting the build.
⨯ Next.js build worker exited with code: 1

=== BUILD COM A CHAVE ===
✓ Generating static pages (521/521)   ● 513 perfis SSG
```

A saída para isso é um segredo compartilhado em `X-Explanando-Interno`, que põe o renderizador
num balde vinte vezes maior. Duas escolhas embutidas:

- **A comparação é em tempo fixo.** Comparar por igualdade de string vazaria o tamanho do
  prefixo acertado pelo tempo de resposta.
- **A variável do frontend não leva `NEXT_PUBLIC_`**, e isso é o ponto: o Next embute as que
  levam no pacote que vai para o navegador, e chave embutida em pacote público deixa de ser
  chave. Há teste provando que ela é lida a cada chamada, e não congelada na importação.

E a API **avisa na subida** quando a chave não está configurada. Sem o aviso, o sintoma
apareceria como build quebrado num deploy futuro, longe da causa.

### 2.5 Medição contra a base real

| | |
|---|---|
| Rajada de 150 pedidos numa conexão, teto de 120 | **122 responderam `200`, 28 responderam `429`** |
| Tempo da rajada | 2 s |
| Corpo da recusa | `ProblemDetails`, `"status": 429`, `application/problem+json` |
| Cabeçalhos da recusa | `Cache-Control: no-store` · `Retry-After` |
| `/saude` durante a recusa | `200` |
| Com `X-Explanando-Interno` | `200` |

**Suíte: 41 testes E2E** (eram 32) e 96 no frontend (eram 93).

### 2.6 Uma medição minha que estava errada

A primeira verificação disparou 130 `curl` sequenciais contra o teto de 120 e **todos
passaram**. A leitura fácil seria "o limitador não está ligado".

Estava — e o errado era o método. Cada `curl` no Windows sobe um processo, e os 130 levaram
mais de um minuto; o balde repôs 120 fichas no meio do caminho. Refeito com uma conexão só, os
`429` apareceram na hora.

Vale registrar porque é o mesmo padrão da Etapa 01, quando oito requisições "provaram" que a
BrasilAPI não tinha rate limit e a primeira coleta real produziu 166 respostas `429`:
**medição com amostra ou ritmo errado descreve o experimento, não o sistema.**

---

## 3. O que falta nesta etapa

| Item | Situação |
|---|---|
| **Varredura global mensal agendada** | ⬜ O ADR-0010 §4 exige e o `BackgroundService` está ocioso desde a Etapa 03. Sem ela, o frescor do dado é desigual entre parlamentares — e isso é explorável num ranking com nome e foto |
| **Política de Privacidade publicada** | ⬜ Não é código. O rodapé já nomeia o Encarregado; falta a política, e a base legal citada no ADR-0010 §2 está trocada (art. 7º, VI é exercício de direitos em processo; o provável é legítimo interesse, art. 7º, IX, que exige avaliação documentada) |
| OpenTelemetry | ⬜ |
| Neon, Cloud Run, Cloudflare | ⬜ `AtrasDeProxy` precisa virar `true` no desdobramento, e a chave interna precisa existir dos dois lados |
