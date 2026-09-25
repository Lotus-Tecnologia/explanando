# ADR-0019 — Tudo numa VPS, e o banco deixa de caber em 512 MB

**Data:** 2026-09-24
**Situação:** aceito
**Atualiza:** [ADR-0001](ADR-0001-frontend-nextjs.md) · [ADR-0010 §1](ADR-0010-resolucoes-produto-infraestrutura.md) · [ADR-0015](ADR-0015-frontend-no-cloudflare.md)
**Responde:** [questoes-abertas-de-negocio.md §1](questoes-abertas-de-negocio.md)

---

## Contexto

O desenho de infraestrutura até aqui tinha três casas: o frontend no **Cloudflare Workers**
(ADR-0015), a API e o worker no **Cloud Run**, e o PostgreSQL no **Neon**, plano gratuito.

Duas coisas empurraram para rever isso.

**A primeira é que o produto nunca subiu.** O item que trava a saída do modo degradado é o
mesmo desde agosto, e não é código: `gcloud auth login`, projeto com faturamento habilitado,
DNS. O Cloud Run é gratuito até um teto generoso, mas exige **conta com cartão**, e essa
fricção segurou o lançamento por mais de um mês.

**A segunda é que o teto do Neon já está moldando o produto.** O
[ADR-0010 §1](ADR-0010-resolucoes-produto-infraestrutura.md) existe **inteiro** para caber em
0,5 GB: o `hash_unico` virou `bytea`, a `url_documento` foi removida da tabela e passou a ser
reconstruída em tempo de execução, o `tipo_despesa` virou `smallint`, e o *tiered storage*
para o R2 foi adiado para 2027. O documento de questões abertas já registrava, em §1, que
*"uma legislatura completa estoura o plano gratuito — não é problema de escala futura, é o
custo de estar de pé com o V1"*, e listava três saídas: pagar o Neon, reduzir a retenção, ou
**trocar de provedor**.

## O que foi medido

**A base local, hoje: 364 MB.**

| Tabela | |
|---|---:|
| `despesas` | 255 MB |
| `unidades_do_estado` | 44 MB |
| `sociedades` | 18 MB |
| `flags_anomalias` | 15 MB |
| `fornecedores` | 14 MB |

**E isso é só a Câmara** — 513 deputados e 711.796 despesas. Os 118 senadores não estão
nesta base. O teto do plano gratuito do Neon é **512 MB**.

> **O número no `desdobramento.md` está defasado.** Ele registra *"a base ocupa 317 MB —
> cabe com 38% de folga"*. Aquilo foi medido antes das Etapas 11, 12 e 14 — o grafo do
> Estado sozinho pesa 44 MB. A folga de 38% não existe mais, e a legislatura 57 só fecha em
> 2027, com despesa entrando todo mês.

**O Always Free da Oracle, conferido na documentação em 2026-09-24:**

| Recurso | |
|---|---|
| Ampere A1 (ARM) | 1.500 OCPU-horas e 9.000 GB-horas por mês — **2 OCPUs e 12 GB de RAM** rodando o mês inteiro |
| Block volume | **200 GB**, dos quais o boot volume mínimo é 47 GB por instância |
| Object Storage | 20 GB e 50.000 requisições por mês |

> **Uma correção minha, para o registro.** Na conversa que originou esta decisão eu afirmei
> "4 cores ARM e 24 GB de RAM". Era o que a Oracle oferecia antes; a documentação de hoje diz
> **2 OCPUs e 12 GB**. O argumento não muda — 12 GB e 200 GB seguem folgadíssimos para uma
> base de 364 MB —, mas o número estava errado e citá-lo de memória foi o erro.

## Decisões

### 1. Uma VPS Always Free da Oracle roda tudo

PostgreSQL, API, worker e frontend na mesma máquina ARM. O que se ganha não é desempenho: é
**sair do lugar**. Não há conta com faturamento a habilitar, não há cartão, e o worker deixa
de esbarrar no limite de duração de requisição do Cloud Run — a coleta mensal leva horas.

### 2. O PostgreSQL passa a ser local, e o Neon é aposentado

**200 GB contra 512 MB são quatrocentas vezes mais espaço.** Some a pergunta aberta de §1,
some o plano de *tiered storage* para 2027, e a poda deixa de ser obrigação de
infraestrutura — continua existindo como política de retenção, que é outra conversa e tem
outro dono.

### 3. O Cloudflare fica, como CDN — não como runtime

