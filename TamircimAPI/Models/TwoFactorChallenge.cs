namespace TamircimAPI.Models
{
    public enum TwoFactorPurpose
    {
        Login = 0,   // Girişte 2. adım
        Enable = 1   // 2FA'yı açarken e-posta sahipliği doğrulaması
    }

    // İki adımlı doğrulama meydan okuması (e-posta OTP). PasswordResetToken deseninin aynısı:
    // 6 haneli kodun HASH'i saklanır (düz kod yalnızca e-postada), tek kullanımlık, kısa süreli
    // (5 dk) ve sınırlı deneme (AttemptCount → brute-force engeli). İstemciye verilen opak
    // challenge token'ın da yalnızca HASH'i tutulur (refresh token deseni gibi).
    public class TwoFactorChallenge
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        // İstemciye düz verilen opak challenge token'ın SHA-256 hash'i (doğrulamada bununla aranır).
        public string ChallengeHash { get; set; } = string.Empty;

        // 6 haneli kodun SHA-256 hash'i.
        public string CodeHash { get; set; } = string.Empty;

        // Login mi yoksa 2FA açma mı — challenge'ın yanlış akışta kullanılmasını engeller.
        public TwoFactorPurpose Purpose { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConsumedAt { get; set; }

        // Yanlış kod denemesi sayısı — eşiği aşınca challenge geçersiz kılınır.
        public int AttemptCount { get; set; }

        public bool IsConsumed => ConsumedAt != null;
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !IsConsumed && !IsExpired;

        public User User { get; set; } = null!;
    }
}
