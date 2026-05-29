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

            var stats = await GetStatsAsync(now);
            var recentCustomers = await GetRecentCustomersAsync();
            var overdueDevices = await GetOverdueDevicesAsync(now);
            var waitingForParts = await GetWaitingForPartsAsync(now);

            return new DashboardResponseDTO
            {
                Stats = stats,
                RecentCustomers = recentCustomers,
                OverdueDevices = overdueDevices,
                WaitingForParts = waitingForParts
            };
        }

        private async Task<DashboardStatsDTO> GetStatsAsync(DateTime now)
        {
            var totalCustomers = await _db.Customers.CountAsync();
            var totalDevices = await _db.Devices.CountAsync();

            var totalWaiting = await _db.Devices
                .Where(d => d.RepairRecords.Any(r => r.Status == RepairStatus.Waiting))
                .CountAsync();

            var sevenDaysAgo = now.AddDays(-7);
            var totalOverdue = await _db.Devices
                .Where(d => !d.IsDelivered
                         && d.RepairRecords.Any(r => !r.IsDeleted
                                                  && r.Status == RepairStatus.Waiting
                                                  && r.CreatedAt < sevenDaysAgo))
                .CountAsync();

            return new DashboardStatsDTO
            {
                TotalCustomers = totalCustomers,
                TotalDevices = totalDevices,
                TotalWaiting = totalWaiting,
                TotalOverdue = totalOverdue
            };
        }

        private async Task<List<DashboardDeviceDTO>> GetRecentCustomersAsync()
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
                DeviceType = x.DeviceTypeInt.HasValue ? DeviceTypeLabel(x.DeviceTypeInt.Value) : "Diğer",
                CreatedAt = x.CreatedAt
            }).ToList();
        }

        private async Task<List<DashboardDeviceDTO>> GetOverdueDevicesAsync(DateTime now)
        {
            var sevenDaysAgo = now.AddDays(-7);

            var waitingGroups = await _db.RepairRecords
                .Where(r => !r.IsDeleted
                         && r.Status == RepairStatus.Waiting
                         && r.CreatedAt < sevenDaysAgo
                         && !r.Device.IsDelivered)
                .GroupBy(r => r.DeviceId)
                .Select(g => new { DeviceId = g.Key, WaitingSince = g.Min(r => r.CreatedAt) })
                .OrderBy(g => g.WaitingSince)
                .Take(5)
                .ToListAsync();

            var deviceIds = waitingGroups.Select(g => g.DeviceId).ToList();

            var devices = await _db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new
                {
                    d.Id,
                    d.CustomerId,
                    CustomerName = d.Customer.FirstName + " " + d.Customer.LastName,
                    Phone = d.Customer.Phone1,
                    d.Brand,
                    d.Model,
                    DeviceTypeInt = (int)d.DeviceType,
                    d.CreatedAt
                })
                .ToListAsync();

            return waitingGroups.Select(g =>
            {
                var d = devices.First(x => x.Id == g.DeviceId);
                return new DashboardDeviceDTO
                {
                    CustomerId = d.CustomerId,
                    CustomerName = d.CustomerName,
                    Phone = d.Phone,
                    DeviceId = d.Id,
                    Brand = d.Brand,
                    Model = d.Model,
                    DeviceType = DeviceTypeLabel(d.DeviceTypeInt),
                    WaitingSince = g.WaitingSince,
                    WaitingDays = (int)(now - g.WaitingSince).TotalDays,
                    CreatedAt = d.CreatedAt
                };
            }).ToList();
        }

        private async Task<List<DashboardDeviceDTO>> GetWaitingForPartsAsync(DateTime now)
        {
            // Aygıt başına en erken Waiting kaydının tarihini bul
            var waitingGroups = await _db.RepairRecords
                .Where(r => r.Status == RepairStatus.Waiting)
                .GroupBy(r => r.DeviceId)
                .Select(g => new { DeviceId = g.Key, WaitingSince = g.Min(r => r.CreatedAt) })
                .OrderBy(g => g.WaitingSince)
                .Take(5)
                .ToListAsync();

            var deviceIds = waitingGroups.Select(g => g.DeviceId).ToList();

            var devices = await _db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => new
                {
                    d.Id,
                    d.CustomerId,
                    CustomerName = d.Customer.FirstName + " " + d.Customer.LastName,
                    Phone = d.Customer.Phone1,
                    d.Brand,
                    d.Model,
                    DeviceTypeInt = (int)d.DeviceType,
                    d.CreatedAt
                })
                .ToListAsync();

            return waitingGroups.Select(g =>
            {
                var d = devices.First(x => x.Id == g.DeviceId);
                return new DashboardDeviceDTO
                {
                    CustomerId = d.CustomerId,
                    CustomerName = d.CustomerName,
                    Phone = d.Phone,
                    DeviceId = d.Id,
                    Brand = d.Brand,
                    Model = d.Model,
                    DeviceType = DeviceTypeLabel(d.DeviceTypeInt),
                    WaitingSince = g.WaitingSince,
                    WaitingDays = (int)(now - g.WaitingSince).TotalDays,
                    CreatedAt = d.CreatedAt
                };
            }).ToList();
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
