# O backup e a conferência dele, numa imagem só.
#
# A partir do Postgres 17 oficial, e não de uma imagem genérica com o cliente instalado à
# parte: o `pg_dump` precisa ser da mesma versão maior do servidor (ou mais nova), e usar a
# mesma imagem do banco garante isso sem ninguém lembrar de conferir na próxima atualização.
#
# O rclone fala S3 com o R2 e com o S3 local do teste (compose.local.yml) pela mesma linha
# de comando — e é ele mesmo quem serve esse S3 local.
FROM postgres:17-alpine

RUN apk add --no-cache rclone curl

# O destino inteiro vem de variáveis de ambiente (compose.yml); sem isto, cada chamada do
# rclone avisa que não achou arquivo de configuração — ruído que esconde o aviso que importa.
ENV RCLONE_CONFIG=/dev/null

COPY backup.sh conferir.sh restaurar.sh /usr/local/bin/
RUN chmod 0755 /usr/local/bin/backup.sh /usr/local/bin/conferir.sh /usr/local/bin/restaurar.sh

# Não é o servidor: o ENTRYPOINT do Postgres iniciaria um banco aqui dentro.
ENTRYPOINT []
CMD ["backup.sh"]
