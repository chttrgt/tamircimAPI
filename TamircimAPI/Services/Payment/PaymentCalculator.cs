using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Payment
{
    // Ücret (Price) ile tahsilat toplamından ödeme durumunu/kalanı türetir.
    // SAKLANMAZ — okuma anında hesaplanır (bkz PaymentStatus enum yorumu), böylece
    // tutar veya ödemeler değişince durum kendiliğinden tutarlı kalır.
    public static class PaymentCalculator
    {
        // Ücret belirlenmemişse (null) kalan bilinemez → null döner.
        public static decimal? Remaining(decimal? price, decimal totalPaid) =>
            price.HasValue ? price.Value - totalPaid : (decimal?)null;

        public static PaymentStatus Status(decimal? price, decimal totalPaid)
        {
            if (totalPaid <= 0) return PaymentStatus.Unpaid;
            if (price.HasValue && totalPaid >= price.Value) return PaymentStatus.Paid;
            // Ödeme var ama ya ücret henüz belirlenmemiş ya da tutarın altında → kısmi.
            return PaymentStatus.Partial;
        }
    }
}
