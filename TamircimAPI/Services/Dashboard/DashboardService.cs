using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Dashboard;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardResponseDTO> GetDashboardAsync()
        {
            var now = DateTime.UtcNow;

            // Cihazın GÜNCEL durumu = EN SON servis kaydı (DeviceQueryService ile aynı mantık).
            // Aktif/bekleyen iş = en son kaydı teslim edilmemiş ve "Beklemede" olan CİHAZ.
            // Böylece bir cihaz, birden çok Waiting kaydı olsa bile tek kez sayılır/listelenir.
            var devices = await _db.Devices
                .Include(d => d.RepairRecords)
                .Include(d => d.Customer)
                .ToListAsync();

            var active = GetActiveWaitingDevices(devices);

            var stats = await GetStatsAsync(active, now);
            var recentCustomers = await GetRecentRecordsAsync();
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

        // Son Kayıtlar = en son GELİŞLER (RepairRecord), ReceivedAt'e göre. Her geliş bir satır;
        // aynı cihaz teslim sonrası n. kez geldiyse her gelişi kendi tarihiyle ayrı görünür.
        private async Task<List<DashboardDeviceDTO>> GetRecentRecordsAsync()
        {
            var raw = await _db.RepairRecords
                .OrderByDescending(r => r.ReceivedAt)
                .Take(5)
                .Select(r => new
                {
                    r.Device.CustomerId,
                    CustomerName = r.Device.Customer.FirstName + " " + r.Device.Customer.LastName,
                    Phone = r.Device.Customer.Phone1,
                    r.DeviceId,
                    r.Device.Brand,
                    r.Device.Model,
                    DeviceTypeInt = (int)r.Device.DeviceType,
                    r.ReceivedAt
                })
                .ToListAsync();

            return raw.Select(x => new DashboardDeviceDTO
            {
                CustomerId = x.CustomerId,
                CustomerName = x.CustomerName,
                Phone = x.Phone,
                DeviceId = x.DeviceId,
                Brand = x.Brand,
                Model = x.Model,
                DeviceType = DeviceTypeLabel(x.DeviceTypeInt),
                CreatedAt = x.ReceivedAt   // kartta gösterilen tarih = gelişin tarihi
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

        private static string DeviceTypeLabel(int value) => value switch
        {
            0 => "Beyaz Eşya",
            1 => "Telefon",
            2 => "Elektronik",
            _ => "Diğer"
        };
    }
}
