using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Device
{
    public class DeviceQueryService : IDeviceQueryService
    {
        private readonly ApplicationDbContext _db;

        public DeviceQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<DeviceListDTO>> GetAllAsync(int? customerId = null, string? search = null)
        {
            var query = _db.Devices.Include(d => d.Customer).Include(d => d.RepairRecords).AsQueryable();

            if (customerId.HasValue)
                query = query.Where(d => d.CustomerId == customerId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(d =>
                    d.Brand.Contains(term) ||
                    d.Model.Contains(term) ||
                    (d.SerialNumber != null && d.SerialNumber.Contains(term)));
            }

            var devices = await query
                .OrderByDescending(d => d.ReceivedAt)
                .ToListAsync();

            return devices.Select(d => new DeviceListDTO
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerFullName = d.Customer.FullName,
                DeviceName = d.DeviceName,
                Brand = d.Brand,
                Model = d.Model,
                SerialNumber = d.SerialNumber,
                DeviceType = d.DeviceType,
                DeviceTypeLabel = GetDeviceTypeLabel(d.DeviceType),
                FaultDescription = d.FaultDescription,
                ReceivedAt = d.ReceivedAt,
                IsDelivered = d.IsDelivered,
                DeliveredAt = d.DeliveredAt,
                CurrentStatus = d.RepairRecords
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => GetStatusLabel(r.Status))
                    .FirstOrDefault()
            });
        }

        public async Task<DeviceDTO?> GetByIdAsync(int id)
        {
            var d = await _db.Devices
                .Include(x => x.Customer)
                .Include(x => x.RepairRecords)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return null;

            return new DeviceDTO
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerFullName = d.Customer.FullName,
                DeviceType = d.DeviceType,
                DeviceTypeLabel = GetDeviceTypeLabel(d.DeviceType),
                DeviceName = d.DeviceName,
                Brand = d.Brand,
                Model = d.Model,
                SerialNumber = d.SerialNumber,
                ExtraFields = d.ExtraFields,
                FaultDescription = d.FaultDescription,
                ReceivedAt = d.ReceivedAt,
                DeliveryDate = d.DeliveryDate,
                IsDelivered = d.IsDelivered,
                DeliveredAt = d.DeliveredAt,
                Notes = d.Notes,
                CreatedAt = d.CreatedAt,
                CurrentStatus = d.RepairRecords
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => GetStatusLabel(r.Status))
                    .FirstOrDefault()
            };
        }

        private static string GetDeviceTypeLabel(DeviceType type) => type switch
        {
            DeviceType.WhiteGoods => "Beyaz Eşya",
            DeviceType.Phone => "Telefon",
            DeviceType.Electronics => "Elektronik",
            DeviceType.Other => "Diğer",
            _ => "Bilinmiyor"
        };

        private static string GetStatusLabel(Models.Enums.RepairStatus status) => status switch
        {
            Models.Enums.RepairStatus.Waiting => "Beklemede",
            Models.Enums.RepairStatus.Repaired => "Onarıldı",
            Models.Enums.RepairStatus.NotRepaired => "Onarılmadı",
            _ => "Bilinmiyor"
        };
    }
}
