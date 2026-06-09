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

            // "Güncel durum = en son servis kaydı" mantığı C#'ta hesaplandığından, TÜM
            // cihazları yüklemek yerine önce SQL'de YALNIZCA açık cihazlara (en son kaydı
            // teslim EDİLMEMİŞ) indiriyoruz → açık-iş kümesi ≪ tüm cihazlar, ölçeklenir.
            // Ziyaret mantığı (GetOpenDevices) bu küçük kümede aynen uygulanır. Toplam
            // sayımlar SQL COUNT ile, son kayıtlar ayrı verimli sorguyla alınır.
            var openDevices = await _db.Devices
                .Where(d => d.RepairRecords.Any()
                    && d.RepairRecords.OrderByDescending(r => r.ReceivedAt)
                           .Select(r => r.IsDelivered).FirstOrDefault() == false)
                .Include(d => d.RepairRecords)
                .Include(d => d.Customer)
                .ToListAsync();

            var active = GetOpenDevices(openDevices);

            var stats = await GetStatsAsync(active, now);
            var recentCustomers = await GetRecentCustomersAsync();
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
        // Tüm cihazları belleğe almadan: (1) DB'de müşteri başına max(ReceivedAt) ile en yeni 5
        // müşteriyi bul, (2) yalnızca bu 5 müşterinin kayıtlarını çekip en son gelişlerini al.
        private async Task<List<DashboardDeviceDTO>> GetRecentCustomersAsync()
        {
            var topCustomers = await _db.RepairRecords
                .GroupBy(r => r.Device.CustomerId)
                .Select(g => new { CustomerId = g.Key, LastAt = g.Max(r => r.ReceivedAt) })
                .OrderByDescending(x => x.LastAt)
                .Take(5)
                .ToListAsync();

            if (topCustomers.Count == 0) return new List<DashboardDeviceDTO>();

            var ids = topCustomers.Select(x => x.CustomerId).ToList();

            // Yalnızca bu 5 müşterinin kayıtları (küçük küme) — her birinin en son gelişi.
            var records = await _db.RepairRecords
                .Where(r => ids.Contains(r.Device.CustomerId))
                .Include(r => r.Device)
                    .ThenInclude(d => d.Customer)
                .ToListAsync();

            return topCustomers
                .Select(tc => records
                    .Where(r => r.Device.CustomerId == tc.CustomerId)
                    .OrderByDescending(r => r.ReceivedAt)
                    .First())
                .Select(r => new DashboardDeviceDTO
                {
                    CustomerId = r.Device.CustomerId,
                    CustomerName = r.Device.Customer.FirstName + " " + r.Device.Customer.LastName,
                    Phone = r.Device.Customer.Phone1,
                    DeviceId = r.Device.Id,
                    Brand = r.Device.Brand,
                    Model = r.Device.Model,
                    DeviceType = DeviceTypeLabel((int)r.Device.DeviceType),
                    CreatedAt = r.ReceivedAt   // kartta gösterilen tarih = en son gelişin tarihi
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
