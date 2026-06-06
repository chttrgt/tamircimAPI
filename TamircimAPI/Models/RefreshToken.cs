namespace TamircimAPI.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        // Refresh public bir akış (tenant context yok) — bu kolona EF filtresi/RLS
        // UYGULANMAZ. Yalnızca kayıt/teşhis amaçlı tutulur; izolasyon token sırrıyla
        // (64-byte rastgele) sağlanır.
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedByIp { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedByIp { get; set; }
        public string? ReplacedByToken { get; set; }
        public string? RevokeReason { get; set; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsRevoked => RevokedAt != null;
        public bool IsActive => !IsRevoked && !IsExpired;

        public User User { get; set; } = null!;
    }
}
