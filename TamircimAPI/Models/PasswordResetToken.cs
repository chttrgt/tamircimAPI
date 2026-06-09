namespace TamircimAPI.Models
{
    // Şifre sıfırlama kodu. Kullanıcı "şifremi unuttum" deyince üretilir; e-postaya
    // 6 haneli kod gönderilir, DB'de kodun HASH'i saklanır. Tek kullanımlık, süreli (15 dk)
    // ve sınırlı deneme hakkı (AttemptCount → brute-force koruması).
    public class PasswordResetToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        // 6 haneli kodun SHA-256 hash'i (düz kod yalnızca e-postada gider).
        public string CodeHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConsumedAt { get; set; }

        // Yanlış kod denemesi sayısı — eşiği aşınca token geçersiz kılınır (brute-force engeli).
        public int AttemptCount { get; set; }

        public bool IsConsumed => ConsumedAt != null;
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !IsConsumed && !IsExpired;

        public User User { get; set; } = null!;
    }
}
