using TamircimAPI.Models.DTOs.Payment;

namespace TamircimAPI.Services.Payment
{
    // Payment entity → PaymentDTO eşlemesi. MethodLabel SQL'e çevrilemediğinden
    // (PaymentLabels.Method bir C# switch'idir) eşleme bellekte yapılır.
    internal static class PaymentMapper
    {
        public static PaymentDTO ToDTO(Models.Payment p) => new()
        {
            Id = p.Id,
            RepairRecordId = p.RepairRecordId,
            Amount = p.Amount,
            Method = p.Method,
            MethodLabel = PaymentLabels.Method(p.Method),
            PaidAt = p.PaidAt,
            Note = p.Note,
            CreatedAt = p.CreatedAt
        };
    }
}
