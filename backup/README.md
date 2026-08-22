# WorkHub Database Backups

A small always-on Railway service that backs up the production Postgres to a
**dedicated** Cloudflare R2 bucket (separate from the photos bucket, with its
own credentials — a leak of one set of R2 keys never exposes both).

It produces three things, all pulled over Railway's private network — the
database service itself is never modified:

| R2 prefix | What | How |
|---|---|---|
| `dump/` | Daily logical dumps (`pg_dump -Fc`) | easiest restore, works across PG versions |
| `base/` | Daily physical base backups (`pg_basebackup`, tar+zstd) | anchor for PITR; also restorable standalone |
| `wal/` | Continuous WAL stream (`pg_receivewal`, zstd per segment) | replay on a base backup to recover to any point in time (~5 min RPO) |

Retention: everything older than `RETENTION_DAYS` (default 30) is pruned, but
only immediately after a successful new backup — if the pipeline breaks, the
last good backups are never aged out.

## Setup

### 1. Cloudflare R2

1. Create a new bucket, e.g. `workhub-db-backups`. Do not reuse the photos bucket.
2. Create an R2 API token scoped to **only this bucket**, permission
   *Object Read & Write*. Note the Access Key ID / Secret Access Key.

### 2. Railway service

1. New service → deploy from this GitHub repo, **root directory `/backup`**
   (Railway will pick up the Dockerfile). Deploy from `main` only — one backup
   service, pointed at production.
2. Optional but recommended: attach a small volume mounted at `/data` so an
   in-progress WAL segment survives restarts.
3. Set variables:

```
DATABASE_URL   = ${{Postgres.DATABASE_URL}}   # must resolve to the PRIVATE host (…railway.internal)
BACKUP_BUCKET  = workhub-db-backups

RCLONE_CONFIG_R2_TYPE              = s3
RCLONE_CONFIG_R2_PROVIDER          = Cloudflare
RCLONE_CONFIG_R2_ACCESS_KEY_ID    = <backup token key id>
RCLONE_CONFIG_R2_SECRET_ACCESS_KEY = <backup token secret>
RCLONE_CONFIG_R2_ENDPOINT          = https://<account id>.r2.cloudflarestorage.com

# Optional overrides (defaults shown)
RETENTION_DAYS             = 30
BASE_BACKUP_INTERVAL_HOURS = 24
WAL_SWITCH_SECONDS         = 300
WAL_SLOT_NAME              = workhub_backup
```

(`Postgres` above is the name of the database service in Railway; adjust the
reference if yours differs.)

4. Check the deploy logs: you should see a `dump:` and `base backup:` line at
   startup, then `backup completed …`. Within ~5 minutes of any database write,
   objects should start appearing under `wal/` in the bucket.

### 3. Verify (do this once, and roughly quarterly)

Restore the newest dump into a scratch database and look at the data:

```bash
rclone copy r2:workhub-db-backups/dump/ ./restore-test --max-age 2d
docker run -d --name restore-test -e POSTGRES_PASSWORD=test -p 5433:5432 postgres:16
pg_restore --no-owner -h localhost -p 5433 -U postgres -C -d postgres ./restore-test/workhub-<newest>.dump
```

A backup that has never been restored is a hope, not a backup.

## Restore playbooks

### Easy path: latest daily dump (up to 24h data loss)

1. Create a fresh Postgres service on Railway (same major version).
2. `pg_restore --no-owner --dbname "<new DATABASE_URL>" workhub-<stamp>.dump`
3. Point the API's `DATABASE_URL` at it, redeploy.

### PITR path: recover to a specific moment (e.g. just before bad data/compromise)

Recovery runs locally, then the result is loaded into Railway via a dump:

1. Pick the newest base backup **older than** your target time; download and
   unpack it, and download the WAL:

   ```bash
   mkdir pgdata wal
   rclone cat r2:workhub-db-backups/base/workhub-base-<stamp>.tar.zst | zstd -d | tar -x -C pgdata
   rclone copy r2:workhub-db-backups/wal/ ./wal && zstd -d --rm ./wal/*.zst
   ```

2. Configure recovery and start a throwaway local Postgres (same major version):

   ```bash
   touch pgdata/recovery.signal
   cat >> pgdata/postgresql.auto.conf <<'EOF'
   restore_command = 'cp /wal/%f %p'
   recovery_target_time = '2026-08-21 14:55:00+00'
   recovery_target_action = 'promote'
   EOF
   docker run -d --name pitr -v ./pgdata:/var/lib/postgresql/data -v ./wal:/wal -p 5433:5432 postgres:16
   ```

3. Watch `docker logs pitr` until it promotes, sanity-check the data, then
   `pg_dump` it and restore that dump into a fresh Railway Postgres as above.

### Full compromise playbook (with this in place)

1. Rotate `JWT_SECRET_KEY` on Railway → every access token dies instantly.
2. `DELETE FROM refresh_tokens;` → every session dies.
3. Reset all user passwords.
4. If data was tampered with: PITR restore to just before the tampering.
5. Rotate the R2 tokens (photos + backups) and the database password.

## Troubleshooting

- **`pg_receivewal` fails with a `pg_hba.conf` error about replication** —
  dumps and base backups still work (they use a normal connection), but WAL
  streaming uses the replication protocol, which `pg_hba.conf` gates
  separately, and some images don't allow it remotely. Fix as superuser
  (appends an hba rule and reloads):

  ```sql
  COPY (SELECT 'host replication all all scram-sha-256')
    TO PROGRAM 'bash -c "cat >> $PGDATA/pg_hba.conf"';
  SELECT pg_reload_conf();
  ```

  If that feels too invasive, fall back to more frequent dumps instead of PITR:
  set `BASE_BACKUP_INTERVAL_HOURS=1` and ignore the WAL warnings (RPO becomes 1h).

- **Database disk usage climbing** — the replication slot holds WAL while this
  service is down. Fix the service, or if retiring it permanently:
  `SELECT pg_drop_replication_slot('workhub_backup');`

- **Monitoring** — cheapest signal: R2 dashboard → bucket → check the newest
  object under `dump/` is less than a day old. Worth a recurring reminder.
