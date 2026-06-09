using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TamircimAPI.Migrations
{
    /// <inheritdoc />
    public partial class SearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P4 — Büyük/küçük harf duyarsız EŞİTLİK aramaları için functional btree index.
            // GetByCodeAsync / CheckSerialAsync 'LOWER(col) = LOWER(@term)' üretir; bu indexler
            // o sorguları tam tarama yerine index'e taşır. Extension gerektirmez → her zaman kurulur.
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Devices_DeviceCode_lower"" ON ""Devices"" (LOWER(""DeviceCode""));");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Devices_SerialNumber_lower"" ON ""Devices"" (LOWER(""SerialNumber""));");

            // P3 — 'LIKE ''%...%''' (içeren) aramaları için trigram GIN index.
            // pg_trgm extension'ı yalnızca superuser/DB-owner oluşturabildiğinden (uygulama rolü
            // değil), bu indexler YALNIZCA extension mevcutsa kurulur. Extension yoksa sessizce
            // atlanır → migration patlamaz, uygulama açılır (arama eski davranışla çalışır).
            // Extension db-init (superuser) tarafından oluşturulur; mevcut bir DB'de yoksa bir kez
            // 'CREATE EXTENSION pg_trgm;' (superuser) çalıştırıp bu migration tekrar uygulanabilir.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Devices_DeviceCode_trgm"" ON ""Devices"" USING gin (""DeviceCode"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Devices_Brand_trgm"" ON ""Devices"" USING gin (""Brand"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Devices_Model_trgm"" ON ""Devices"" USING gin (""Model"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Devices_SerialNumber_trgm"" ON ""Devices"" USING gin (""SerialNumber"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Customers_Phone1_trgm"" ON ""Customers"" USING gin (""Phone1"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Customers_NationalId_trgm"" ON ""Customers"" USING gin (""NationalId"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Customers_Email_trgm"" ON ""Customers"" USING gin (""Email"" gin_trgm_ops)';
                        EXECUTE 'CREATE INDEX IF NOT EXISTS ""IX_Customers_Name_trgm"" ON ""Customers"" USING gin (turkish_lower(""FirstName"" || '' '' || ""LastName"") gin_trgm_ops)';
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_DeviceCode_lower"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_SerialNumber_lower"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_DeviceCode_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_Brand_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_Model_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Devices_SerialNumber_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_Phone1_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_NationalId_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_Email_trgm"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_Name_trgm"";");
        }
    }
}
