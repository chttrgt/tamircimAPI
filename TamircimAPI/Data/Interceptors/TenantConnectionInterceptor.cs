using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TamircimAPI.Services.Tenant;

namespace TamircimAPI.Data.Interceptors
{
    // PostgreSQL Row-Level Security'nin (RLS) ikinci güvenlik katmanı.
    // Her fiziksel bağlantı açılışında oturum değişkeni app.tenant_id'yi geçerli
    // tenant'a ayarlar. RLS politikaları bu değeri current_setting('app.tenant_id')
    // ile okuyup başka tenant'ın satırlarını DB seviyesinde gizler/engeller — uygulama
    // katmanı (EF filtresi) bypass edilse/bug olsa bile.
    //
    // Bağlantı havuzunda fiziksel bağlantılar yeniden kullanılır; ama bu interceptor
    // HER açılışta değeri taze yazdığı için stale (eski tenant) değer kalmaz.
    // Tenant yoksa (login/register/refresh/migration) 0 yazılır → hiçbir gerçek
    // tenant'ın id'si 0 olmadığından iş-verisi tablolarından satır dönmez (güvenli).
    public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
    {
        private readonly ITenantContext _tenant;

        public TenantConnectionInterceptor(ITenantContext tenant) => _tenant = tenant;

        // -1 = bypass (güvenilir arka plan kodu), >0 = tenant, 0 = bağlam yok (satır dönmez).
        private int ResolveValue() => _tenant.IsBypass ? -1 : (_tenant.TenantId ?? 0);

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            using var cmd = connection.CreateCommand();
            // Değer int (JWT'den parse edilmiş veya 0) — string enjeksiyonu mümkün değil.
            cmd.CommandText = $"SET app.tenant_id = {ResolveValue()}";
            cmd.ExecuteNonQuery();
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET app.tenant_id = {ResolveValue()}";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
