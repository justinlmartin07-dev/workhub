#!/usr/bin/env bash
# Idempotently ensure the server's pg_hba.conf allows remote replication
# connections (required by pg_basebackup and pg_receivewal; pg_dump is not
# affected). Some Postgres images only allow replication from localhost.
#
# Runs at container startup as the superuser: if no `replication ... all` rule
# exists, appends one (password-auth; the DB has no public endpoint, so this
# does not widen exposure) and reloads the config. Reads the file path from
# the server itself, and survives the database service being recreated —
# the next restart of this service reapplies it.
set -uo pipefail

psql "$DATABASE_URL" --no-password -v ON_ERROR_STOP=1 <<'SQL'
DO $do$
DECLARE
  cmd text := 'cat >> ' || current_setting('hba_file');
BEGIN
  IF EXISTS (SELECT 1 FROM pg_hba_file_rules
             WHERE database = '{replication}' AND address = 'all') THEN
    RAISE NOTICE 'pg_hba: remote replication rule already present';
  ELSE
    -- leading empty row guards against a file with no trailing newline
    EXECUTE format(
      $f$COPY (SELECT line FROM (VALUES (''), ('host replication all all scram-sha-256')) AS t(line)) TO PROGRAM %L$f$,
      cmd);
    PERFORM pg_reload_conf();
    RAISE NOTICE 'pg_hba: appended remote replication rule and reloaded config';
  END IF;
END
$do$;
SQL
