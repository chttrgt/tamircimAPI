using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Payment
{
    // Ödeme yöntemi için Türkçe etiket. Mevcut desen (RepairStatus → StatusLabel)
    // ile tutarlı: istemci hem enum değerini hem hazır etiketi alır.
    public static class PaymentLabels
    {
        public static string Method(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "Nakit",
            PaymentMethod.Card => "Kart",
            PaymentMethod.Transfer => "Havale/EFT",
            _ => "Diğer"
        };
    }
}
