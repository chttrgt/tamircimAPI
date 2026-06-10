using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Payment;

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

            var items = await query
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
                    Price = r.Price,
                    // Silinen ödemeler global query filter ile zaten elenir.
                    TotalPaid = r.Payments.Sum(p => (decimal?)p.Amount) ?? 0m,
                    CreatedAt = r.CreatedAt,
                    CompletedAt = r.CompletedAt
                })
                .ToListAsync();

            // Kalan tutar ve ödeme durumu Price + TotalPaid'den türetilir (saklanmaz).
            foreach (var item in items)
            {
                item.Remaining = PaymentCalculator.Remaining(item.Price, item.TotalPaid);
                item.PaymentStatus = PaymentCalculator.Status(item.Price, item.TotalPaid);
            }

            return items;
        }

        public async Task<RepairRecordDTO?> GetByIdAsync(int id)
        {
            var r = await _db.RepairRecords
                .Include(x => x.Device).ThenInclude(d => d.Customer)
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (r == null) return null;

            // Payments navigation'ı global query filter ile tenant-scope'lu ve silinmemiş gelir.
            var payments = r.Payments.OrderBy(p => p.PaidAt).Select(PaymentMapper.ToDTO).ToList();
            var totalPaid = r.Payments.Sum(p => p.Amount);

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
                Price = r.Price,
                TotalPaid = totalPaid,
                Remaining = PaymentCalculator.Remaining(r.Price, totalPaid),
                PaymentStatus = PaymentCalculator.Status(r.Price, totalPaid),
                Payments = payments,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }
    }
}
