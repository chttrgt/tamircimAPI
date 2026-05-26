using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Repair
{
    public class RepairCommandService : IRepairCommandService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRepairQueryService _query;

        public RepairCommandService(ApplicationDbContext db, IRepairQueryService query)
        {
            _db = db;
            _query = query;
        }

        public async Task<RepairRecordDTO> CreateAsync(CreateRepairRecordDTO dto)
        {
            var deviceExists = await _db.Devices.AnyAsync(d => d.Id == dto.DeviceId);
            if (!deviceExists)
                throw new KeyNotFoundException($"Cihaz bulunamadı: {dto.DeviceId}");

            ValidateStatusFields(dto.Status, dto.WorkDone, dto.NotRepairedReason, dto.WaitingReason);

            var record = new RepairRecord
            {
                DeviceId = dto.DeviceId,
                Status = dto.Status,
                WorkDone = dto.WorkDone?.Trim(),
                NotRepairedReason = dto.NotRepairedReason?.Trim(),
                WaitingReason = dto.WaitingReason?.Trim(),
                CompletedAt = dto.CompletedAt,
                Notes = dto.Notes?.Trim()
            };

            _db.RepairRecords.Add(record);
            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(record.Id))!;
        }

        public async Task<RepairRecordDTO> UpdateAsync(int id, UpdateRepairRecordDTO dto)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Arıza kaydı bulunamadı: {id}");

            ValidateStatusFields(dto.Status, dto.WorkDone, dto.NotRepairedReason, dto.WaitingReason);

            record.Status = dto.Status;
            record.WorkDone = dto.WorkDone?.Trim();
            record.NotRepairedReason = dto.NotRepairedReason?.Trim();
            record.WaitingReason = dto.WaitingReason?.Trim();
            record.CompletedAt = dto.CompletedAt;
            record.Notes = dto.Notes?.Trim();

            await _db.SaveChangesAsync();

            return (await _query.GetByIdAsync(id))!;
        }

        public async Task DeleteAsync(int id)
        {
            var record = await _db.RepairRecords.FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException($"Arıza kaydı bulunamadı: {id}");

            record.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        private static void ValidateStatusFields(RepairStatus status, string? workDone, string? notRepairedReason, string? waitingReason)
        {
            if (status == RepairStatus.Repaired && string.IsNullOrWhiteSpace(workDone))
                throw new ArgumentException("Onarıldı durumunda 'Yapılan İşlemler' alanı zorunludur.");

            if (status == RepairStatus.NotRepaired && string.IsNullOrWhiteSpace(notRepairedReason))
                throw new ArgumentException("Onarılmadı durumunda 'Onarılmama Sebebi' alanı zorunludur.");

            if (status == RepairStatus.Waiting && string.IsNullOrWhiteSpace(waitingReason))
                throw new ArgumentException("Beklemede durumunda 'Bekleme Sebebi' alanı zorunludur.");
        }
    }
}