Aqui está a parte que se aposenta do [ADR-0015](ADR-0015-frontend-no-cloudflare.md): sai o
`@opennextjs/cloudflare`, sai o `wrangler`, sai o `.open-next`. O Next roda em Node na
própria máquina, que é como ele foi feito para rodar.

**Mas o Cloudflare não sai da frente.** A aposta do [ADR-0001](ADR-0001-frontend-nextjs.md) é
uma frase: *"é a CDN, e não o Redis, que absorve o pico viral"*. Duas OCPUs não absorvem uma
matéria no Fantástico; a CDN com as 863 páginas estáticas em cache, sim. O proxy também é o
que torna seguro o `RateLimit__AtrasDeProxy=true`, que já está previsto.

Trocamos **um produto do Cloudflare por outro**, e o mais simples dos dois.

### 4. O backup diário fora da máquina é condição, não consequência

A Oracle **recupera instância Always Free ociosa**. O critério, na documentação: durante 7
dias, CPU no percentil 95 abaixo de 20%, **e** rede abaixo de 20%, **e** memória abaixo de
20% — esta última só nas A1, que é exatamente a máquina que vamos usar.

Uma VPS recuperada com o único PostgreSQL dentro leva o dado junto. Então:

- `pg_dump | gzip` diário para armazenamento de objeto **fora da máquina**;
- de preferência no **R2 da Cloudflare** (10 GB grátis) e não no Object Storage da própria
  Oracle, porque backup no mesmo fornecedor que pode recuperar a instância é meio backup;
- e um teste de restauração, porque backup que nunca foi restaurado é esperança, não backup.

O dump comprimido de uma base de 364 MB fica na casa de dezenas de megabytes. Cabe com folga
absurda.

### 5. O PostgreSQL usa a RAM que tem — e isso não é só desempenho

`shared_buffers` generoso, na casa dos 3 GB dos 12 disponíveis. É a configuração certa por
mérito próprio: hoje o Postgres roda em contêiner com o padrão de 128 MB, o que para uma base
de 364 MB significa ir ao disco por tudo.

**E tem um segundo efeito, que vale declarar em vez de deixar implícito:** 20% de 12 GB são
2,4 GB, e uma máquina com Postgres, API e Next ociosos ficaria bem abaixo disso. Usar a
memória mantém a instância fora de um dos três critérios de ociosidade — e basta **um** dos
três falhar para a máquina não ser recuperada.

### 6. As otimizações do ADR-0010 §1 ficam, menos uma que volta à mesa

`bytea` no hash e `smallint` no tipo de despesa continuam certos **por mérito**, e não por
pressão de espaço: índice menor é busca mais rápida em qualquer disco.

**A `url_documento` derivada volta à mesa.** Ela foi removida da tabela para economizar
espaço, e reconstruí-la em tempo de execução custa complexidade espalhada por API e
frontend. Sem o teto de 512 MB, a economia deixa de pagar essa complexidade. Não é decisão
deste ADR — é um item que passa a ter sentido reabrir.

## Consequências

**O caminho para o ar encurta.** Uma máquina, um `compose`, um DNS. Some o Cloud Run, some o
Neon, some o adaptador do Cloudflare Workers, some a ordem delicada de "rode o job do worker
antes de publicar a API" — ela continua valendo, mas agora tudo sobe junto.

**O que era gerenciado passa a ser nosso:** backup, restauração, atualização do PostgreSQL,
patch do sistema. O Neon fazia isso sem ninguém pensar. Esta é a conta real da mudança, e ela
se paga em espaço e em autonomia — mas se paga em trabalho.

**Um disco, um ponto de falha.** Não há réplica. A mitigação é o item 4, e ele não é
opcional.

**ARM.** O .NET 10 e o Node publicam para `linux-arm64` sem esforço, mas a imagem do
contêiner precisa ser construída para a arquitetura certa — é o tipo de coisa que falha na
primeira subida e confunde, porque o erro aparece como "exec format error" e não como
"arquitetura errada".

**A capacidade A1 falta.** É comum a criação da instância responder *"out of host capacity"*
nas regiões populares. Não é impeditivo; é um motivo para criar a máquina antes de precisar
dela.

**O `desdobramento.md` precisa ser reescrito**, e o
[ADR-0015](ADR-0015-frontend-no-cloudflare.md) passa a valer só pela metade: o diagnóstico
sobre a Vercel continua correto e é por isso que não voltamos para lá; a escolha do Workers
como runtime é o que esta decisão substitui.
