# Backup sidecar for the Railway Postgres service.
# Base image major version MUST match the database server's major version
# (pg_basebackup/pg_receivewal are not forward-compatible). Check with:
#   SELECT version();
FROM postgres:16-alpine

RUN apk add --no-cache bash rclone zstd

WORKDIR /backup
COPY *.sh ./
# Strip CRLF in case the build context came from a Windows checkout
RUN sed -i 's/\r$//' *.sh && chmod +x *.sh

ENTRYPOINT ["/backup/entrypoint.sh"]
