namespace TamircimAPI.Services.Tenant
{
    public class TenantContext : ITenantContext
    {
        public int? TenantId { get; private set; }
        public bool HasTenant => TenantId.HasValue;
        public bool IsBypass { get; private set; }

        public void SetTenant(int tenantId)
        {
            if (IsBypass)
                throw new InvalidOperationException("Bypass modunda tenant ayarlanamaz.");
            // Tek sefer set edilir; sonradan değiştirilmesi bir programlama hatasıdır.
            if (TenantId.HasValue && TenantId.Value != tenantId)
                throw new InvalidOperationException("Tenant context bir istek içinde değiştirilemez.");
            TenantId = tenantId;
        }

        public void SetBypass()
        {
            if (TenantId.HasValue)
                throw new InvalidOperationException("Tenant ayarlanmışken bypass'a geçilemez.");
            IsBypass = true;
        }
    }
}
