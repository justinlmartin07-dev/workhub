# Backup sidecar for the Railway Postgres service.
# Base image major version MUST match the database server's major version
# (pg_dump/pg_basebackup/pg_receivewal can't talk to a NEWER server). Check with:
#   SELECT version();   -- production is 18.x as of 2026-08
FROM postgres:18-alpine

RUN apk add --no-cache bash rclone zstd

# Bucket-scoped R2 tokens can't HeadBucket/CreateBucket; without this rclone
# mistakes the 403 for a missing bucket and tries to create it (AccessDenied).
ENV RCLONE_CONFIG_R2_NO_CHECK_BUCKET=true

WORKDIR /backup
COPY *.sh ./
# Strip CRLF in case the build context came from a Windows checkout
RUN sed -i 's/\r$//' *.sh && chmod +x *.sh

ENTRYPOINT ["/backup/entrypoint.sh"]
