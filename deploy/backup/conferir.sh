#!/usr/bin/env bash
#
# O teste de restauração: backup que nunca foi restaurado é esperança, não backup (ADR-0019 §4).
#
# Baixa o dump mais recente do R2 — do R2, e não do disco local, porque é de lá que ele
# viria no dia do desastre —, restaura num banco à parte no mesmo servidor, e compara as
# linhas de cada tabela com as contagens tiradas no instante do dump. Qualquer diferença,
# arquivo corrompido, ou backup velho demais é falha, com código de saída diferente de zero.
#
# O banco de conferência é apagado no fim, dê certo ou errado.
set -euo pipefail

: "${BACKUP_BALDE:?BACKUP_BALDE nao definido}"
# O compose deixa estas passarem vazias para que o site suba antes de o R2 existir; quem
# precisa delas e' esta rotina, e e' aqui que a falta vira erro legivel.
: "${RCLONE_CONFIG_DESTINO_ENDPOINT:?BACKUP_ENDPOINT nao definido no .env}"
: "${RCLONE_CONFIG_DESTINO_ACCESS_KEY_ID:?BACKUP_CHAVE_ID nao definido no .env}"
: "${RCLONE_CONFIG_DESTINO_SECRET_ACCESS_KEY:?BACKUP_CHAVE_SECRETA nao definido no .env}"
IDADE_MAXIMA_HORAS="${CONFERENCIA_IDADE_MAXIMA_HORAS:-36}"
BANCO="explanando_conferencia"

DESTINO="destino:${BACKUP_BALDE}"
PASTA="$(mktemp -d)"

avisar() {
    if [ -n "${CONFERENCIA_AVISO_URL:-}" ]; then
        curl -fsS -m 10 --retry 3 "${CONFERENCIA_AVISO_URL}${1:-}" >/dev/null 2>&1 || true
    fi
}

terminar() {
    local codigo=$?
    dropdb --if-exists "$BANCO" 2>/dev/null || true
    rm -rf "$PASTA"
    if [ "$codigo" -ne 0 ]; then
        echo "CONFERENCIA FALHOU (codigo $codigo)." >&2
        avisar /fail
    fi
    exit "$codigo"
}
trap terminar EXIT

CONTAGEM="select table_name || E'\t' || (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from public.%I', table_name), false, true, '')))[1]::text from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE' order by table_name;"

# ── O mais recente ─────────────────────────────────────────────────────────────────────
ULTIMO=$(rclone lsf --files-only --include '*.dump' "$DESTINO/diarios" | sort | tail -n 1)

if [ -z "$ULTIMO" ]; then
    echo "Nenhum dump em ${DESTINO}/diarios." >&2
    exit 10
fi

NOME="${ULTIMO%.dump}"
echo "== Conferindo ${NOME} =="

# O nome carrega o instante (explanando-AAAA-MM-DDTHHMMZ). Um backup parado há dias é o
# defeito que esta rotina mais precisa pegar — o dump pode estar perfeito e ser de semana
# passada.
CARIMBO="${NOME#explanando-}"
QUANDO="${CARIMBO:0:10} ${CARIMBO:11:2}:${CARIMBO:13:2}:00"
IDADE_HORAS=$(( ( $(date -u +%s) - $(date -u -d "$QUANDO" +%s) ) / 3600 ))

if [ "$IDADE_HORAS" -gt "$IDADE_MAXIMA_HORAS" ]; then
    echo "O dump mais recente tem ${IDADE_HORAS} h (limite: ${IDADE_MAXIMA_HORAS} h). O backup diario parou." >&2
    exit 11
fi

for extensao in dump contagens sha256; do
    rclone copyto "$DESTINO/diarios/$NOME.$extensao" "$PASTA/$NOME.$extensao"
done

( cd "$PASTA" && sha256sum -c "$NOME.sha256" ) || { echo "sha256 nao confere." >&2; exit 12; }

# ── Restauração ────────────────────────────────────────────────────────────────────────
#
# Mesma collation do banco de produção (compose.yml): restaurar em outra faria a
# conferência passar num banco que ordena texto diferente do original.
dropdb --if-exists "$BANCO"
createdb --template=template0 --encoding=UTF8 --locale=C "$BANCO"

INICIO=$(date +%s)
pg_restore --no-owner --no-privileges --exit-on-error --jobs=2 --dbname="$BANCO" "$PASTA/$NOME.dump"
echo "Restaurado em $(( $(date +%s) - INICIO )) s."

psql -X -q -A -t -v ON_ERROR_STOP=1 -d "$BANCO" -c "$CONTAGEM" > "$PASTA/restaurado.contagens"

if ! diff -u "$PASTA/$NOME.contagens" "$PASTA/restaurado.contagens"; then
    echo "As linhas restauradas NAO batem com as do instante do dump (diferenca acima)." >&2
    exit 13
fi

TABELAS=$(wc -l < "$PASTA/restaurado.contagens")
LINHAS=$(awk -F'\t' '{ soma += $2 } END { print soma }' "$PASTA/restaurado.contagens")
echo "OK: ${TABELAS} tabelas, ${LINHAS} linhas, identicas ao instante do dump (${IDADE_HORAS} h atras)."
avisar
