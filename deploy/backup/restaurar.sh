#!/usr/bin/env bash
#
# O dia do desastre: traz um dump do R2 de volta para o banco de PRODUÇÃO.
#
#     docker compose run --rm -e RESTAURAR_CONFIRMO=sim backup restaurar.sh            (o mais recente)
#     docker compose run --rm -e RESTAURAR_CONFIRMO=sim backup restaurar.sh explanando-2026-09-26T0730Z
#
# É destrutivo por natureza, então tem duas travas:
#   - RESTAURAR_CONFIRMO=sim, dito explicitamente;
#   - e recusa banco que já tem tabelas, a menos que RESTAURAR_SOBRE_DADOS=sim. O caso normal
#     é uma máquina nova com o volume vazio; restaurar por cima de uma base viva é outra
#     decisão, e merece ser tomada de propósito.
#
# A API e o frontend devem estar parados durante a restauração (`docker compose stop api web`).
set -euo pipefail

: "${BACKUP_BALDE:?BACKUP_BALDE nao definido}"
: "${RCLONE_CONFIG_DESTINO_ENDPOINT:?BACKUP_ENDPOINT nao definido no .env}"

if [ "${RESTAURAR_CONFIRMO:-}" != "sim" ]; then
    echo "Isto substitui o banco de producao. Rode com -e RESTAURAR_CONFIRMO=sim." >&2
    exit 2
fi

DESTINO="destino:${BACKUP_BALDE}"
PASTA="$(mktemp -d)"
trap 'rm -rf "$PASTA"' EXIT

NOME="${1:-}"
if [ -z "$NOME" ]; then
    # Procura nos diários e nos mensais: numa conta nova, os diários podem já ter vencido.
    NOME=$( { rclone lsf --files-only --include '*.dump' "$DESTINO/diarios" | sed 's#^#diarios/#';
              rclone lsf --files-only --include '*.dump' "$DESTINO/mensais" | sed 's#^#mensais/#'; } \
            | sort -t/ -k2 | tail -n 1 )
    [ -n "$NOME" ] || { echo "Nenhum dump no balde." >&2; exit 10; }
    NOME="${NOME%.dump}"
else
    NOME="diarios/${NOME%.dump}"
fi

echo "== Restaurando ${NOME} =="

TABELAS=$(psql -X -A -t -c "select count(*) from information_schema.tables where table_schema = 'public'")
if [ "$TABELAS" -gt 0 ] && [ "${RESTAURAR_SOBRE_DADOS:-}" != "sim" ]; then
    echo "O banco ja tem ${TABELAS} tabelas. Para restaurar por cima, acrescente -e RESTAURAR_SOBRE_DADOS=sim." >&2
    exit 3
fi

for extensao in dump sha256 contagens; do
    rclone copyto "$DESTINO/$NOME.$extensao" "$PASTA/$(basename "$NOME").$extensao"
done
( cd "$PASTA" && sha256sum -c "$(basename "$NOME").sha256" )

pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error --jobs=2 \
    --dbname="$PGDATABASE" "$PASTA/$(basename "$NOME").dump"

echo "Restaurado. Confira as contagens do instante do dump:"
cat "$PASTA/$(basename "$NOME").contagens"
