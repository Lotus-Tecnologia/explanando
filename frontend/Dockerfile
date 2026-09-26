# syntax=docker/dockerfile:1
#
# O frontend em Node, na própria VPS (ADR-0019) — sai o Cloudflare Workers como runtime, fica
# o Cloudflare como CDN na frente.
#
# **O build precisa da API de pé.** Os 631 perfis, os rankings e as unidades do Estado são
# pré-renderizados buscando a API, e sem ela o build degrada em silêncio: sai verde, com a
# home dizendo "o ranking não pôde ser carregado" e o sitemap com 4 endereços em vez de ~870.
# Por isso ele roda NA VPS, depois da API, com `network: host` — ver deploy/compose.yml e
# deploy/atualizar.sh. Construído na arquitetura da máquina, é arm64 sem emulação.

# ── Dependências ─────────────────────────────────────────────────────────────────────────
#
# Separadas do código pelo mesmo motivo do backend: `npm ci` é a parte lenta, e só precisa
# refazer quando o lockfile muda.
FROM node:22-alpine AS dependencias
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# ── Build ────────────────────────────────────────────────────────────────────────────────
FROM node:22-alpine AS construcao
WORKDIR /app
COPY --from=dependencias /app/node_modules ./node_modules
COPY . .

# `public/` é opcional para o Next e hoje está vazia — e o git não guarda pasta vazia. Sem
# ela, o COPY do estágio de execução falha com "/app/public: not found".
RUN mkdir -p public

# Lidas NO BUILD e embutidas nas páginas: o endereço público do site (canônicos, sitemap,
# tags de compartilhamento) e a trava de indexação — só a grafia exata `true` liga.
ARG NEXT_PUBLIC_SITE_URL=https://explanando.com.br
ARG NEXT_PUBLIC_INDEXAVEL=

# De onde o build busca os dados. Não é `NEXT_PUBLIC_`, de propósito: o Next embutiria o
# endereço no pacote do navegador, e ele só vale dentro da máquina. Em execução o contêiner
# recebe outro valor (`http://api:8080`, a rede do compose) — ver lib/api.ts.
ARG EXPLANANDO_API=http://127.0.0.1:8080

ENV NEXT_TELEMETRY_DISABLED=1 \
    NEXT_PUBLIC_SITE_URL=$NEXT_PUBLIC_SITE_URL \
    NEXT_PUBLIC_INDEXAVEL=$NEXT_PUBLIC_INDEXAVEL \
    EXPLANANDO_API=$EXPLANANDO_API

# A chave interna entra como SEGREDO de build, e não como ARG: ARG fica gravado no
# histórico da imagem (`docker history` mostra), e a chave que libera o teto da API deixaria
# de ser segredo para qualquer um com a imagem. Sem ela, os ~870 fetches do build esbarram
# no limite de 120/min e o build quebra com 429 — medido na Etapa 08.
RUN --mount=type=secret,id=chave_interna,env=EXPLANANDO_CHAVE_INTERNA \
    npm run build

# ── Execução ─────────────────────────────────────────────────────────────────────────────
#
# `output: "standalone"` (next.config.ts) copia para `.next/standalone` só o que o servidor
# importa — sem o node_modules de desenvolvimento, que é quase todo o peso. Os estáticos e o
# `public/` não vão junto por decisão do Next; a cópia é nossa.
FROM node:22-alpine AS execucao
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    HOSTNAME=0.0.0.0 \
    PORT=3000

COPY --from=construcao --chown=node:node /app/public ./public
COPY --from=construcao --chown=node:node /app/.next/standalone ./
COPY --from=construcao --chown=node:node /app/.next/static ./.next/static

# Não-root, como a API. O usuário `node` já existe na imagem oficial.
USER node
EXPOSE 3000

# `wget` vem no busybox do Alpine; o robots.txt é a rota mais barata que ainda passa pelo
# servidor do Next inteiro.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:3000/robots.txt || exit 1

CMD ["node", "server.js"]
