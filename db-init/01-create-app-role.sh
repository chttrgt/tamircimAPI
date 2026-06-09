#!/bin/bash
# Postgres ilk kurulumda (boş veri dizini) bir kez çalışır — superuser olarak.
# Uygulamanın çalışma-zamanı rolünü oluşturur: tamircim_app.
#
# KRİTİK GÜVENLİK: Uygulama ASLA superuser (postgres) ile bağlanmamalıdır. Superuser
# ve BYPASSRLS rolleri Row-Level Security'yi FORCE olsa bile tamamen atlar → tenant
# izolasyonu çöker. tamircim_app: NOSUPERUSER + NOBYPASSRLS olduğundan RLS'e tabidir.
# Tabloları migration ile bu rol oluşturur (public şemasının sahibi) → FORCE RLS sahibi
# de bağlar. Böylece tek rol hem migration'ı çalıştırır hem de izole sorgular yapar.
set -e

if [ -z "${APP_DB_PASSWORD}" ]; then
  echo "HATA: APP_DB_PASSWORD ayarlanmamış. Uygulama rolü oluşturulamıyor." >&2
  exit 1
fi

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
      IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'tamircim_app') THEN
        CREATE ROLE tamircim_app LOGIN PASSWORD '${APP_DB_PASSWORD}'
          NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
      END IF;
    END
    \$\$;

    GRANT CONNECT ON DATABASE "$POSTGRES_DB" TO tamircim_app;
    -- Migration'ı bu rol çalıştıracak → public şemasını ona ver (tabloları o oluşturup sahiplenir).
    ALTER SCHEMA public OWNER TO tamircim_app;
    GRANT CREATE, USAGE ON SCHEMA public TO tamircim_app;

    -- Arama performansı için trigram extension'ı (gin_trgm_ops → LIKE '%...%' indexlenir).
    -- Extension oluşturmayı yalnızca superuser/DB-owner yapabilir; uygulama rolünün
    -- (tamircim_app) DB-seviyesi CREATE yetkisi yok. Bu yüzden burada (init, superuser)
    -- oluşturulur. Migration trigram indexlerini yalnızca bu extension VARSA ekler.
    CREATE EXTENSION IF NOT EXISTS pg_trgm;
EOSQL

echo "tamircim_app uygulama rolü oluşturuldu (NOSUPERUSER, NOBYPASSRLS)."
