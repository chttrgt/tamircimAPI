using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Services.Payment
{
    public interface IPaymentCommandService
    {
        // Var olan bir servis kaydına yeni ödeme (tahsilat) ekler.
        Task<PaymentDTO> AddAsync(int repairRecordId, CreatePaymentDTO dto);

        // Ödemeyi soft-delete eder.
        Task DeleteAsync(int paymentId);
    }
}
