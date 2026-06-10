using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Common;

namespace TamircimAPI.Services.Repair
{
    public class RepairCommandService : IRepairCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepairQueryService _query;
        private readonly ICodeGenerator _codes;

        public RepairCommandService(ApplicationDbContext db, IRepairQueryService query, ICodeGenerator codes)
        {
            _db = db;
            _query = query;
            _codes = codes;
        }

        // Var olan bir cihaza yeni servis kaydı (geliş) açar.
        public async Task<RepairRecordDTO> CreateAsync(CreateRepairRecordDTO dto)
        {
            var deviceExists = await _db.Devices.AnyAsync(d => d.Id == dto.DeviceId);
            if (!deviceExists)
                throw new KeyNotFoundException($"Cihaz bulunamadı: {dto.DeviceId}");

            ValidateStatusFields(dto.Status, dto.WorkDone, dto.NotRepairedReason, dto.WaitingReason);

            var record = new RepairRecord
            {
                DeviceId = dto.DeviceId,
                TicketNo = await _codes.NextTicketNoAsync(),
                FaultDescription = dto.FaultDescription.Trim(),
                ReceivedAt = dto.ReceivedAt ?? DateTime.UtcNow,
                DeliveryDate = dto.DeliveryDate,
                Status = dto.Status,
                WorkDone = dto.WorkDone?.Trim(),
                NotRepairedReason = dto.NotRepairedReason?.Trim(),
                WaitingReason = dto.WaitingReason?.Trim(),
                CompletedAt = dto.CompletedAt,
                Notes = dto.Notes?.Trim(),
                Price = dto.Price
            };

            _db.RepairRecords.Add(record);
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(record.Id))!;
        }

        public async Task<RepairRecordDTO> UpdateAsync(int id, UpdateRepairRecordDTO dto)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Servis kaydı bulunamadı: {id}");

            ValidateStatusFields(dto.Status, dto.WorkDone, dto.NotRepairedReason, dto.WaitingReason);

            if (!string.IsNullOrWhiteSpace(dto.FaultDescription))
                record.FaultDescription = dto.FaultDescription.Trim();
            if (dto.ReceivedAt.HasValue)
                record.ReceivedAt = dto.ReceivedAt.Value;
            record.DeliveryDate = dto.DeliveryDate;
            record.Status = dto.Status;
            record.WorkDone = dto.WorkDone?.Trim();
            record.NotRepairedReason = dto.NotRepairedReason?.Trim();
            record.WaitingReason = dto.WaitingReason?.Trim();
            record.CompletedAt = dto.CompletedAt;
            record.Notes = dto.Notes?.Trim();
            record.Price = dto.Price;

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        // Yalnızca anlaşılan ücreti günceller. Servis kaydının diğer alanlarını (durum,
        // yapılan iş vb.) yeniden göndermeye gerek bırakmaz → tahsilat ekranından hızlı
        // fiyat girişi için. Ödeme durumu/kalan zaten okuma anında Price'tan türetilir.
        public async Task<RepairRecordDTO> SetPriceAsync(int id, decimal? price)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Servis kaydı bulunamadı: {id}");

            record.Price = price;
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task<RepairRecordDTO> MarkDeliveredAsync(int id, DateTime? deliveredAt = null)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Servis kaydı bulunamadı: {id}");

            if (record.Status == RepairStatus.Waiting)
                throw new ArgumentException("Beklemedeki bir kayıt teslim edilemez. Önce durumu güncelleyin.");

            var ts = deliveredAt ?? DateTime.UtcNow;
            record.IsDelivered = true;
            record.DeliveredAt = ts;
            record.DeliveryDate = ts;

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task<RepairRecordDTO> UndoDeliveryAsync(int id)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Servis kaydı bulunamadı: {id}");

            record.IsDelivered = false;
            record.DeliveredAt = null;
            record.DeliveryDate = null;

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task DeleteAsync(int id)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Servis kaydı bulunamadı: {id}");

            record.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        private static void ValidateStatusFields(RepairStatus status, string? workDone, string? notRepairedReason, string? waitingReason)
        {
            if (status == RepairStatus.Repaired && string.IsNullOrWhiteSpace(workDone))
                throw new ArgumentException("Onarıldı durumunda 'Yapılan İşlemler' alanı zorunludur.");

            if (status == RepairStatus.NotRepaired && string.IsNullOrWhiteSpace(notRepairedReason))
                throw new ArgumentException("Onarılmadı durumunda 'Onarılmama Sebebi' alanı zorunludur.");
        }
    }
}
