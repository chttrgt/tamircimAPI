using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Dashboard;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DashboardService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<DashboardResponseDTO> GetDashboardAsync()
        {
            var now = DateTime.UtcNow;

            // Cihazı olmayan müşterilerde varsayılan tür, giriş yapan kullanıcının branşı olsun.
            var defaultDeviceType = await GetCurrentUserBranchAsync();

            // Cihazın GÜNCEL durumu = EN SON servis kaydı (DeviceQueryService ile aynı mantık).
            // Aktif/bekleyen iş = en son kaydı teslim edilmemiş ve "Beklemede" olan CİHAZ.
            // Böylece bir cihaz, birden çok Waiting kaydı olsa bile tek kez sayılır/listelenir.
            var devices = await _db.Devices
                .Include(d => d.RepairRecords)
                .Include(d => d.Customer)
                .ToListAsync();

            var active = GetActiveWaitingDevices(devices);

            var stats = await GetStatsAsync(active, now);
            var recentCustomers = await GetRecentCustomersAsync(defaultDeviceType);
            var overdueDevices = GetOverdueDevices(active, now);
            var waitingForParts = GetActiveRepairs(active, now);

            return new DashboardResponseDTO
            {
                Stats = stats,
                RecentCustomers = recentCustomers,
                OverdueDevices = overdueDevices,
                WaitingForParts = waitingForParts
            };
        }

        // Açıkta bekleyen iş = cihazın AÇIK ziyaretinin (son teslimden sonraki kayıtlar)
        // güncel durumu "Beklemede". Her cihaz en fazla bir kez döner.
        private static List<ActiveDevice> GetActiveWaitingDevices(List<Models.Device> devices)
        {
            var result = new List<ActiveDevice>();
            foreach (var d in devices)
            {
                var ordered = d.RepairRecords.OrderBy(r => r.ReceivedAt).ToList();
                if (ordered.Count == 0) continue;

                var latest = ordered[^1];
                if (latest.IsDelivered) continue;                       // güncel ziyaret kapalı (teslim edildi)
                if (latest.Status != RepairStatus.Waiting) continue;    // güncel durum bekleme değil

                // Açık ziyaretin başlangıcı = son teslim edilen kayıttan sonraki ilk kayıt
                // (hiç teslim yoksa cihazın ilk kaydı). n. gelişte eski ziyaretin tarihi alınmaz.
                var lastDeliveredIdx = ordered.FindLastIndex(r => r.IsDelivered);
                var intake = ordered[lastDeliveredIdx + 1];
                result.Add(new ActiveDevice(d, intake.ReceivedAt));
            }
            return result;
        }

        private async Task<DashboardStatsDTO> GetStatsAsync(List<ActiveDevice> active, DateTime now)
        {
            var totalCustomers = await _db.Customers.CountAsync();
            var totalDevices = await _db.Devices.CountAsync();

            var sevenDaysAgo = now.AddDays(-7);

            return new DashboardStatsDTO
            {
                TotalCustomers = totalCustomers,
                TotalDevices = totalDevices,
                // Kayıt değil CİHAZ sayılır → aynı cihaz birden çok Waiting kaydıyla şişmez.
                TotalWaiting = active.Count,
                TotalOverdue = active.Count(a => a.WaitingSince < sevenDaysAgo)
            };
        }

        private async Task<List<DashboardDeviceDTO>> GetRecentCustomersAsync(string defaultDeviceType)
        {
            var raw = await _db.Customers
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new
                {
                    c.Id,
                    CustomerName = c.FirstName + " " + c.LastName,
                    c.Phone1,
                    DeviceId = c.Devices.OrderByDescending(d => d.CreatedAt).Select(d => (int?)d.Id).FirstOrDefault(),
                    Brand = c.Devices.OrderByDescending(d => d.CreatedAt).Select(d => d.Brand).FirstOrDefault() ?? "-",
                    Model = c.Devices.OrderByDescending(d => d.CreatedAt).Select(d => d.Model).FirstOrDefault() ?? "-",
                    DeviceTypeInt = c.Devices.OrderByDescending(d => d.CreatedAt).Select(d => (int?)d.DeviceType).FirstOrDefault(),
                    c.CreatedAt
                })
                .ToListAsync();

            return raw.Select(x => new DashboardDeviceDTO
            {
                CustomerId = x.Id,
                CustomerName = x.CustomerName,
                Phone = x.Phone1,
                DeviceId = x.DeviceId ?? 0,
                Brand = x.Brand,
                Model = x.Model,
                DeviceType = x.DeviceTypeInt.HasValue ? DeviceTypeLabel(x.DeviceTypeInt.Value) : defaultDeviceType,
                CreatedAt = x.CreatedAt
            }).ToList();
        }

        // En uzun süredir bekleyen (7+ gün) açık işler — her cihaz tek satır.
        private List<DashboardDeviceDTO> GetOverdueDevices(List<ActiveDevice> active, DateTime now)
        {
            var sevenDaysAgo = now.AddDays(-7);
            return active
                .Where(a => a.WaitingSince < sevenDaysAgo)
                .OrderBy(a => a.WaitingSince)
                .Take(5)
                .Select(a => MapActiveDevice(a, now))
                .ToList();
        }

        // Aktif onarımlar (açık + beklemede) — her cihaz tek satır, en yeni gelişten eskiye.
        private List<DashboardDeviceDTO> GetActiveRepairs(List<ActiveDevice> active, DateTime now)
        {
            return active
                .OrderByDescending(a => a.WaitingSince)
                .Take(5)
                .Select(a => MapActiveDevice(a, now))
                .ToList();
        }

        private static DashboardDeviceDTO MapActiveDevice(ActiveDevice a, DateTime now)
        {
            var d = a.Device;
            return new DashboardDeviceDTO
            {
                CustomerId = d.CustomerId,
                CustomerName = d.Customer.FirstName + " " + d.Customer.LastName,
                Phone = d.Customer.Phone1,
                DeviceId = d.Id,
                Brand = d.Brand,
                Model = d.Model,
                DeviceType = DeviceTypeLabel((int)d.DeviceType),
                WaitingSince = a.WaitingSince,
                WaitingDays = (int)(now - a.WaitingSince).TotalDays,
                CreatedAt = d.CreatedAt
            };
        }

        // Açık (teslim edilmemiş, güncel durumu Beklemede) bir cihaz ve bu gelişin başlangıç tarihi.
        private sealed record ActiveDevice(Models.Device Device, DateTime WaitingSince);

        private async Task<string> GetCurrentUserBranchAsync()
        {
            var userIdStr = _httpContextAccessor.HttpContext?.Items["UserId"] as string;
            if (!int.TryParse(userIdStr, out var userId))
                return "Diğer";

            // Branch artık tenant düzeyinde. Kullanıcı mevcut tenant'ta (filtre uygular).
            var branch = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Tenant.Branch)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(branch) ? "Diğer" : branch;
        }

        private static string DeviceTypeLabel(int value) => value switch
        {
            0 => "Beyaz Eşya",
            1 => "Telefon",
            2 => "Elektronik",
            _ => "Diğer"
        };
    }
}
