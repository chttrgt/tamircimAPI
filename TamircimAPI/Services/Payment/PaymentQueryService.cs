using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Services.Payment
{
    public class PaymentQueryService : IPaymentQueryService
    {
        private readonly ApplicationDbContext _db;

        public PaymentQueryService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Bir servis kaydının ödemelerini (tahsilat geçmişi) tarih sırasıyla döner.
        // Global query filter tenant izolasyonunu ve silinen ödemelerin elenmesini sağlar.
        public async Task<IEnumerable<PaymentDTO>> GetByRepairAsync(int repairRecordId)
        {
            var payments = await _db.Payments
                .Where(p => p.RepairRecordId == repairRecordId)
                .OrderBy(p => p.PaidAt)
                .ToListAsync();

            return payments.Select(PaymentMapper.ToDTO).ToList();
        }
    }
}
