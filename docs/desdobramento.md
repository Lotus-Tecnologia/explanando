# Desdobramento — a VPS, do zero ao ar

Roteiro operacional do [ADR-0019](decisoes/ADR-0019-tudo-numa-vps.md): Postgres, API, worker e
frontend numa VPS ARM da Oracle, com a Cloudflare na frente como CDN. **Tudo aqui foi testado
em 2026-09-25**, com o compose de produção rodando nesta máquina contra a base completa (631
parlamentares, 790 mil despesas) — os números citados são medidos, não estimados.

> **A versão anterior deste documento** descrevia Cloud Run + Neon + Cloudflare Workers. Está no
> histórico do git; o ADR-0019 explica por que saiu.

> **Ordem curta:** VPS → Docker → código e `.env` → carga da base → `./atualizar.sh` → túnel da
> Cloudflare → R2 e backup → timers → olhar o site → indexar.

---

## 1. O que está no repositório

| | |
|---|---|
| [`deploy/compose.yml`](../deploy/compose.yml) | A produção: `postgres`, `migracoes`, `api`, `web`, `cloudflared`, e as tarefas `worker` e `backup` |
| [`deploy/exemplo.env`](../deploy/exemplo.env) | As variáveis, comentadas. Vira `deploy/.env` na VPS |
| [`deploy/atualizar.sh`](../deploy/atualizar.sh) | Puxa, constrói e sobe **na ordem certa** (§5) |
| [`deploy/backup/`](../deploy/backup/) | `backup.sh`, `conferir.sh` (teste de restauração) e `restaurar.sh` (desastre) |
| [`deploy/systemd/`](../deploy/systemd/) | Os timers: ciclo, cadeiras, Estado, backup e conferência |
| [`deploy/compose.local.yml`](../deploy/compose.local.yml) | O mesmo compose rodando numa máquina de desenvolvimento, com um S3 local no lugar do R2 |

**Nenhuma porta fica aberta para a internet.** O tráfego entra pelo **Cloudflare Tunnel** — uma
conexão de *saída* da VPS até a Cloudflare. Três consequências:

- não há 80/443 a abrir na Security List da Oracle nem no iptables da imagem Ubuntu (a
  armadilha do handoff §4 some);
- o IP da máquina não aparece em DNS, e ninguém contorna a CDN batendo direto nele;
- `RateLimit__AtrasDeProxy=true` passa a ser seguro de verdade: todo pedido que chega à API
  passou pela Cloudflare, que sobrescreve `CF-Connecting-IP`.

---

## 2. As contas (todas do Gustavo)

| Serviço | Para quê | Custo |
|---|---|---|
| **Oracle Cloud** | A VPS (Ampere A1, Always Free) | Zero dentro do Always Free |
| **Registro.br** | `explanando.com.br` — os nameservers passam a ser os da Cloudflare | ~R$ 40/ano |
| **Cloudflare** | DNS, CDN, WAF, **Tunnel** e **R2** (backup) | Zero no plano gratuito; R2 grátis até 10 GB |
| **healthchecks.io** (opcional, recomendado) | Aviso quando o backup ou a conferência param | Zero no plano gratuito |

Resend, Mercado Pago e INLABS entram com os blocos que dependem deles (Fase 8 e além).

---

## 3. A máquina

### 3.1 Criar a instância

- **Shape:** `VM.Standard.A1.Flex`, **2 OCPUs e 12 GB** (o teto do Always Free). Se a Oracle só
  tiver capacidade para **1 OCPU e 6 GB**, crie assim e cresça depois — o compose roda nos dois
  tamanhos; só três variáveis do Postgres mudam (ver `exemplo.env`).
- **Imagem:** Ubuntu 24.04 (aarch64).
- **Disco de boot:** 100 GB (o Always Free dá 200 GB de block volume no total).
- **Rede:** IP público efêmero basta — ele só serve para o SSH. Não é preciso reservar IP nem
  abrir 80/443. Na Security List, deixe só a 22, de preferência restrita ao seu IP.
- **Chave SSH:** a que já existe em `$HOME\.ssh\oracle_explanando` — em "Add SSH keys",
  **"Upload public key files"** com o `.pub` (impressão digital `SHA256:z+6ODvK4…`). A chave
  que o assistente gera sozinho não é a que está nesta máquina.

