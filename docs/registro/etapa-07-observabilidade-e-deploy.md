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

## 3. Varredura global mensal — ✅ concluída

### 3.1 O `BackgroundService` era um esqueleto que dizia estar ocioso

Ele existia desde a Etapa 00 logando *"nenhuma fila conectada ainda (previsto para a Etapa
3)"* — e estamos na 7. Não era código morto por acaso: era uma funcionalidade que o
[ADR-0010](../decisoes/ADR-0010-resolucoes-produto-infraestrutura.md) §4 exige e que nunca
tinha sido escrita.

### 3.2 A regra é sobre justiça, não sobre operação

O dado também é atualizado por varredura paga sob demanda, então dois parlamentares podem ter
frescor diferente. Num ranking público com nome e foto, quem foi varrido mais recentemente
aparece pior — não por ser, mas por ter sido olhado mais perto. E é explorável: basta o
adversário de alguém pagar varreduras repetidas para manter aquele dado fresco enquanto os
aliados envelhecem.

**Daí a decisão central: o vencimento olha para o parlamentar MAIS ATRASADO.** Com o mais
recente, uma única varredura paga por mês manteria a base "atualizada" para sempre enquanto os
outros 512 envelheciam — exatamente a distorção que a passada existe para corrigir. É um `Min`
onde a leitura descuidada escreveria `Max`, e há teste de integração contra Postgres real
montando o cenário: um varrido hoje, dois parados há um ano, e o ciclo tem de vencer.

### 3.3 Nenhuma tabela nova

A data da última varredura de cada parlamentar já está em `scores_risco`, gravada pela própria
varredura. Uma tabela de "última execução" duplicaria esse fato e criaria a chance de os dois
divergirem — e o que vale é o que a varredura fez, não o que um registro paralelo diz que ela
fez.

### 3.4 O formato de produção é o comando, não o serviço

`dotnet run -- ciclo-mensal` sobe, trabalha e morre. É o que um agendador externo dispara —
Cloud Scheduler chamando um Cloud Run Job. Um serviço de pé esperando um mês passar contradiz
o *scale to zero* em que o custo do projeto se apoia.

O `BackgroundService` continua existindo para uma máquina que já está ligada, e ele **pergunta
ao banco** de seis em seis horas em vez de contar o tempo por conta própria: contar
internamente perderia a conta a cada reinício do contêiner, e no Cloud Run reiniciar é rotina.

A ordem é coleta → varredura → rede → consolidação, e não é arbitrária: a coleta traz despesa
nova, a varredura precisa dela, a passada de rede só faz sentido depois que todos foram
varridos, e a consolidação soma o que existe no fim. A poda **não** entra — apagar gasto
público tem de ser ato explícito, nunca efeito colateral de rotina.

Coleta incompleta não passa em silêncio: o resultado carrega `ColetaIncompleta` e o comando
sai com código 2. A varredura roda mesmo assim, porque produz análise válida sobre o que está
no banco — mas o dado ficou mais velho do que o resumo sugere, e foi esse tipo de sucesso
enganoso que produziu o [incidente de 2026-08-21](incidente-2026-08-21-despesas-vazia.md).

### 3.5 Verificado contra os 513

```
=== CICLO MENSAL ===
Nada a fazer: a base inteira foi varrida ha menos de 30 dias.
```

Saída correta, código 0. A base foi varrida em 21/08 e o ciclo não vence.

---

## 4. Observabilidade — ✅ concluída

OpenTelemetry nos dois hosts, como o documento de Arquitetura §1.3 pede: instrumentação de
ASP.NET, `HttpClient`, Npgsql e runtime; rastros e métricas.

**A sonda `/saude` fica fora do rastro.** Ela dispara sem parar e não diz nada sobre ninguém —
rastreá-la afogaria a requisição de verdade no ruído e, num backend cobrado por span, seria
pagar para guardar batimento cardíaco. (Nas *métricas* ela continua, porque ali é um agregado
por rota e custa uma série.)

**Sem `OTEL_EXPORTER_OTLP_ENDPOINT`, nenhum exportador é registrado.** Um OTLP apontando para o
vazio tentaria entregar, falharia e repetiria para sempre, gastando CPU num contêiner cobrado
por tempo sem entregar a ninguém.

**A amostragem é configuração, não constante.** Cem por cento é o certo no começo — tráfego
baixo, cada rastro conta. Num pico deixa de ser, e é justamente quando não dá para reagir: o
número fica em `Observabilidade:Amostragem` para poder baixar sem redeploy.

**O que foi conferido antes de ligar a instrumentação de saída:** nenhuma URL externa carrega
dado pessoal. A da Câmara leva o id público do deputado; a da BrasilAPI leva CNPJ, que é dado
de pessoa jurídica. O CPF do parlamentar nunca entrou no sistema (Etapa 01) e o do sócio chega
mascarado da fonte.

### 4.1 Medido, e não presumido

Um exportador de console (`Observabilidade:Console=true`) existe para **ver** o rastro em
desenvolvimento — "configurei e presumo que funciona" não vale aqui. Contra a base real:

```
Activity.DisplayName: GET /api/v1/parlamentares   Kind: Server   http.route: /api/v1/parlamentares
Activity.DisplayName: postgresql                  Kind: Client   db.system.name: postgresql
```

O span de servidor sai com o **template da rota**, não com o caminho cru — o que impede a
cardinalidade explodir com 513 ids distintos. E `GET /saude` produziu **zero** spans de
servidor.

### 4.2 Duas armadilhas

**`AddNpgsql` resolve para o método errado.** Sem qualificar, o compilador escolhe a extensão
do EF Core sobre `IServiceCollection` — que pede string de conexão — em vez da do
`Npgsql.OpenTelemetry` sobre `TracerProviderBuilder`. Nomes iguais em assemblies diferentes.

**Faixa de `IdExterno` já ocupada derruba a suíte só quando ela roda inteira.** O teste novo
escolheu 900 mil, depois 800 mil, e as duas estavam em uso — a segunda em
`Integracoes/IngestaoPontaAPontaTests`, numa subpasta que a primeira busca não cobriu.
Isolados os testes passavam; juntos, `23505`. É o mesmo tropeço da Etapa 04 §5.6, e a forma de
não repeti-lo é conferir com `grep -rn "_sequencia) + " tests/Explanando.Integration.Tests/`,
**recursivo**.

---

## 5. O que falta nesta etapa

| Item | Situação |
|---|---|
| **Política de Privacidade publicada** | ⬜ Não é código. O rodapé já nomeia o Encarregado; falta a política, e a base legal citada no ADR-0010 §2 está trocada (art. 7º, VI é exercício de direitos em processo; o provável é legítimo interesse, art. 7º, IX, que exige avaliação documentada) |
| **Dockerfile e CI de deploy** | ⬜ Nada empacota a API nem o worker hoje |
| **Neon, Cloud Run, Cloudflare** | ⬜ Precisa de contas e segredos. No desdobramento, `RateLimit:AtrasDeProxy` vira `true`, `RateLimit:ChaveInterna` precisa existir dos dois lados, e `OTEL_EXPORTER_OTLP_ENDPOINT` aponta para o coletor |
| **Agendar o `ciclo-mensal`** | ⬜ O comando existe e está verificado; falta o Cloud Scheduler que o dispara |
