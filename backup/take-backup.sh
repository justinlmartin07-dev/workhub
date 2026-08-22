#!/usr/bin/env bash
# One backup cycle:
#   1. Logical dump (pg_dump custom format) — the easy, version-flexible restore.
#   2. Physical base backup (pg_basebackup tar) — the PITR anchor; replay
#      archived WAL from wal/ on top of it to recover to a point in time.
#      --wal-method=fetch embeds enough WAL to make the tar restorable on its own.
#   3. Retention pruning. Runs ONLY after this cycle succeeded (set -e), so a
#      broken backup pipeline can never age out the last good backups.
set -euo pipefail

: "${DATABASE_URL:?DATABASE_URL is required}"
: "${BACKUP_BUCKET:?BACKUP_BUCKET is required}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

STAMP="$(date -u +%Y-%m-%dT%H-%M-%SZ)"

echo "dump: workhub-${STAMP}.dump"
pg_dump --dbname="$DATABASE_URL" --format=custom --no-password \
    | rclone rcat "r2:${BACKUP_BUCKET}/dump/workhub-${STAMP}.dump"

echo "base backup: workhub-base-${STAMP}.tar.zst"
pg_basebackup --dbname="$DATABASE_URL" --pgdata=- --format=tar \
    --wal-method=fetch --checkpoint=fast --no-slot --no-password \
    | zstd -q -c \
    | rclone rcat "r2:${BACKUP_BUCKET}/base/workhub-base-${STAMP}.tar.zst"

echo "pruning objects older than ${RETENTION_DAYS}d"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/dump/"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/base/"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/wal/"
