using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Services.Payment
{
    public class PaymentCommandService : IPaymentCommandService
    {
        private readonly ApplicationDbContext _db;

        public PaymentCommandService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Var olan bir servis kaydına yeni ödeme (tahsilat) ekler. Her ödeme ayrı
        // satırdır → tahsilat geçmişi korunur, tek "ödenen" alanı üzerinde çakışma olmaz.
        public async Task<PaymentDTO> AddAsync(int repairRecordId, CreatePaymentDTO dto)
        {
            // Servis kaydının (bu tenant'ta) var olduğunu doğrula; query filter
            // başka tenant'ın kaydını zaten görünmez kılar → cross-tenant ekleme engeli.
            var exists = await _db.RepairRecords.AnyAsync(r => r.Id == repairRecordId);
            if (!exists)
                throw new KeyNotFoundException($"Servis kaydı bulunamadı: {repairRecordId}");

            var payment = new Models.Payment
            {
                RepairRecordId = repairRecordId,
                Amount = dto.Amount,
                Method = dto.Method,
                PaidAt = dto.PaidAt ?? DateTime.UtcNow,
                Note = dto.Note?.Trim()
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return PaymentMapper.ToDTO(payment);
        }

        // Ödemeyi soft-delete eder (tahsilat geçmişinden çıkar; ücret durumu yeniden hesaplanır).
        public async Task DeleteAsync(int paymentId)
        {
            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId)
                ?? throw new KeyNotFoundException($"Ödeme bulunamadı: {paymentId}");

            payment.IsDeleted = true;
            await _db.SaveChangesAsync();
        }
    }
}
