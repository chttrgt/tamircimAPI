using TamircimAPI.Models.Enums;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    // Bir servis kaydına (RepairRecord) ait tek bir ödeme işlemi (kapora, kalan,
    // ek tahsilat vb.). Her ödeme ayrı satırdır → tam tahsilat geçmişi tutulur ve
    // tek bir "ödenen" kolonu üzerinde eşzamanlılık çakışması (lost update) yaşanmaz.
    public class Payment : IAuditable, ISoftDeletable, ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int RepairRecordId { get; set; }

        // Tahsil edilen tutar. numeric(12,2); para için ASLA float/double kullanılmaz.
        public decimal Amount { get; set; }

        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

        // Ödemenin alındığı tarih (geçmiş tarihli kayıt için ayarlanabilir).
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;

        public string? Note { get; set; }

        // IAuditable
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }

        // ISoftDeletable
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }

        public RepairRecord RepairRecord { get; set; } = null!;
        public User? CreatedByUser { get; set; }
        public User? UpdatedByUser { get; set; }
        public User? DeletedByUser { get; set; }
    }
}
