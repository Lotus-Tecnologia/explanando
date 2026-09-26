#!/usr/bin/env bash
#
# Atualiza a produção a partir da main: puxa o código, reconstrói e sobe tudo na ordem certa.
#
#     cd /opt/explanando/deploy && ./atualizar.sh
#
# A ordem não é estética:
#   1. o esquema (migracoes) antes da API — a API não migra, e contra esquema velho dá 500;
#   2. a API de pé ANTES do build do frontend — o build pré-renderiza buscando a API, e sem
#      ela sai verde e vazio (ver frontend/Dockerfile);
#   3. o frontend novo só substitui o antigo depois de construído: se o build quebrar, o site
#      que está no ar continua no ar.
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -f .env ]; then
    echo "Falta deploy/.env (copie de exemplo.env e preencha)." >&2
    exit 1
fi

# Exportado, e não só lido pelo compose. O compose usa o .env para interpolar o YAML, mas o
# segredo de build (`secrets.chave_interna.environment`) é lido pelo buildx do AMBIENTE do
# processo — medido: sem o export, o build do frontend sai sem a chave interna, cai no teto
# público de 120/min e quebra com 429 na página 245 de 982.
set -a
# shellcheck disable=SC1091
. ./.env
set +a

echo "== Codigo"
git pull --ff-only

echo "== Imagens do backend"
docker compose build migracoes api worker backup

echo "== Banco, esquema e API"
docker compose up -d postgres migracoes api

echo "== Esperando a API responder"
for tentativa in $(seq 1 60); do
    if curl -fsS -o /dev/null http://127.0.0.1:8080/saude; then
        break
    fi
    if [ "$tentativa" -eq 60 ]; then
        echo "A API nao respondeu em 2 minutos. Veja: docker compose logs api" >&2
        exit 1
    fi
    sleep 2
done

echo "== Frontend (pre-renderiza contra a API; leva alguns minutos)"
docker compose build web

echo "== Subindo o resto"
docker compose up -d

# Imagens antigas acumulam a cada atualização — centenas de MB por vez num disco de 47 GB.
docker image prune -f > /dev/null

echo
docker compose ps
echo
echo "Confira:  curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:3000/"
