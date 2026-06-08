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

            var active = GetOpenDevices(devices);

            var stats = await GetStatsAsync(active, now);
            var recentCustomers = GetRecentCustomers(devices);
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

        // Açık iş = cihazın EN SON kaydı teslim EDİLMEMİŞ (durum fark etmez: Beklemede /
        // Onarıldı / Onarılmadı). Endüstri standardı "açık iş emri" mantığı: iş, müşteri
        // teslim alana kadar açıktır; yaşlanma geliş tarihinden sayılır. Her cihaz tek kez döner.
        private static List<ActiveDevice> GetOpenDevices(List<Models.Device> devices)
        {
            var result = new List<ActiveDevice>();
            foreach (var d in devices)
            {
                var ordered = d.RepairRecords.OrderBy(r => r.ReceivedAt).ToList();
                if (ordered.Count == 0) continue;

                var latest = ordered[^1];
                if (latest.IsDelivered) continue;   // güncel ziyaret kapalı (teslim edildi) → açık değil

                // Açık ziyaretin başlangıcı = son teslim edilen kayıttan sonraki ilk kayıt
                // (hiç teslim yoksa cihazın ilk kaydı). n. gelişte eski ziyaretin tarihi alınmaz.
                var lastDeliveredIdx = ordered.FindLastIndex(r => r.IsDelivered);
                var intake = ordered[lastDeliveredIdx + 1];
                result.Add(new ActiveDevice(d, intake.ReceivedAt, latest.Status));
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
                // Aktif Onarım = ÜZERİNDE ÇALIŞILAN iş → yalnızca güncel durumu Beklemede olanlar.
                TotalWaiting = active.Count(a => a.Status == RepairStatus.Waiting),
                // Gecikmiş = teslim edilmemiş TÜM açık işler (durum fark etmez) 7 günü geçmişse.
                TotalOverdue = active.Count(a => a.WaitingSince < sevenDaysAgo)
            };
        }

        // Son Kayıtlar = MÜŞTERİ başına tek satır, müşterinin EN SON gelişine (RepairRecord) göre.
        // Aynı müşteri başka cihaz ya da aynı cihazı n. kez getirse de tek satır; en son geliş
        // tarihiyle ve o gelişteki cihazla görünür. Kart müşteri detayına gider.
        private static List<DashboardDeviceDTO> GetRecentCustomers(List<Models.Device> devices)
        {
            return devices
                .SelectMany(d => d.RepairRecords.Select(r => new { Device = d, Record = r }))
                .GroupBy(x => x.Device.CustomerId)
                .Select(g => g.OrderByDescending(x => x.Record.ReceivedAt).First())  // müşterinin en son gelişi
                .OrderByDescending(x => x.Record.ReceivedAt)                          // en son gelen müşteri üstte
                .Take(5)
                .Select(x => new DashboardDeviceDTO
                {
                    CustomerId = x.Device.CustomerId,
                    CustomerName = x.Device.Customer.FirstName + " " + x.Device.Customer.LastName,
                    Phone = x.Device.Customer.Phone1,
                    DeviceId = x.Device.Id,
                    Brand = x.Device.Brand,
                    Model = x.Device.Model,
                    DeviceType = DeviceTypeLabel((int)x.Device.DeviceType),
                    CreatedAt = x.Record.ReceivedAt   // kartta gösterilen tarih = en son gelişin tarihi
                })
                .ToList();
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

        // Aktif onarımlar = ÜZERİNDE ÇALIŞILAN işler → yalnızca güncel durumu Beklemede olanlar
        // (Onarıldı/Onarılmadı ama teslim edilmemiş cihazlar buraya GİRMEZ; gerekiyorsa +7 Gün'de
        // görünürler). Her cihaz tek satır, en yeni gelişten eskiye, ilk 5.
        private List<DashboardDeviceDTO> GetActiveRepairs(List<ActiveDevice> active, DateTime now)
        {
            return active
                .Where(a => a.Status == RepairStatus.Waiting)
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

        // Açık (teslim edilmemiş) bir cihaz: bu gelişin başlangıç tarihi ve güncel durumu (en son kayıt).
        private sealed record ActiveDevice(Models.Device Device, DateTime WaitingSince, RepairStatus Status);

        private static string DeviceTypeLabel(int value) => value switch
        {
            0 => "Beyaz Eşya",
            1 => "Telefon",
            2 => "Elektronik",
            _ => "Diğer"
        };
    }
}
