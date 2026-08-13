#!/usr/bin/env bash
# Backup diario: pg_dump + uploads → tar → guardado em /var/backups/aob e opcionalmente Cloudflare R2.
# Correr em cron: 0 3 * * * /opt/aob/infra/deploy/backup.sh
set -euo pipefail

BACKUP_DIR=/var/backups/aob
STAMP=$(date +%Y%m%d-%H%M%S)
RETAIN_DAYS=14

mkdir -p "$BACKUP_DIR"

echo "[backup $STAMP] pg_dump"
sudo -u postgres pg_dump aob_prod | gzip > "$BACKUP_DIR/aob_prod-$STAMP.sql.gz"

echo "[backup $STAMP] uploads (tar.gz)"
tar -czf "$BACKUP_DIR/uploads-$STAMP.tar.gz" -C /var/www uploads

echo "[backup $STAMP] limpar >$RETAIN_DAYS dias"
find "$BACKUP_DIR" -type f -name '*.gz' -mtime "+$RETAIN_DAYS" -delete

# Upload opcional para Cloudflare R2 (se rclone configurado com remote 'r2')
if command -v rclone >/dev/null && rclone listremotes | grep -q '^r2:'; then
    echo "[backup $STAMP] rclone r2:aob-backups/"
    rclone copy "$BACKUP_DIR" r2:aob-backups/ --include "*-$STAMP.*"
fi

echo "[backup $STAMP] concluido"