> **A Micro (`VM.Standard.E2.1.Micro`) não serve para a produção.** É 1/8 de OCPU e 1 GB,
> x86: a pilha ociosa já usa ~500 MB, o build do Next não roda em 1 GB, e a varredura mensal
> se arrastaria. Ela pode ficar (o Always Free permite as duas Micro junto com a A1), mas o
> site vai na A1.
>
> **"Out of capacity for shape VM.Standard.A1.Flex"** é comum em São Paulo. Tentar de madrugada
> funciona com frequência; converter a conta para Pay As You Go também costuma destravar — ver
> §9 sobre o que mais essa conversão resolve.

### 3.2 Preparar o sistema

```bash
ssh -i ~/.ssh/oracle_explanando ubuntu@<ip>

sudo timedatectl set-timezone America/Sao_Paulo

# 2 GB de swap: a imagem da Oracle vem sem nenhum, e o build do Next é o momento de pico.
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

# Docker, pelo repositório oficial (a versão do Ubuntu é antiga para o compose deste projeto)
sudo apt-get update && sudo apt-get install -y ca-certificates curl git
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] \
  https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt-get update && sudo apt-get install -y docker-ce docker-ce-cli containerd.io \
  docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker ubuntu   # sair e entrar de novo no SSH
```

O Ubuntu da Oracle já vem com `unattended-upgrades` ligado — patch de segurança do sistema se
aplica sozinho.

### 3.3 O código

O repositório é privado (ADR-0008). Na VPS, uma **deploy key só de leitura**:

```bash
ssh-keygen -t ed25519 -f ~/.ssh/explanando_deploy -N ""
cat ~/.ssh/explanando_deploy.pub   # GitHub → repositório → Settings → Deploy keys → Add (read-only)

sudo mkdir -p /opt/explanando && sudo chown ubuntu /opt/explanando
GIT_SSH_COMMAND="ssh -i ~/.ssh/explanando_deploy" \
  git clone git@github.com:Lotus-Tecnologia/explanando.git /opt/explanando
git -C /opt/explanando config core.sshCommand "ssh -i ~/.ssh/explanando_deploy"
```

### 3.4 O `.env`

```bash
cd /opt/explanando/deploy
cp exemplo.env .env && chmod 600 .env
nano .env
```

`POSTGRES_PASSWORD` e `CHAVE_INTERNA` se geram com `openssl rand -hex 24` e `openssl rand -hex 32`.
O token do túnel e as chaves do R2 vêm dos passos 6 e 7 — dá para subir sem eles e preencher
depois.

---

## 4. A carga da base

A base local tem tudo (Câmara, Senado, TSE, grafo do Estado, cadeiras). Coletar do zero na VPS
leva **~13 horas** (quase tudo é espera pelo limitador da BrasilAPI) e ainda exigiria os
arquivos do TSE, que só se baixam por navegador. O caminho é levar um dump:

```bash
# Nesta máquina (Windows, Git Bash):
docker exec explanando-postgres pg_dump -U explanando -Fc explanando > explanando.dump   # ~75 MB
scp -i ~/.ssh/oracle_explanando explanando.dump ubuntu@<ip>:/tmp/

# Na VPS:
cd /opt/explanando/deploy
docker compose up -d postgres
docker compose exec -T postgres pg_restore -U explanando -d explanando \
  --no-owner --no-privileges --exit-on-error < /tmp/explanando.dump
rm /tmp/explanando.dump
```

Medido: o dump tem **72–75 MB** e restaura em **5 segundos**. As migrations que o dump ainda não
tiver são aplicadas pelo serviço `migracoes` no passo seguinte.

---

## 5. Subir: `./atualizar.sh`

```bash
cd /opt/explanando/deploy && ./atualizar.sh
```

O script faz, nesta ordem, e **a ordem não é estética**:

1. `git pull`;
2. constrói as imagens do backend;
3. sobe `postgres` → `migracoes` (o worker com o comando `migrar`, que aplica o esquema e sai) →
   `api`. **A API não aplica migrations de propósito**, e contra esquema velho dá HTTP 500;
4. espera a API responder em `127.0.0.1:8080/saude`;
5. constrói o frontend — **o build pré-renderiza ~980 páginas buscando a API**, por isso ele só
   roda com a API de pé, e com a rede do host (`network: host`);
6. sobe o resto (`web` e, com `COMPOSE_PROFILES=publico`, o `cloudflared`).

Se o build do frontend quebrar, o site que está no ar continua no ar: a imagem nova só substitui
a antiga depois de pronta.

**Três armadilhas que o teste local pegou, e que o script já resolve:**

