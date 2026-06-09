#!/bin/bash
# Tamircim — şifreli yedeği (.dump.age) ÇÖZER.
# Bunu KENDİ bilgisayarında çalıştır (özel/açma anahtarı yalnızca sende olmalı, sunucuda DEĞİL).
#
# Kullanım:
#   ./scripts/decrypt-backup.sh <ozel-anahtar.txt> <yedek.dump.age> [cikti.dump]
#
# Çıktı verilmezse .age uzantısı atılır (tamircim-...dump.age -> tamircim-...dump).
# Çözülen .dump'ı sonra sunucuya yükleyip restore-db.sh ile geri yüklersin.

set -euo pipefail

KEY="${1:-}"
ENC="${2:-}"
OUT="${3:-}"

if [ -z "$KEY" ] || [ -z "$ENC" ] || [ ! -f "$KEY" ] || [ ! -f "$ENC" ]; then
  echo "Kullanım: $0 <ozel-anahtar.txt> <yedek.dump.age> [cikti.dump]" >&2
  exit 1
fi
if ! command -v age >/dev/null 2>&1; then
  echo "HATA: 'age' kurulu değil. Önce age'i kur." >&2
  exit 1
fi

OUT="${OUT:-${ENC%.age}}"
age -d -i "$KEY" -o "$OUT" "$ENC"
echo "Çözüldü: $ENC -> $OUT"
