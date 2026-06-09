#!/bin/bash
# Tamircim — veritabanını bir yedekten geri yükler.
#
# Kullanım:        ./scripts/restore-db.sh <yedek-dosyasi.dump>
# Sormadan (otomasyon):  FORCE=1 ./scripts/restore-db.sh <yedek-dosyasi.dump>
#
# DİKKAT: Mevcut verinin üzerine yazar (--clean: önce mevcut nesneleri siler,
# sonra yedekteki haliyle yeniden oluşturur). Önce bir test/kopya ortamında dene.

set -euo pipefail

CONTAINER="${BACKUP_CONTAINER:-tamircim-postgres}"
DB="${BACKUP_DB:-TamircimDB}"
DB_USER="${BACKUP_DB_USER:-postgres}"

FILE="${1:-}"
if [ -z "$FILE" ] || [ ! -f "$FILE" ]; then
  echo "Kullanım: $0 <yedek-dosyasi.dump>" >&2
  exit 1
fi

if [ "${FORCE:-0}" != "1" ]; then
  echo "DİKKAT: '$DB' veritabanı '$FILE' yedeğiyle DEĞİŞTİRİLECEK. Mevcut veri silinecek."
  read -r -p "Devam etmek için EVET yazın: " confirm
  [ "$confirm" = "EVET" ] || { echo "İptal edildi."; exit 1; }
fi

# Yedeği container'a kopyala, pg_restore ile geri yükle.
# --clean --if-exists: mevcut nesneleri (varsa) silip yeniden kurar.
# Sahiplik dump'tan korunur (tablolar tamircim_app'e ait kalır → RLS bozulmaz);
# bu yüzden tamircim_app rolünün hedef DB'de mevcut olması gerekir (db-init kurar).
docker cp "$FILE" "$CONTAINER:/tmp/restore.dump"
docker exec "$CONTAINER" pg_restore -U "$DB_USER" -d "$DB" --clean --if-exists /tmp/restore.dump
docker exec "$CONTAINER" rm -f /tmp/restore.dump
echo "Geri yükleme tamamlandı: $FILE → $DB"
