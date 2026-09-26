#!/usr/bin/env bash
#
# Backup diário do Postgres para fora da máquina (ADR-0019 §4).
#
# A Oracle recupera instância Always Free ociosa, e o disco vai junto. Por isso o dump sai
# daqui para o R2 — outro fornecedor, de propósito: backup no mesmo fornecedor que pode
# recuperar a instância é meio backup.
#
# O que sobe, por execução:
#   diarios/explanando-AAAA-MM-DDTHHMMZ.dump       pg_dump no formato custom (comprimido)
#   diarios/explanando-AAAA-MM-DDTHHMMZ.contagens  linhas por tabela, no MESMO instante do dump
#   diarios/explanando-AAAA-MM-DDTHHMMZ.sha256     para a conferência saber que baixou inteiro
# e, no dia 1 de cada mês, uma cópia em mensais/.
#
# As contagens são o que dá sentido ao teste de restauração (conferir.sh): restaurar e ver
# que "deu certo" não prova nada; restaurar e achar exatamente as mesmas linhas, tabela a
# tabela, prova.
set -euo pipefail

: "${BACKUP_BALDE:?BACKUP_BALDE nao definido}"
# O compose deixa estas passarem vazias para que o site suba antes de o R2 existir; quem
# precisa delas e' esta rotina, e e' aqui que a falta vira erro legivel.
: "${RCLONE_CONFIG_DESTINO_ENDPOINT:?BACKUP_ENDPOINT nao definido no .env}"
: "${RCLONE_CONFIG_DESTINO_ACCESS_KEY_ID:?BACKUP_CHAVE_ID nao definido no .env}"
: "${RCLONE_CONFIG_DESTINO_SECRET_ACCESS_KEY:?BACKUP_CHAVE_SECRETA nao definido no .env}"
DIAS_DIARIOS="${BACKUP_DIAS_DIARIOS:-15}"
DIAS_MENSAIS="${BACKUP_DIAS_MENSAIS:-400}"

DESTINO="destino:${BACKUP_BALDE}"
MOMENTO="$(date -u +%Y-%m-%dT%H%MZ)"
NOME="explanando-${MOMENTO}"
PASTA="$(mktemp -d)"

avisar() {
    # Opcional. Falha do aviso nunca derruba o backup, que é o que importa.
    if [ -n "${AVISO_URL:-}" ]; then
        curl -fsS -m 10 --retry 3 "${AVISO_URL}${1:-}" >/dev/null 2>&1 || true
    fi
}

terminar() {
    local codigo=$?
    rm -rf "$PASTA"
    if [ "$codigo" -ne 0 ]; then
        echo "BACKUP FALHOU (codigo $codigo)." >&2
        avisar /fail
    fi
    exit "$codigo"
}
trap terminar EXIT

# Linhas por tabela do esquema public, numa consulta só. `query_to_xml` é o jeito de rodar
# um COUNT dinâmico por tabela sem criar função no banco de produção.
CONTAGEM="select table_name || E'\t' || (xpath('/row/c/text()', query_to_xml(format('select count(*) as c from public.%I', table_name), false, true, '')))[1]::text from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE' order by table_name;"

echo "== Backup ${NOME} =="

# ── Um instante só para o dump e para as contagens ─────────────────────────────────────
#
# O `pg_dump` já tira uma fotografia consistente do banco, mas a fotografia é dele: uma
# contagem feita um segundo depois pode incluir despesa que o worker gravou nesse segundo, e
# a conferência acusaria diferença onde não há defeito. Por isso uma sessão abre a
# transação, exporta a fotografia, conta nela, e o `pg_dump` usa a MESMA fotografia.
coproc SESSAO { psql -X -q -A -t -v ON_ERROR_STOP=1; }
printf '%s\n' \
    "BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;" \
    "SELECT pg_export_snapshot();" >&"${SESSAO[1]}"
read -r FOTOGRAFIA <&"${SESSAO[0]}"

printf '%s\n' "$CONTAGEM" "SELECT '--fim--';" >&"${SESSAO[1]}"
: > "$PASTA/$NOME.contagens"
while read -r linha <&"${SESSAO[0]}"; do
    [ "$linha" = "--fim--" ] && break
    printf '%s\n' "$linha" >> "$PASTA/$NOME.contagens"
done

pg_dump --snapshot="$FOTOGRAFIA" --format=custom --compress=6 --file="$PASTA/$NOME.dump"

printf '%s\n' "COMMIT;" >&"${SESSAO[1]}"
exec {SESSAO[1]}>&-
wait "$SESSAO_PID" || true

# O arquivo abre? `pg_restore --list` lê o índice inteiro sem restaurar nada — barato, e
# pega o dump truncado antes de ele substituir um bom na nuvem.
pg_restore --list "$PASTA/$NOME.dump" > /dev/null

( cd "$PASTA" && sha256sum "$NOME.dump" > "$NOME.sha256" )

TABELAS=$(wc -l < "$PASTA/$NOME.contagens")
TAMANHO=$(du -h "$PASTA/$NOME.dump" | cut -f1)
echo "Dump: ${TAMANHO}, ${TABELAS} tabelas contadas."

# ── Subida ─────────────────────────────────────────────────────────────────────────────
#
# O .dump sobe por último de propósito: a conferência procura o .dump mais recente, e ele
# só aparece quando as contagens e o sha256 dele já estão lá.
for extensao in contagens sha256 dump; do
    rclone copyto --s3-no-check-bucket "$PASTA/$NOME.$extensao" "$DESTINO/diarios/$NOME.$extensao"
done

if [ "$(date -u +%d)" = "01" ]; then
    for extensao in contagens sha256 dump; do
        rclone copyto --s3-no-check-bucket "$PASTA/$NOME.$extensao" "$DESTINO/mensais/$NOME.$extensao"
    done
    echo "Copia mensal gravada."
fi

# ── Retenção ───────────────────────────────────────────────────────────────────────────
#
# Só depois de a cópia nova estar lá. Com `set -e`, uma subida que falhou nunca chega aqui
# — e é isso que impede a rotina de apagar o último backup bom por causa de um problema de
# rede.
rclone delete --min-age "${DIAS_DIARIOS}d" "$DESTINO/diarios"
rclone delete --min-age "${DIAS_MENSAIS}d" "$DESTINO/mensais"

echo "Backup ${NOME} concluido."
avisar
