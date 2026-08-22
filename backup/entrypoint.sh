#!/usr/bin/env bash
# Runs both halves of the backup service:
#   - wal-stream.sh: continuous WAL archiving to R2 (PITR)
#   - schedule.sh:   periodic pg_dump + pg_basebackup to R2, then retention pruning
# If either half dies the container exits so Railway restarts it.
set -uo pipefail

: "${DATABASE_URL:?DATABASE_URL is required}"
: "${BACKUP_BUCKET:?BACKUP_BUCKET is required}"

/backup/wal-stream.sh &
/backup/schedule.sh &

wait -n
echo "FATAL: a backup process exited; container will restart" >&2
exit 1
