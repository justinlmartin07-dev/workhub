#!/usr/bin/env bash
# Takes a backup at startup and then every BASE_BACKUP_INTERVAL_HOURS.
# (Startup run means every deploy/restart also produces a fresh backup —
# harmless at this database size, and it self-heals missed runs.)
set -uo pipefail

INTERVAL_HOURS="${BASE_BACKUP_INTERVAL_HOURS:-24}"

while true; do
    if /backup/take-backup.sh; then
        echo "backup completed $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    else
        echo "WARN: backup failed; retrying in 1h" >&2
        sleep 3600
        continue
    fi
    sleep $((INTERVAL_HOURS * 3600))
done
