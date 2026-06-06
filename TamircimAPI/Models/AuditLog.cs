using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    public class AuditLog : ITenantOwned
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public string Action { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ChangedFields { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }

        public User? User { get; set; }
        public User? DeletedByUser { get; set; }
    }
}
