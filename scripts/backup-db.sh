#!/bin/bash
# Tamircim — günlük veritabanı yedeği (mantıksal, pg_dump custom format).
# İsteğe bağlı şifreleme: .env'de BACKUP_AGE_PUBLIC_KEY tanımlı + 'age' kuruluysa
# yedek o açık anahtarla ŞİFRELENİR (.dump.age) ve şifresiz hâli silinir.
# Anahtar yoksa eskisi gibi şifresiz çalışır (kademeli geçiş — bir şey bozulmaz).
#
# Kullanım: ./scripts/backup-db.sh
# Cron (her gece 03:00):
#   0 3 * * * /home/ubuntu/tamircimAPI/scripts/backup-db.sh >> /home/ubuntu/tamircimAPI/backups/backup.log 2>&1

set -euo pipefail

# --- Ayarlar ---
CONTAINER="${BACKUP_CONTAINER:-tamircim-postgres}"
DB="${BACKUP_DB:-TamircimDB}"
DB_USER="${BACKUP_DB_USER:-postgres}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BACKUP_DIR="${BACKUP_DIR:-$PROJECT_DIR/backups}"

# Şifreleme açık anahtarı: önce ortam değişkeni, yoksa .env'den oku.
# Açık anahtar GİZLİ DEĞİL — yedeği yalnızca KİLİTLER (açamaz). Açma anahtarı yalnızca sende.
AGE_PUBKEY="${BACKUP_AGE_PUBLIC_KEY:-}"
if [ -z "$AGE_PUBKEY" ] && [ -f "$PROJECT_DIR/.env" ]; then
  AGE_PUBKEY=$(grep -E '^BACKUP_AGE_PUBLIC_KEY=' "$PROJECT_DIR/.env" | head -1 | cut -d= -f2- | tr -d "\"'\r")
fi

mkdir -p "$BACKUP_DIR"
# Güvenlik: yedek TÜM kiracıların verisini içerir → klasör yalnızca sahibe erişilir.
chmod 700 "$BACKUP_DIR" 2>/dev/null || true

TS=$(date +%Y%m%d-%H%M%S)
RAW="$BACKUP_DIR/tamircim-$TS.dump"

# 1) pg_dump (custom format; sahiplik + RLS politikaları dahildir).
if ! docker exec "$CONTAINER" pg_dump -U "$DB_USER" -Fc "$DB" > "$RAW"; then
  echo "[$(date)] HATA: pg_dump başarısız (DB erişilemiyor olabilir)." >&2
  rm -f "$RAW"; exit 1
fi
if [ ! -s "$RAW" ]; then
  echo "[$(date)] HATA: Yedek boş: $RAW" >&2
  rm -f "$RAW"; exit 1
fi

# 2) Şifreleme (açık anahtar + age varsa). Başarılıysa şifresiz hâli silinir.
if [ -n "$AGE_PUBKEY" ] && command -v age >/dev/null 2>&1; then
  if age -r "$AGE_PUBKEY" -o "$RAW.age" "$RAW"; then
    rm -f "$RAW"
    FINAL="$RAW.age"
    chmod 600 "$FINAL" 2>/dev/null || true
    echo "[$(date)] ŞİFRELİ yedek alındı: $FINAL ($(du -h "$FINAL" | cut -f1))"
  else
    # Şifreleme patlarsa: yedeksiz kalma — şifresiz bırak ama yüksek sesle uyar.
    FINAL="$RAW"
    chmod 600 "$FINAL" 2>/dev/null || true
    echo "[$(date)] HATA: Şifreleme başarısız → yedek ŞİFRESİZ bırakıldı: $FINAL" >&2
  fi
else
  FINAL="$RAW"
  chmod 600 "$FINAL" 2>/dev/null || true
  if [ -n "$AGE_PUBKEY" ]; then
    echo "[$(date)] UYARI: Açık anahtar var ama 'age' kurulu değil → yedek ŞİFRESİZ: $FINAL" >&2
  else
    echo "[$(date)] Yedek alındı (şifresiz — açık anahtar tanımlı değil): $FINAL ($(du -h "$FINAL" | cut -f1))"
  fi
fi

# 3) Eski yedekleri temizle (.dump ve .dump.age, RETENTION_DAYS günden eski).
find "$BACKUP_DIR" -type f \( -name "tamircim-*.dump" -o -name "tamircim-*.dump.age" \) -mtime +"$RETENTION_DAYS" -delete
echo "[$(date)] $RETENTION_DAYS günden eski yedekler temizlendi."

# --- (OPSİYONEL) off-site yükleme ---
# S3/R2 hedefi belirlenince: aws s3 cp "$FINAL" "s3://$BUCKET/db/" --endpoint-url ...
# Yedek zaten şifreli olacağından off-site'a güvenle gider.
