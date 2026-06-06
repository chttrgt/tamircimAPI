using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Services.Tenant;

namespace TamircimAPI.Services.Common
{
    public class CodeGenerator : ICodeGenerator
    {
        private readonly ApplicationDbContext _db;
        private readonly ITenantContext _tenant;

        public CodeGenerator(ApplicationDbContext db, ITenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        // Per-tenant numaralandırma: her tenant kendi serisini 1'den görür.
        // Atomik UPDATE ... RETURNING satır kilidi alır → aynı tenant'ta eşzamanlı
        // istekler çakışmadan sıralı numara alır; farklı tenant'lar farklı satır
        // olduğundan birbirini kilitlemez. {tid} interpolasyonu EF tarafından
        // parametreleştirilir (enjeksiyon yok).
        public async Task<string> NextDeviceCodeAsync()
        {
            var tid = RequireTenant();
            // UPDATE ... RETURNING non-composable'dır → FirstAsync() (Take/subquery)
            // ile sarılamaz. ToListAsync ham SQL'i olduğu gibi çalıştırır, tek satır döner.
            var rows = await _db.Database
                .SqlQuery<long>($"UPDATE \"Tenants\" SET \"NextDeviceSeq\" = \"NextDeviceSeq\" + 1 WHERE \"Id\" = {tid} RETURNING \"NextDeviceSeq\" - 1 AS \"Value\"")
                .ToListAsync();
            return $"CHZ-{rows[0]:D6}";
        }

        public async Task<string> NextTicketNoAsync()
        {
            var tid = RequireTenant();
            var rows = await _db.Database
                .SqlQuery<long>($"UPDATE \"Tenants\" SET \"NextTicketSeq\" = \"NextTicketSeq\" + 1 WHERE \"Id\" = {tid} RETURNING \"NextTicketSeq\" - 1 AS \"Value\"")
                .ToListAsync();
            return $"{DateTime.UtcNow:yy}-{rows[0]:D6}";
        }

        private int RequireTenant() =>
            _tenant.TenantId ?? throw new InvalidOperationException(
                "Kod üretimi için tenant bağlamı gerekli.");
    }
}
