namespace TamircimAPI.Models.Enums
{
    // Ödeme yöntemi. Muhasebe/raporlama için işlemin nasıl alındığını tutar.
    public enum PaymentMethod
    {
        Cash = 0,      // Nakit
        Card = 1,      // Kredi/banka kartı
        Transfer = 2,  // Havale/EFT
        Other = 3      // Diğer
    }
}
