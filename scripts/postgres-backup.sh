#!/bin/sh

set -eu

BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_COUNT="${BACKUP_RETENTION_COUNT:-7}"
SLEEP_SECONDS="${BACKUP_INTERVAL_SECONDS:-86400}"

mkdir -p "$BACKUP_DIR"

export PGPASSWORD="${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"

backup_once() {
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
  backup_name="${POSTGRES_DB}_${timestamp}.dump"
  tmp_path="$BACKUP_DIR/.${backup_name}.tmp"
  final_path="$BACKUP_DIR/$backup_name"

  pg_dump \
    --format=custom \
    --clean \
    --if-exists \
    --no-owner \
    --no-privileges \
    --host="${PGHOST:?PGHOST is required}" \
    --port="${PGPORT:-5432}" \
    --username="${POSTGRES_USER:?POSTGRES_USER is required}" \
    --dbname="${POSTGRES_DB:?POSTGRES_DB is required}" \
    --file="$tmp_path"

  mv "$tmp_path" "$final_path"
  chmod 600 "$final_path"

  count=0
  for file in $(ls -1t "$BACKUP_DIR"/*.dump 2>/dev/null || true); do
    count=$((count + 1))
    if [ "$count" -gt "$RETENTION_COUNT" ]; then
      rm -f "$file"
    fi
  done

  echo "Created PostgreSQL backup: $final_path"
}

while true; do
  backup_once
  sleep "$SLEEP_SECONDS"
done