- **O segredo de build lê o ambiente do processo, não o `.env`.** O compose usa o `.env` para
  interpolar o YAML, mas o buildx lê `secrets.chave_interna` do ambiente. Sem exportar, o build
  sai sem a chave interna, cai no teto público de 120/min e quebra com 429 na página 245 de 982.
  O script faz `set -a; . ./.env`.
- **O build pede mais do que o balde interno da API** (2.400/min) em menos de 20 segundos. O
  cliente do frontend agora espera o `Retry-After` e tenta de novo (`lib/api.ts`); o build leva
  ~2 min em vez de quebrar.
- **Chave interna ausente** faz o build passar *verde* com a home dizendo que o ranking não
  carregou. Confira o número de páginas no fim do build: **982**.

**Arquitetura:** as imagens são construídas na própria VPS, então saem arm64 nativas. O
`backend/Dockerfile` também compila cruzado (`--platform=$BUILDPLATFORM` + `-a $TARGETARCH`), e
o CI constrói API e worker para `linux/arm64` a cada push — o "exec format error" aparece lá,
e não na primeira subida.

---

## 6. A Cloudflare na frente

### 6.1 O túnel

Zero Trust → Networks → **Tunnels** → Create a tunnel → **Cloudflared** → copie o **token** do
comando de instalação para `CLOUDFLARE_TUNNEL_TOKEN` no `.env`. Em **Public Hostnames**:

| Hostname | Serviço |
|---|---|
| `explanando.com.br` | `http://web:3000` |
| `www.explanando.com.br` | `http://web:3000` (ou uma regra de redirecionamento para o apex) |
| `api.explanando.com.br` | `http://api:8080` |

`web` e `api` são os nomes dos serviços na rede do compose — o `cloudflared` roda dentro dela.
Depois: `docker compose up -d cloudflared`.

### 6.2 Configurações da zona

| | |
|---|---|
| SSL/TLS | **Full** (o túnel já é criptografado; não há certificado de origem a gerenciar) |
| HSTS | Ligar em SSL/TLS → Edge Certificates, depois de o site estar estável em HTTPS |
| Cache | As páginas saem do Next com `s-maxage` do ISR; os estáticos (`/_next/static`) com `immutable` |
| **Rate limiting** | **Uma regra** (o plano gratuito dá uma): `/comparar/*` e `/parlamentares/*/despesas*` — ver abaixo |

