#!/usr/bin/env bash
# One backup cycle:
#   1. Logical dump (pg_dump custom format) — the easy, version-flexible restore.
#   2. Physical base backup (pg_basebackup tar) — the PITR anchor.
#      --wal-method=fetch embeds enough WAL to make the tar restorable on its own.
#   3. Retention pruning. Runs ONLY after this cycle succeeded (set -e), so a
#      broken backup pipeline can never age out the last good backups.
#
# Each artifact is staged to local disk first and only uploaded once fully
# written — a failed pg_dump/pg_basebackup must never leave a truncated
# object in the bucket, where it could be mistaken for a restorable backup.
set -euo pipefail

: "${DATABASE_URL:?DATABASE_URL is required}"
: "${BACKUP_BUCKET:?BACKUP_BUCKET is required}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"

STAMP="$(date -u +%Y-%m-%dT%H-%M-%SZ)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

echo "dump: workhub-${STAMP}.dump"
pg_dump --dbname="$DATABASE_URL" --format=custom --no-password \
    > "$STAGE/workhub-${STAMP}.dump"
rclone moveto "$STAGE/workhub-${STAMP}.dump" \
    "r2:${BACKUP_BUCKET}/dump/workhub-${STAMP}.dump"

echo "base backup: workhub-base-${STAMP}.tar.zst"
pg_basebackup --dbname="$DATABASE_URL" --pgdata=- --format=tar \
    --wal-method=fetch --checkpoint=fast --no-slot --no-password \
    | zstd -q -c > "$STAGE/workhub-base-${STAMP}.tar.zst"
rclone moveto "$STAGE/workhub-base-${STAMP}.tar.zst" \
    "r2:${BACKUP_BUCKET}/base/workhub-base-${STAMP}.tar.zst"

echo "pruning objects older than ${RETENTION_DAYS}d"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/dump/"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/base/"
rclone delete --min-age "${RETENTION_DAYS}d" "r2:${BACKUP_BUCKET}/wal/"
