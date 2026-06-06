using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;

namespace TamircimAPI.Services.Storage
{
    // Hibrit silmenin "garbage collection" ayağı.
    // Günde bir kez çalışır; retention süresi (varsayılan 30 gün) dolmuş soft-deleted
    // fotoğrafları diskten ve veritabanından KALICI siler.
    // Kullanıcı tarafında sıfır etki — arka planda, indexli sorgu.
    public class PhotoCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<PhotoCleanupService> _logger;
        private readonly int _retentionDays;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        public PhotoCleanupService(
            IServiceProvider services,
            IConfiguration configuration,
            ILogger<PhotoCleanupService> logger)
        {
            _services = services;
            _logger = logger;
            _retentionDays = int.TryParse(configuration["Storage:PhotoRetentionDays"], out var d) ? d : 30;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Açılışta hemen bir tur, sonra her 24 saatte bir.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCleanupAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Fotoğraf temizleme görevi başarısız oldu.");
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

        private async Task RunCleanupAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            // Arka plan GC tüm tenant'lar üzerinde çalışır. RLS bunu DB seviyesinde
            // engellemesin diye güvenilir bypass'ı etkinleştir (DbContext oluşturulmadan
            // ÖNCE, ki interceptor app.tenant_id=-1 yazsın). İstek akışından ulaşılamaz.
            var tenant = scope.ServiceProvider.GetRequiredService<TamircimAPI.Services.Tenant.ITenantContext>();
            tenant.SetBypass();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IPhotoStorage>();

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

            // Query filter hem soft-deleted'ı hem tenant'ı gizler → IgnoreQueryFilters
            // (RLS bypass'ı yukarıda ayrıca sağlandı).
            var expired = await db.DevicePhotos
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted && p.DeletedAt != null && p.DeletedAt < cutoff)
                .ToListAsync(ct);

            if (expired.Count == 0) return;

            foreach (var photo in expired)
            {
                // Önce diskten (ana + thumbnail), hata olsa bile DB satırını sil
                try { storage.Delete(photo.DeviceId, photo.FileName); } catch { /* yoksay */ }
                try { storage.Delete(photo.DeviceId, photo.ThumbnailFileName); } catch { /* yoksay */ }
            }

            db.DevicePhotos.RemoveRange(expired);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("{Count} süresi dolmuş fotoğraf kalıcı olarak silindi.", expired.Count);
        }
    }
}