**Por que a regra de rate limiting importa.** Toda página que ainda não está em cache faz o
frontend chamar a API **com a chave interna** — e a chave interna tem um balde só. As rotas de
espaço aberto (o comparador tem 138 mil pares; a lista de despesas aceita filtros e páginas
arbitrários) deixam um raspador gastar esse balde passando pelo site, e aí o próprio site passa
a esperar. O limite por IP na borda é o que fecha isso (achado #14 da auditoria de 2026-09-25).

---

## 7. O backup

### 7.1 O balde

R2 → Create bucket `explanando-backup` → R2 → **Manage API Tokens** → token com **Object Read &
Write só neste balde**. No `.env`: `BACKUP_ENDPOINT` (`https://<ID-DA-CONTA>.r2.cloudflarestorage.com`),
`BACKUP_CHAVE_ID`, `BACKUP_CHAVE_SECRETA`.

### 7.2 O que roda

| | O que faz | Medido |
|---|---|---|
| `backup.sh` | `pg_dump` + **contagem de linhas de cada tabela no mesmo instante** (a mesma fotografia da transação) + sha256 → `diarios/`; no dia 1, também `mensais/`. Retenção: 15 dias e 400 dias | 72 MB, 11 s |
| `conferir.sh` | Baixa o mais recente **do R2**, confere o sha256, restaura num banco à parte e exige as **mesmas linhas, tabela a tabela**; falha se o backup mais novo tiver mais de 36 h | 1.271.567 linhas em 25 tabelas, idênticas, 8 s |
| `restaurar.sh` | O dia do desastre: traz um dump do R2 para o banco de produção. Duas travas: `RESTAURAR_CONFIRMO=sim` e recusa banco com tabelas sem `RESTAURAR_SOBRE_DADOS=sim` | — |

Primeira execução, à mão:

```bash
docker compose run --rm backup backup.sh
docker compose run --rm backup conferir.sh
```

Para ser avisado quando uma rotina parar, crie dois checks no healthchecks.io (diário e
semanal) e ponha as URLs em `BACKUP_AVISO_URL` e `CONFERENCIA_AVISO_URL`. **Backup que falha em
silêncio é descoberto no dia em que se precisa dele.**

---

## 8. Os timers

```bash
sudo cp /opt/explanando/deploy/systemd/*.service /opt/explanando/deploy/systemd/*.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now explanando-backup.timer explanando-conferencia.timer \
  explanando-ciclo.timer explanando-cadeiras.timer explanando-estado.timer
systemctl list-timers 'explanando-*'
```

| Timer | Quando | O quê |
|---|---|---|
| `ciclo` | todo dia, 02h | `ciclo-mensal` — **coleta as duas casas**, varre, cruza a rede, consolida. Só roda quando vence (30 dias desde a varredura mais antiga); nos outros dias sai em segundos |
| `cadeiras` | todo dia, 06h | `coletar-cadeiras --se-vencido` — só relê a fonte parada há mais de uma semana |
| `estado` | dia 5, 03h | `coletar-estado` — o grafo do SIORG (~9 min) |
| `backup` | todo dia, 04h30 | `backup.sh` |
| `conferencia` | domingo, 05h30 | `conferir.sh` |

> **O ciclo coletava só a Câmara.** A coleta injetada era a dela; a do Senado só existia dentro
> do comando `coletar-senado`. Corrigido ao empacotar (`ColetasDoCiclo`); com o agendador
> disparando só o ciclo, a despesa nova dos senadores nunca entraria sozinha.

O `coletar-pessoal` fica manual: a fonte bloqueia por volume, e o vão 2025-11 a 2026-06 se fecha
com `--meses 2` de cada vez, com horas de intervalo (pendências §5).

Logs de qualquer rotina: `journalctl -u explanando-ciclo -e`.

---

## 9. A ociosidade da Oracle — **decisão aberta**

A Oracle recupera instância Always Free que passe **7 dias** com CPU (percentil 95), rede **e**
memória abaixo de 20% — a de memória só nas A1. O ADR-0019 §5 apostava que um `shared_buffers`
generoso manteria a memória acima de 20%. **Medido no teste local, não mantém:** o Postgres só
ocupa a memória das páginas que de fato leu, e a pilha inteira ociosa usou **~500 MB** (Postgres
69 MB, API 320 MB, Next 93 MB) — uns **4% de 12 GB**. CPU e rede de um site com pouco acesso
também ficam abaixo de 20%.

Ou seja: sem tráfego, a máquina se qualifica como ociosa. O backup diário fora da máquina (§7)
garante que isso não perde dado — mas perder a VPS ainda é horas de reinstalação. A saída
indicada é converter a conta para **Pay As You Go**: a documentação oficial diz que a regra vale
para instâncias Always Free, e o relato corrente é que contas PAYG não são recuperadas (sem
cobrança enquanto o uso fica dentro do Always Free). Não está escrito com essas palavras na
documentação da Oracle; vale confirmar no suporte ao converter.

---

## 10. Antes de indexar

1. Abrir o site no ar: home, um ranking de cada casa, três perfis (um deputado, um senador, um
   sem amostra), uma despesa, o comparador, o mapa do Estado.
2. `curl -s https://explanando.com.br/sitemap.xml | grep -c '<loc>'` → **976**.
3. `curl -sI https://explanando.com.br | grep -i cf-cache-status` → `HIT` na segunda chamada.
4. Só então `INDEXAVEL=true` no `.env` e `./atualizar.sh` (a trava é lida no build).
5. Google Search Console com o sitemap — é o canal de aquisição principal do Business Model Canvas.

---

## 11. Operação

| | |
|---|---|
| Atualizar | `cd /opt/explanando/deploy && ./atualizar.sh` |
| Ver o que está de pé | `docker compose ps` |
| Logs | `docker compose logs -f api` (o Docker gira os arquivos: 3 × 10 MB por serviço) |
| psql | `docker compose exec postgres psql -U explanando` (a porta não é publicada, nem em localhost) |
| Um comando do worker | `docker compose run --rm worker <comando>` |
| Testar tudo antes, na sua máquina | `docker compose -p explanando-teste -f compose.yml -f compose.local.yml --env-file <arquivo> ...` — **o `-p` não é opcional**: o compose de desenvolvimento também se chama `explanando` e tem um volume `postgres-dados` |
| Atualizar o Postgres de versão maior | dump → trocar a imagem → volume novo → restore. Nunca trocar a imagem sobre o volume antigo |

---

## 12. O que continua faltando, e não é conta

- **Avaliação de legítimo interesse documentada** — a ANPD pode solicitá-la (art. 7º, IX).
- **Revisão jurídica** da política publicada.
- **Limites da CEAP 2023–2025 e da CEAPS** — curadoria humana dos Atos da Mesa; até lá,
  `REG_TX_002` reporta `SemDado` honestamente.
