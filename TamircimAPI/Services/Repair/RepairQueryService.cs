using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Repair
{
    public class RepairQueryService : IRepairQueryService
    {
        private readonly ApplicationDbContext _db;

        public RepairQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<RepairRecordListDTO>> GetAllAsync(int? deviceId = null, RepairStatus? status = null)
        {
            var query = _db.RepairRecords
                .Include(r => r.Device).ThenInclude(d => d.Customer)
                .AsQueryable();

            if (deviceId.HasValue)
                query = query.Where(r => r.DeviceId == deviceId.Value);

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            return await query
                .OrderByDescending(r => r.ReceivedAt)
                .Select(r => new RepairRecordListDTO
                {
                    Id = r.Id,
                    DeviceId = r.DeviceId,
                    TicketNo = r.TicketNo,
                    DeviceCode = r.Device.DeviceCode,
                    DeviceBrand = r.Device.Brand,
                    DeviceModel = r.Device.Model,
                    CustomerFullName = r.Device.Customer.FirstName + " " + r.Device.Customer.LastName,
                    FaultDescription = r.FaultDescription,
                    ReceivedAt = r.ReceivedAt,
                    DeliveryDate = r.DeliveryDate,
                    IsDelivered = r.IsDelivered,
                    DeliveredAt = r.DeliveredAt,
                    Status = r.Status,
                    StatusLabel = r.Status == RepairStatus.Waiting ? "Beklemede"
                        : r.Status == RepairStatus.Repaired ? "Onarıldı"
                        : "Onarılmadı",
                    WorkDone = r.WorkDone,
                    NotRepairedReason = r.NotRepairedReason,
                    WaitingReason = r.WaitingReason,
                    Notes = r.Notes,
                    CreatedAt = r.CreatedAt,
                    CompletedAt = r.CompletedAt
                })
                .ToListAsync();
        }

        public async Task<RepairRecordDTO?> GetByIdAsync(int id)
        {
            var r = await _db.RepairRecords
                .Include(x => x.Device).ThenInclude(d => d.Customer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (r == null) return null;

            return new RepairRecordDTO
            {
                Id = r.Id,
                DeviceId = r.DeviceId,
                TicketNo = r.TicketNo,
                DeviceCode = r.Device.DeviceCode,
                DeviceBrand = r.Device.Brand,
                DeviceModel = r.Device.Model,
                CustomerFullName = r.Device.Customer.FullName,
                FaultDescription = r.FaultDescription,
                ReceivedAt = r.ReceivedAt,
                DeliveryDate = r.DeliveryDate,
                IsDelivered = r.IsDelivered,
                DeliveredAt = r.DeliveredAt,
                Status = r.Status,
                StatusLabel = r.Status == RepairStatus.Waiting ? "Beklemede"
                    : r.Status == RepairStatus.Repaired ? "Onarıldı"
                    : "Onarılmadı",
                WorkDone = r.WorkDone,
                NotRepairedReason = r.NotRepairedReason,
                WaitingReason = r.WaitingReason,
                CompletedAt = r.CompletedAt,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }
    }
}
