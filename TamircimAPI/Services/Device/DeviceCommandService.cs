using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Common;

namespace TamircimAPI.Services.Device
{
    public class DeviceCommandService : IDeviceCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDeviceQueryService _query;
        private readonly ICodeGenerator _codes;
        private readonly IHttpContextAccessor _http;

        public DeviceCommandService(
            ApplicationDbContext db,
            IDeviceQueryService query,
            ICodeGenerator codes,
            IHttpContextAccessor http)
        {
            _db = db;
            _query = query;
            _codes = codes;
            _http = http;
        }

        // Yeni fiziksel cihaz + ilk servis kaydını (geliş) birlikte oluşturur.
        public async Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto)
        {
            var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId);
            if (!customerExists)
                throw new KeyNotFoundException($"Müşteri bulunamadı: {dto.CustomerId}");

            var deviceType = await ResolveDeviceTypeFromUserBranchAsync();

            var device = new Models.Device
            {
                CustomerId = dto.CustomerId,
                DeviceCode = await _codes.NextDeviceCodeAsync(),
                DeviceType = deviceType,
                DeviceName = dto.DeviceName.Trim(),
                Brand = dto.Brand.Trim(),
                Model = dto.Model.Trim(),
                SerialNumber = string.IsNullOrWhiteSpace(dto.SerialNumber) ? null : dto.SerialNumber.Trim(),
                ExtraFields = dto.ExtraFields,
                Notes = dto.Notes?.Trim()
            };

            _db.Devices.Add(device);
            await _db.SaveChangesAsync();

            _db.RepairRecords.Add(new Models.RepairRecord
            {
                DeviceId = device.Id,
                TicketNo = await _codes.NextTicketNoAsync(),
                FaultDescription = dto.FaultDescription.Trim(),
                ReceivedAt = dto.ReceivedAt ?? DateTime.UtcNow,
                DeliveryDate = dto.DeliveryDate,
                Status = RepairStatus.Waiting,
                WaitingReason = dto.InitialWaitingReason?.Trim(),
                Notes = dto.InitialRepairNotes?.Trim(),
            });
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(device.Id))!;
        }

        // Yalnızca cihazın kalıcı (varlık) bilgilerini günceller.
        public async Task<DeviceDTO> UpdateAsync(int id, UpdateDeviceDTO dto)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            device.DeviceName = dto.DeviceName.Trim();
            device.Brand = dto.Brand.Trim();
            device.Model = dto.Model.Trim();
            device.SerialNumber = string.IsNullOrWhiteSpace(dto.SerialNumber) ? null : dto.SerialNumber.Trim();
            device.ExtraFields = dto.ExtraFields;
            device.Notes = dto.Notes?.Trim();

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
