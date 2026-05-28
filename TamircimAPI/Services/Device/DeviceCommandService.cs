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

        public DeviceCommandService(ApplicationDbContext db, IDeviceQueryService query)
        {
            _db = db;
            _query = query;
        }

        public async Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto)
        {
            var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId);
            if (!customerExists)
                throw new KeyNotFoundException($"Müşteri bulunamadı: {dto.CustomerId}");

            var device = new Models.Device
            {
                CustomerId = dto.CustomerId,
                DeviceType = dto.DeviceType,
                Brand = dto.Brand.Trim(),
                Model = dto.Model.Trim(),
                SerialNumber = dto.SerialNumber?.Trim(),
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
            });
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(device.Id))!;
        }

        public async Task<DeviceDTO> UpdateAsync(int id, UpdateDeviceDTO dto)
        {
            var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == id)
                ?? throw new KeyNotFoundException($"Cihaz bulunamadı: {id}");

            device.DeviceType = dto.DeviceType;
            device.Brand = dto.Brand.Trim();
            device.Model = dto.Model.Trim();
            device.SerialNumber = dto.SerialNumber?.Trim();
            device.FaultDescription = dto.FaultDescription.Trim();
            device.DeliveryDate = dto.DeliveryDate;
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
    }
}
