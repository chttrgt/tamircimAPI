using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Device
{
    public class DeviceCommandService : IDeviceCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDeviceQueryService _query;
        private readonly IHttpContextAccessor _http;

        public DeviceCommandService(ApplicationDbContext db, IDeviceQueryService query, IHttpContextAccessor http)
        {
            _db = db;
            _query = query;
            _http = http;
        }

        public async Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto)
        {
            var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId);
            if (!customerExists)
                throw new KeyNotFoundException($"Müşteri bulunamadı: {dto.CustomerId}");

            var deviceType = await ResolveDeviceTypeFromUserBranchAsync();

            var device = new Models.Device
            {
                CustomerId = dto.CustomerId,
                DeviceType = deviceType,
                DeviceName = dto.DeviceName.Trim(),
                Brand = dto.Brand.Trim(),
                Model = dto.Model.Trim(),
                SerialNumber = dto.SerialNumber?.Trim(),
                ExtraFields = dto.ExtraFields,
                FaultDescription = dto.FaultDescription.Trim(),
                ReceivedAt = dto.ReceivedAt ?? DateTime.UtcNow,
                DeliveryDate = dto.DeliveryDate,
                Notes = dto.Notes?.Trim()
            };

            _db.Devices.Add(device);
            await _db.SaveChangesAsync();

            _db.RepairRecords.Add(new Models.RepairRecord
            {
                DeviceId = device.Id,
                Status = RepairStatus.Waiting,
                WaitingReason = dto.InitialWaitingReason?.Trim(),
                Notes = dto.InitialRepairNotes?.Trim(),
            });
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(device.Id))!;
        }

        public async Task<DeviceDTO> UpdateAsync(int id, UpdateDeviceDTO dto)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            device.DeviceName = dto.DeviceName.Trim();
            device.Brand = dto.Brand.Trim();
            device.Model = dto.Model.Trim();
            device.SerialNumber = dto.SerialNumber?.Trim();
            device.ExtraFields = dto.ExtraFields;
            device.FaultDescription = dto.FaultDescription.Trim();
            device.DeliveryDate = dto.DeliveryDate;
            if (device.IsDelivered && dto.DeliveryDate.HasValue)
                device.DeliveredAt = dto.DeliveryDate;
            device.Notes = dto.Notes?.Trim();

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task<DeviceDTO> MarkDeliveredAsync(int id, DateTime? deliveredAt = null)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            var ts = deliveredAt ?? DateTime.UtcNow;
            device.IsDelivered = true;
            device.DeliveredAt = ts;
            device.DeliveryDate = ts;

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task<DeviceDTO> UndoDeliveryAsync(int id)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            device.IsDelivered = false;
            device.DeliveredAt = null;
            device.DeliveryDate = null;

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task DeleteAsync(int id)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            device.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        private async Task<DeviceType> ResolveDeviceTypeFromUserBranchAsync()
        {
            var userIdStr = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return DeviceType.Other;

            var branch = await _db.Users
                .Where(u => u.Id == userId)
                .Select(u => u.Branch)
                .FirstOrDefaultAsync();

            return branch switch
            {
                "Beyaz Eşya" => DeviceType.WhiteGoods,
                "Telefon"    => DeviceType.Phone,
                "Elektronik" => DeviceType.Electronics,
                _            => DeviceType.Other,
            };
        }
    }
}
