using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Payment
{
    public class PaymentDTO
    {
        public int Id { get; set; }
        public int RepairRecordId { get; set; }
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public string MethodLabel { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Bir servis kaydına yeni ödeme (tahsilat) ekler. RepairRecordId route'tan gelir.
    public class CreatePaymentDTO
    {
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        // İsteğe bağlı: geçmiş tarihli ödeme. null → sunucu saati (UtcNow).
        public DateTime? PaidAt { get; set; }
        public string? Note { get; set; }
    }
}
