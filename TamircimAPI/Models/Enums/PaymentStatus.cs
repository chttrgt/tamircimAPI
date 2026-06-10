namespace TamircimAPI.Models.Enums
{
    // Bir servis kaydının ödeme durumu. SAKLANMAZ — ücret (Price) ile ödemeler
    // toplamı karşılaştırılarak okuma anında hesaplanır (denormalize alan tutulmaz,
    // böylece tutar/ödeme değişince durum kendiliğinden tutarlı kalır).
    public enum PaymentStatus
    {
        Unpaid = 0,   // Hiç ödeme yapılmamış (veya ücret henüz belirlenmemiş)
        Partial = 1,  // Kısmi ödeme (kapora vb.) yapılmış
        Paid = 2      // Tamamı ödenmiş
    }
}
