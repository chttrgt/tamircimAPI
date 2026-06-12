using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Services.Storage;

namespace TamircimAPI.Services.Account
{
    // Hesap silmenin "yürütme" ayağı. Periyodik çalışır; grace süresi (DeletionScheduledAt)
    // dolmuş tenant'ları ve TÜM verisini KALICI siler (diskteki fotoğraflar dahil).
    // Soft-delete'i baypas etmek için ExecuteDeleteAsync (raw SQL DELETE) kullanır; RLS'i
    // güvenilir bypass + açık TenantId filtresiyle aşar (PhotoCleanupService deseni).
    public class AccountDeletionService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AccountDeletionService> _logger;
        // TEST: kalıcı silmeyi hızlı görmek için 15 sn. Geri al → TimeSpan.FromHours(6).
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

        public AccountDeletionService(IServiceProvider services, ILogger<AccountDeletionService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Hesap silme görevi başarısız oldu.");
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            // RLS bypass'ı DbContext ilk kullanılmadan (bağlantı açılmadan) ÖNCE etkinleştir,
            // ki interceptor app.tenant_id=-1 yazsın → tüm tenant'ların satırlarına erişilebilir.
            // Yanlışlıkla başka tenant silinmesin diye HER sorgu açık TenantId ile filtrelenir.
            var tenantCtx = scope.ServiceProvider.GetRequiredService<TamircimAPI.Services.Tenant.ITenantContext>();
            tenantCtx.SetBypass();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();

            var now = DateTime.UtcNow;
            var dueTenantIds = await db.Tenants
                .Where(t => t.DeletionScheduledAt != null && t.DeletionScheduledAt <= now)
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (dueTenantIds.Count == 0) return;

            foreach (var tenantId in dueTenantIds)
            {
                try
                {
                    await DeleteTenantAsync(db, storage, tenantId, ct);
                    _logger.LogWarning("Tenant {TenantId} ve tüm verisi kalıcı olarak silindi.", tenantId);
                }
                catch (Exception ex)
                {
                    // Bir tenant başarısız olsa diğerleri etkilenmesin; sonraki turda tekrar denenir.
                    _logger.LogError(ex, "Tenant {TenantId} silinemedi.", tenantId);
                }
            }
        }

        // FK güvenli sıra (hepsi Restrict): Payment → RepairRecord/DevicePhoto → Device → Customer.
        // En son Tenant silinir → Users ve onların token/izinleri DB cascade ile gider.
        private static async Task DeleteTenantAsync(
            ApplicationDbContext db, IPhotoStorage storage, int tenantId, CancellationToken ct)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Diskteki fotoğraf dosyalarını sil (DB satırları aşağıda kaldırılacak).
            var photos = await db.DevicePhotos
                .IgnoreQueryFilters()
                .Where(p => p.TenantId == tenantId)
                .Select(p => new { p.DeviceId, p.FileName, p.ThumbnailFileName })
                .ToListAsync(ct);
            foreach (var p in photos)
            {
                try { storage.Delete(p.DeviceId, p.FileName); } catch { /* yoksay */ }
                try { storage.Delete(p.DeviceId, p.ThumbnailFileName); } catch { /* yoksay */ }
            }

            // Hard-delete (ExecuteDelete → soft-delete interceptor'ını baypas eder). Açık TenantId
            // filtresi + IgnoreQueryFilters → soft-deleted satırlar dahil, yalnızca bu tenant.
            await db.Payments.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await db.DevicePhotos.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await db.RepairRecords.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await db.Devices.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await db.Customers.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);
            await db.AuditLogs.IgnoreQueryFilters().Where(e => e.TenantId == tenantId).ExecuteDeleteAsync(ct);

            // Tenant'ı sil → User → RefreshToken/UserPermission/EmailVerification/2FA DB cascade.
            await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync(ct);

            await tx.CommitAsync(ct);
        }
    }
}
