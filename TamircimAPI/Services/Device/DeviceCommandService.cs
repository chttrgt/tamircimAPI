using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Common;
using TamircimAPI.Services.Tenant;

namespace TamircimAPI.Services.Device
{
    public class DeviceCommandService : IDeviceCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IDeviceQueryService _query;
        private readonly ICodeGenerator _codes;
        private readonly ITenantContext _tenant;

        public DeviceCommandService(
            ApplicationDbContext db,
            IDeviceQueryService query,
            ICodeGenerator codes,
            ITenantContext tenant)
        {
            _db = db;
            _query = query;
            _codes = codes;
            _tenant = tenant;
        }

        // Yeni fiziksel cihaz + ilk servis kaydını (geliş) birlikte oluşturur.
        // İki kayıt tek bir transaction'da yazılır: cihaz oluşup servis kaydı
        // yazılamazsa hiçbiri kalıcı olmaz (atomiklik) — yetim cihaz oluşmaz.
        // EnableRetryOnFailure açık olduğundan manuel transaction execution
        // strategy ile sarılmalıdır (aksi halde EF Core hata fırlatır).
        public async Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto)
        {
            var customerExists = await _db.Customers.AnyAsync(c => c.Id == dto.CustomerId);
            if (!customerExists)
                throw new KeyNotFoundException($"Müşteri bulunamadı: {dto.CustomerId}");

            var deviceType = await ResolveDeviceTypeFromTenantBranchAsync();

            var strategy = _db.Database.CreateExecutionStrategy();
            var deviceId = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

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

                await tx.CommitAsync();
                return device.Id;
            });

            return (await _query.GetByIdAsync(deviceId))!;
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

        // Cihaz tipi, tenant'ın (dükkânın) branch'inden türetilir → dükkân genelinde tutarlı.
        private async Task<DeviceType> ResolveDeviceTypeFromTenantBranchAsync()
        {
            var tid = _tenant.TenantId;
            if (tid == null)
                return DeviceType.Other;

            var branch = await _db.Tenants
                .Where(t => t.Id == tid.Value)
                .Select(t => t.Branch)
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
