#!/usr/bin/env bash
# Streams WAL from Postgres via the replication protocol (pg_receivewal) into
# WAL_DIR, and ships completed segments to R2 (zstd-compressed), deleting them
# locally once uploaded. A replication slot makes the server retain any WAL we
# haven't received yet across our restarts.
#
# CAUTION: the slot means a long outage of THIS service grows the database's
# disk usage (server holds WAL for us). If this service is ever retired, drop
# the slot: SELECT pg_drop_replication_slot('<slot>');
set -uo pipefail

WAL_DIR="${WAL_DIR:-/data/wal}"
SLOT="${WAL_SLOT_NAME:-workhub_backup}"
# How often to force out the in-progress segment so a quiet server still gets
# its recent writes into R2 (pg_switch_wal is a no-op when nothing was written).
SWITCH_SECONDS="${WAL_SWITCH_SECONDS:-300}"

mkdir -p "$WAL_DIR"

upload_completed_segments() {
    local f base
    for f in "$WAL_DIR"/*; do
        [ -f "$f" ] || continue
        case "$f" in *.partial) continue ;; esac
        base="$(basename "$f")"
        if zstd -q -c "$f" | rclone rcat "r2:${BACKUP_BUCKET}/wal/${base}.zst"; then
            rm -f "$f"
        else
            echo "WARN: failed to upload WAL segment ${base}; will retry" >&2
        fi
    done
}

# Uploader + segment-switch loop
(
    while sleep "$SWITCH_SECONDS"; do
        psql "$DATABASE_URL" -Atqc "SELECT pg_switch_wal();" >/dev/null 2>&1 \
            || echo "WARN: pg_switch_wal failed (connection issue?)" >&2
        upload_completed_segments
    done
) &

# Receiver loop. pg_receivewal already retries lost connections internally;
# this loop covers hard failures (e.g. pg_hba rejecting replication — see
# README troubleshooting) without killing the dump half of the service.
while true; do
    pg_receivewal --dbname="$DATABASE_URL" --slot="$SLOT" \
        --create-slot --if-not-exists --no-password \
        || echo "WARN: slot creation failed; see README (pg_hba replication entry)" >&2

    pg_receivewal --dbname="$DATABASE_URL" --slot="$SLOT" \
        --directory="$WAL_DIR" --no-password
    echo "WARN: pg_receivewal exited; retrying in 60s" >&2
    sleep 60
done
