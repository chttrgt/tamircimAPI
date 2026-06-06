namespace TamircimAPI.Models
{
    // Self-servis kayıt sonrası e-posta doğrulama token'ı. Doğrulanınca tenant
    // aktifleşir (IsActive=true) ve kullanıcı giriş yapabilir. Tek kullanımlık ve
    // süreli; yeniden gönderimde eski token'lar tüketilmiş sayılır.
    public class EmailVerificationToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        // URL-güvenli rastgele token (kriptografik). Benzersiz.
        public string Token { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConsumedAt { get; set; }

        public bool IsConsumed => ConsumedAt != null;
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !IsConsumed && !IsExpired;

        public User User { get; set; } = null!;
    }
}
