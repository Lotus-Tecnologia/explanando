# Estado atual do projeto — handoff

**Gerado em:** 2026-09-19 · **Branch:** `main`

Este documento existe para que outra sessão retome sem reler o histórico. O que está aqui foi
**verificado contra o banco e o código**, não escrito de memória.

---

## 1. Onde estamos

O **Explanando** audita gastos da cota parlamentar (CEAP/CEAPS) cruzando dados públicos para
apontar inconsistências financeiras.

**As etapas 00 a 06 e 08 a 12 estão concluídas. A 07 (observabilidade e deploy) está quase
fechada.**

> **O marco mais recente: o grafo do Estado.** A adaptação ao Brasil do
> [CivLab](https://graph.civlab.org/us) — **96.658 unidades** da administração pública federal,
> com hierarquia, base legal e rastreador de mudanças estruturais. Roda hoje **contra o Postgres
> local**, em 22 MB. Duas coletas independentes da fonte ao vivo produziram zero diferenças, o
> que prova a comparação. Ver [Etapa 11](docs/registro/etapa-11-grafo-do-estado.md) e o
> [ADR-0016](docs/decisoes/ADR-0016-grafo-do-estado.md).
>
> **E o grafo já diz quanta gente trabalha em cada órgão.** A
> [Etapa 12](docs/registro/etapa-12-quadro-de-pessoal.md) trouxe 759.351 vínculos, 95,5% deles
> ligados a um nó — **em contagens, sem um nome no banco**. Falta só *as nomeações ao longo do
> tempo* (DOU, via INLABS, que só o Gustavo pode cadastrar) e *as cadeiras vagas*, que nenhuma
> fonte publica.
>
> **E o mapa de poder existe**, dentro de `/estado`: 138 cadeiras de comando — Presidente,
> 38 ministros, Mesa da Câmara, 47 lideranças e 30 presidências de comissão —, de três
> fontes independentes. Clicar na cunha de um poder no grafo abre quem o comanda. O
> Judiciário aparece vazio e explica por quê. Ver
> [Etapa 14](docs/registro/etapa-14-mapa-de-poder.md) e o
> [ADR-0018](docs/decisoes/ADR-0018-cadeiras-de-comando.md).
>
> A página `/rede` foi desativada — o backend dela continua, com o ADR-0017 e os testes, e
> `ObterLigacoesAsync` tem casa natural no perfil de um parlamentar.

> **O marco desta sessão: as duas casas estão na base.** 631 parlamentares — 513 deputados e
> 118 senadores —, 791.515 despesas e 36.527 flags. O que falta é a API subir no Cloud Run e o
> frontend ser reconstruído contra ela.
>
> **O Senado foi calibrado e os rankings separados.** A taxa dele caiu de 19,8% para 5,080%,
> contra 2,957% da Câmara — e em gravidade alta ficou **abaixo** dela. Os dois rankings são
> separados porque as cotas são: a casa é parâmetro obrigatório na API, e o piso amostral do
> ADR-0009 vale dentro dela. Ver
> [Etapa 10](docs/registro/etapa-10-calibragem-e-rankings-por-casa.md).

| Etapa | Situação |
|---|---|
| 00 Fundação · 01 Contratos Câmara · 01b Contratos Senado | ✅ |
| 02 Domínio e EF Core · 03 Ingestão | ✅ |
| 04 Motor · 04b Calibragem · 04c Armazenamento · 04d Coleta completa | ✅ |
| 05 API pública de leitura · 06 Frontend Next.js | ✅ |
| **07 Observabilidade e deploy** | 🚧 **falta a API no Cloud Run e o DNS** |
| 08 Linguagem da interface · 09 Ingestão do Senado · 10 Calibragem e rankings por casa | ✅ |
| **11 O grafo do Estado** | ✅ local; falta subir para a Neon e agendar |
| **12 Quem trabalha no Estado** | ✅ local; falta subir para a Neon e agendar |

**Testes:** 843 no backend (503 domínio, 75 aplicação, 195 integração, 10 arquitetura, 60
ponta a ponta) + **157 no frontend**. CI verde nos quatro jobs.

---

## 2. Regras de negócio ativas

### 2.1 O ADR-0004 é a espinha dorsal — e é executável

O produto **descreve inconsistências, não imputa conduta**. Isso não é diretriz de redação: é
invariante que o domínio recusa violar.

`VocabularioDeCompliance.GarantirVocabularioTecnico` **lança exceção** se a descrição de uma
flag contiver termo acusatório. Os termos proibidos:

```
fraude, fraudulenta, fraudulento, crime, crimes, criminoso, criminosa,
corrupcao, corrupto, corrupta, lavagem, desvio, desviou, desviado,
roubo, roubou, propina, caixa dois
```

Três detalhes que já custaram bug e não podem ser desfeitos:

1. **Comparação por palavra inteira.** "descrição" contém "cri"; "discriminado" contém "crim".
2. **`ExpressoesPermitidas = ["desvio padrao"]`.** "Desvio Padrão" é termo *preferido* que
   contém termo *proibido*.
3. **Trecho entre aspas simples não é examinado.** Três lava-jatos —
   `MOREIRA LAVAGEM E POLIMENTOS`, `ESQUILOS LAVAGEM AUTOMOTIVA`,
   `RODO SERVICE LAVAGEM E CONSERVACAO` — derrubavam `REG_KYB_001`, porque a descrição
   interpola a razão social. **Custava 181 flags legítimas**, já que uma exceção descarta
   todas as flags daquele parlamentar. O filtro limita o que o *produto* afirma; nome de
   empresa que existe no mundo não é escolha de palavra nossa.

### 2.2 Postura permissiva (ADR-0007) — na dúvida, silêncio

Falta de dado **nunca** vira suspeita. `ResultadoDeCompatibilidade` tem três estados
(`Compativel`, `Incompativel`, `SemBaseParaAvaliar`) porque booleano forçaria escolher entre
acusar e inocentar quando a resposta honesta é "não sei".

### 2.3 A distinção que sustenta o produto inteiro

**Zero por falta de dado ≠ zero por nada encontrado.**

Cada regra declara em `IRegraAnomalia.Exige` o dado externo de que depende, e o motor mede
quantas despesas o têm. `CoberturaDoParlamentar.SilencioEhConclusivo` é o campo que a API
expõe e a interface consulta **antes de desenhar** — nunca a contagem de flags.

Na base real:

```
    REG_TX_001  SemAchados  flags=0   ← avaliou e está limpo
 >> REG_TX_002  SemDado     flags=0   ← não teve como avaliar
```

### 2.4 Dois números, nunca um (ADR-0009)

| | Volume (B2C) | Índice (B2B) |
|---|---|---|
| O quê | Soma em reais, estornos abatidos | Indicadores por 100 despesas |
| Piso amostral | Nenhum | **100 despesas** |
| Abaixo do piso | Continua no ranking | Sai, e o valor vem **`null`, não zero** |

A API tem **três** ordenações: `volume`, `indice` e `gravidade` — a última ordena só pelos
achados de gravidade alta, porque pelo total quem tem 14 achados graves fica abaixo de quem
tem zero. A escolha ficou com quem consome (questão 5, resolvida).

### 2.5 Assimetria Câmara × Senado (ADR-0012)

- **Glosa desconhecida é `null`, nunca zero.** Zero afirmaria que houve conferência e nada foi
  cortado. A interface omite a quebra de valores quando `GlosaConhecida` é falso.
- **O Senado não publica comprovante.** Tag inativa atribuindo a ausência à fonte, mais nota
  técnica no rodapé do Relatório Cidadão.
- **`REG_NET_004` roda sobre as duas casas juntas** — o Senado tem 82 parlamentares e o piso é
  100.
- **`LimiteCota` tem `Casa`**: CEAP e CEAPS são cotas distintas com valores próprios por UF.

### 2.6 Taxonomia visual (ADR-0013) — a cor é texto

Quatro pesos, cada um com **cor, forma e rótulo**. Contraste calculado, não estimado:

| Nível | % da base | Fundo | Texto | Ícone | Contraste |
|---|---|---|---|---|---|
| Baixa | 43,3% | `#F8FAFC` | `#334155` | `i` círculo | 9,90 AAA |
| Média | 53,4% | `#FEF3C7` | `#78350F` | `!` triângulo | 8,15 AAA |
| Alta | **3,3%** | `#FEE2E2` | `#991B1B` | `×` octógono | 6,80 AA |
| Não avaliado | — | `#F1F5F9` | `#475569` | `–` tracejado | 6,92 AA |

**O vermelho fica estritamente com os 3,3%.** Sem `uppercase` — caixa alta é ênfase, e ênfase
sobre achado descritivo é imputação pela tipografia. Botão de ação usa `#064E3B` (9,72); o
verde `#059669` reprova para texto (3,77) e serve só para elemento não textual.

> **Regra permanente:** toda cor nova passa pelo cálculo de contraste antes de entrar. Seis
> valores reprovaram na revisão, **três deles propostos depois de o problema já ter sido
> apontado nos outros três**.

### 2.7 As 12 regras

`REG_KYB_001` a `005`, `REG_TX_001` a `004`, `REG_GEO_002`, `REG_GEO_003` (motor por
parlamentar) e `REG_NET_004` (passada de rede, cruza parlamentares).

**Sem fonte de dado, não implementadas:** `REG_NET_001` (precisa do TSE), `REG_NET_002` (405
em quatro rotas), `REG_NET_003` (só o Senado tem endereço de escritório), `REG_GEO_001` (a
despesa não traz local nem hora).

---

## 3. Infraestrutura e banco

### 3.1 Stack

- **.NET 10** (SDK 10.0.302), Clean Architecture verificada por teste (ADR-0006)
- **PostgreSQL 17** + EF Core 10 + Npgsql, snake_case
- **Next.js 16 / React 19 / Tailwind v4** — Tailwind v4 configura por **CSS**, não pelo
  `tailwind.config.js` que o documento de identidade descrevia
- **Frontend no Cloudflare Workers** via `@opennextjs/cloudflare`, com o ISR em R2
  ([ADR-0015](docs/decisoes/ADR-0015-frontend-no-cloudflare.md)) — não na Vercel
- **HybridCache** (memória; ganha Redis via `EXPLANANDO_REDIS`)
- Repositório **privado** (ADR-0008)

### 3.2 Docker

`docker-compose.yml` na raiz sobe `explanando-postgres`, `explanando-redis` e
`explanando-localstack`. **Os três precisam estar no ar** para os testes de integração.

### 3.3 Banco — **PRODUÇÃO NA NEON, verificado em 2026-08-25**

O banco local do Docker continua existindo para desenvolvimento e testes. **A base de verdade
agora é a Neon**, e ela está completa:

| Tabela | Linhas |
|---|---|
| `parlamentares` | **631** (513 deputados + 118 senadores) |
| `despesas` | **791.515** |
| `fornecedores` | **40.860** |
| `flags_anomalias` | **25.047** |
| `consolidados_mensais` | **23.655** |
| `checkpoints_ingestao` | **631**, e **0 com alerta** |
| **Tamanho** | **300 MB** de 512 (41% de folga) |

Volume total: **R$ 924.487.309,77**. A **Câmara** sinaliza **2,957%** — contra 2,96% da base
anterior, o que revalida os limiares do `ParametrosDoMotor`. O **Senado sinaliza 5,080%**
depois da calibragem da Etapa 10, e em gravidade alta fica abaixo da Câmara (0,061% contra
0,097%). O motor está na **versão 4**.

**A string de conexão vive em `backend/.env.neon`** (ignorado pelo git). Ela está no formato
chave-valor do Npgsql; a Neon entrega URI, e `backend/converter-conexao.py` traduz sem
imprimir a senha.

> ⚠️ **A API não roda migrations na subida** — de propósito. Quem cria esquema é o worker, em
> qualquer comando. Publicar a API contra banco vazio dá HTTP 500.

**Migration mais recente:** `20260821193814_CoberturaPersistida`. As 12 estão aplicadas na Neon.


### 3.4 Comandos

```bash
# Contra a Neon (o padrão agora). A partir de backend/:
EXPLANANDO_CONEXAO="$(tr -d '
' < .env.neon)" dotnet run --project src/Explanando.Worker -- <comando>

#   coletar --legislatura 57   ~8 h na primeira vez; retomável por checkpoint
#   varrer                     motor + passada de rede
#   consolidar [--meses N]     sem --meses não apaga nada
#   ciclo-mensal [--forcar]    coleta + varre + rede + consolida (ADR-0010 §4)

# API contra a Neon (sobe na 5000 sem ASPNETCORE_HTTP_PORTS)
EXPLANANDO_CONEXAO="$(tr -d '
' < .env.neon)" dotnet run --project src/Explanando.Api

# Consultar a Neon direto, sem psql instalado — via Docker
# (converter-conexao.py monta a URI; ver §4)

# frontend (a partir de frontend/)
npm test          # 110 testes
npm run build     # build normal do Next
npm run cf:build  # bundle do Cloudflare — NÃO roda no Windows
```


### 3.5 Armadilhas do ambiente que voltam a morder

1. **Heredoc do bash come barras invertidas.** `\d` vira `d` em `GeneratedRegex`. Use a
   ferramenta Write para conteúdo com barra.
2. **`InvariantGlobalization=true`** faz `String.Normalize(FormD)` virar no-op silencioso.
3. **Value converter bloqueia LINQ.** `Competencia` vira `int` AAAAMM; compare o objeto inteiro.
4. **`ArtifactsPath` fora do OneDrive** — Smart App Control bloqueia DLL não assinada.
5. **`byte[]` compara por referência** em C#. `HashUnico` precisa de `ComparadorDeHash`.
6. **Medir tamanho de tabela exige `VACUUM FULL`.**
7. **Processo `.NET` rodando trava o build.** A API ou o worker de pé seguram as DLLs e o
   `dotnet build` morre em `MSB3027`. Matar o processo antes — inclusive quem escuta a porta,
   porque `TaskStop` nem sempre derruba o filho do `dotnet run`.
8. **`.ps1` sem BOM é lido como ANSI pelo PowerShell 5.1.** Os acentos corrompem o parse e o
   erro acusa `'}' de fechamento ausente` numa linha correta — falha longe da causa.
9. **A string da Neon vem em URI; o Npgsql quer chave-valor.** Ver `converter-conexao.py`.
10. **O adaptador do Cloudflare não roda no Windows.** A primeira execução completa; as
    seguintes penduram em toda rota. O build tem de sair de Linux — CI ou WSL.
11. **`export const revalidate` não aceita constante importada** no Next: derruba o build com
    *"Invalid segment configuration export"*, sem apontar o arquivo.
12. **Faixa de `IdExterno` repetida derruba a suíte só quando ela roda inteira.** Confira com
    `grep -rn "_sequencia) + " tests/Explanando.Integration.Tests/`, **recursivo**.


## 4. Onde paramos exatamente

### 4.1 O que já está no ar

| | Onde | Estado |
|---|---|---|
| **Banco** | Neon (`ep-spring-mode-...sa-east-1`) | ✅ duas casas, 631/791.515/25.047 |
| **Frontend** | `https://explanando.gustavo-criado.workers.dev` | ⚠️ publicado, mas **degradado** |
| **Repositório** | `Lotus-Tecnologia/explanando`, privado | ✅ |
| **Domínio** | `explanando.com.br` | comprado, **ainda não aponta para nada** |
| **API** | — | ❌ **só local** |

**O frontend está degradado de propósito e isso é o comportamento correto.** Como
`api.explanando.com.br` não existe, o build caiu no caminho medido: **9 páginas em vez de 522,
sitemap com 4 endereços, home dizendo "o ranking não pôde ser carregado"**. O código preferiu
degradar a quebrar.

**A indexação está trancada** (`NEXT_PUBLIC_INDEXAVEL` ausente ⇒ `Disallow: /` + `noindex`),
para o Google não conhecer o site nesse estado. Indexação é o canal de aquisição principal, e
primeira impressão de índice é cara de refazer.

### 4.2 Os próximos passos, na ordem

1. **Publicar a API no Cloud Run.** A imagem já existe e foi testada contra a Neon
   (`docker build -f backend/Dockerfile --target api`). Variáveis em
   [docs/desdobramento.md](docs/desdobramento.md) §2 — `EXPLANANDO_CONEXAO` com o endpoint
   **pooled**, `RateLimit__AtrasDeProxy=true`, `RateLimit__ChaveInterna`.
2. **`api.explanando.com.br` → Cloud Run**, Cloudflare **proxied** (nuvem laranja). Só com o
   proxy de fato na frente é que `AtrasDeProxy=true` é seguro.
3. **`explanando.com.br` e `www` → o Worker** do frontend.
4. **`wrangler secret put EXPLANANDO_CHAVE_INTERNA`** no Worker — ele precisa da chave em
   *execução*, não só no build.
5. **Rodar o workflow "Publicar frontend"** → sitemap vira 517, home mostra os 513.
6. **Abrir o site e olhar.** Só então `NEXT_PUBLIC_INDEXAVEL=true` (variável de repositório) e
   mais um deploy.
7. **Cloud Scheduler** disparando `ciclo-mensal`, `0 3 1 * *`, com timeout ≥ 14 h.

O deploy do frontend é **manual** por decisão (`workflow_dispatch`): o build busca a API, e um
deploy disparado com ela fora publica o site degradado sem ninguém olhando.

### 4.3 Segredos e arquivos locais que não estão no git

| | |
|---|---|
| `backend/.env.neon` | String da Neon, formato Npgsql. **A mesma chave interna precisa ir para o Cloud Run** |
| `frontend/.env.local` | As três variáveis do frontend, com `EXPLANANDO_CHAVE_INTERNA` gerada |
| `%LOCALAPPDATA%\explanando\` | Logs de coleta, varredura e supervisor |

No GitHub já estão configurados: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID`,
`EXPLANANDO_CHAVE_INTERNA` (secrets) e `NEXT_PUBLIC_SITE_URL`, `NEXT_PUBLIC_API` (variables).

### 4.4 O que esta sessão descobriu, e vale não reaprender

**A fonte da Câmara degradou de novo** — a primeira coleta parou em 1m46s com 61 respostas
`200` todas vazias. O freio dos 10 alertas seguidos, criado depois do incidente de 2026-08-21,
fez o trabalho. Minutos depois a mesma URL devolvia 12/12 com dado. **Investigue antes de
reexecutar**: bater 12 vezes na URL do coletor separa "a fonte caiu" de "quebramos algo".

**Queda de energia derrubou a coleta em 291/513**, e **nada se perdeu**: os checkpoints fizeram
a retomada pular os já feitos. Foi o `0 com alerta` que garantiu que o parcial era confiável.

**A coleta quente é 2× mais rápida que a fria** — 0,89 min/deputado contra 1,58 —, porque CNPJ
já conhecido não é reconsultado. Não extrapole tempo de coleta fria para retomada.

**O supervisor tem um defeito conhecido:** ele lê o log no instante em que o processo sai, antes
de o buffer ser descarregado, e pode julgar "incompleta" uma coleta que terminou. Errou para o
lado seguro (relançou, custou 28 s, confirmou a conclusão), mas a correção é esperar um pouco
depois do `WaitForExit`.

### 4.5 Pendências

| Pendência | Impacto | De quem é |
|---|---|---|
| **`REG_TX_001` no Senado** | Médio | **Sua** — 25× a taxa da Câmara, 946 das 1.467 em passagens. Não é calibragem: a regra agrupa por fornecedor e mês e descreve corretamente o que vê. Várias notas da mesma agência no mês é padrão real de contratação. Se vale mostrar é decisão de produto |
| **Limites da CEAP 2023–2025** | **Alto** | **Sua** — Atos da Mesa, não vem em API. `REG_TX_002` responde `SemDado` nas 791 mil despesas |
| **Limites da CEAPS por UF** | Alto para o Senado | **Sua** — idem |
| **Avaliação de legítimo interesse documentada** | Alto | **Sua** — a ANPD pode solicitá-la (art. 7º, IX) |
| **Revisão jurídica da política publicada** | Médio | **Sua** |
| Revisar a tabela CNAE × tipo do Senado | Médio | **Sua** — pedida desde a Etapa 01b |
| Índice de risco × opacidade da fonte | Médio | **Sua** — decisão em aberto no [ADR-0014](docs/decisoes/ADR-0014-lacuna-camada-2-e-base-legal.md) |
| Acrescentar a casa à chave de `checkpoints_ingestao` | Baixo | Minha — hoje protegido por guarda no comando, não por esquema |
| Escritórios de apoio do Senado (`REG_NET_003`) | Médio | Minha — a fonte existe e não foi ingerida |
| Card viral com disclaimer na imagem | Médio | Minha — client-side, não depende de backend |
| Catalogar os 4 tipos de despesa | Baixo | Minha — grafia em mãos (Etapa 04c §3.5) |
| 62 despesas rejeitadas por glosa inconsistente | Baixo | Minha — investigar origem × rigidez |
| Corrigir a corrida do supervisor | Baixo | Minha |
| Favoritar, contestar, pagar, Camada 2, API B2B | — | Bloqueados em contas (Mercado Pago, Gemini, Resend) |

**Sem fonte, e não por escolha:** `REG_NET_001` (TSE), `REG_NET_002` (405 em quatro rotas),
`REG_NET_003` (só o Senado tem endereço), `REG_GEO_001` (a despesa não traz local nem hora).


## 5. Como o trabalho vem sendo feito

Cinco combinados que valem manter:

1. **Planejar antes de executar, e pedir autorização.** Instrução permanente do Gustavo.
2. **TDD obrigatório** (ADR-0005). Teste antes do código, nos quatro níveis.
3. **Registro por etapa** em `docs/registro/`. É o contexto que sobrevive entre sessões.
4. **Medir, não estimar.** Praticamente todo defeito sério desta base foi achado rodando
   contra dado real — a coleta expôs quatro que nenhum teste com dado inventado acharia.
5. **Comentário explica o *porquê*, não o *o quê*.** O código é denso em justificativa
   histórica: por que a glosa é nula, por que o vermelho é só dos 3,3%, por que o `last`
   ausente é suspeito. Isso é intencional e deve continuar.

### O que está pausado

Em 18/09/2026 o foco passou para o **grafo do Estado** (adaptação do CivLab ao Brasil). O
que ficou esperando — o deploy, a página de perfil, as decisões de curadoria e os itens
pequenos — está em [docs/pendencias.md](docs/pendencias.md), com o porquê de cada um.

### Para desdobrar

O guia operacional — contas, nomes exatos de variável, onde cada segredo vai e a ordem que
importa — está em [docs/desdobramento.md](docs/desdobramento.md).

### Documentos de referência

- **15 ADRs** em `docs/decisoes/` — quando um ADR contradiz `docs/especificacao/`, **o ADR
  vence**
- **13 registros** em `docs/registro/`, incluindo o
  [incidente de 2026-08-21](docs/registro/incidente-2026-08-21-despesas-vazia.md)
- **Contratos verificados** em `docs/contratos/` — Câmara, Senado e BrasilAPI, com fixtures
  escolhidas *pelas armadilhas*, não pelo caminho feliz
