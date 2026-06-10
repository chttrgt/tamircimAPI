using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Services.Payment
{
    public interface IPaymentQueryService
    {
        // Bir servis kaydının ödemelerini (tahsilat geçmişi) tarih sırasıyla döner.
        Task<IEnumerable<PaymentDTO>> GetByRepairAsync(int repairRecordId);
    }
}
