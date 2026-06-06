namespace TamircimAPI.Services.Tenant
{
    // İstek başına (scoped) geçerli tenant kimliği. Yalnızca imzalı JWT'deki
    // tenant_id claim'inden doldurulur (UserEnrichmentMiddleware). İstemci girdisinden
    // ASLA türetilmez. Auth öncesi akışlarda (login/register/refresh) TenantId null'dır.
    public interface ITenantContext
    {
        int? TenantId { get; }
        bool HasTenant { get; }

        // RLS bypass'ı: yalnızca güvenilir SUNUCU-İÇİ kod (arka plan GC gibi) tüm
        // tenant'lar üzerinde işlem yaparken kullanır. İstek akışından (middleware)
        // ASLA çağrılmaz → dışarıdan ulaşılamaz. true iken interceptor app.tenant_id'yi
        // -1 (bypass sentinel'i) yapar ve RLS politikaları tüm satırlara izin verir.
        bool IsBypass { get; }

        void SetTenant(int tenantId);
        void SetBypass();
    }
}
