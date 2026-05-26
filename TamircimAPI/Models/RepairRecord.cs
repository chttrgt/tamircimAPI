using TamircimAPI.Models.Enums;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    public class RepairRecord : IAuditable, ISoftDeletable
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.Waiting;

        // Onarıldıysa ne yapıldı
        public string? WorkDone { get; set; }

        // Onarılmadıysa sebebi
        public string? NotRepairedReason { get; set; }

        // Beklemedeyse sebebi (örn: yedek parça bekleniyor)
        public string? WaitingReason { get; set; }

        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }

        // IAuditable
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int? CreatedByUserId { get; set; }
        public int? UpdatedByUserId { get; set; }

        // ISoftDeletable
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }

        public Device Device { get; set; } = null!;
        public User? CreatedByUser { get; set; }
        public User? UpdatedByUser { get; set; }
        public User? DeletedByUser { get; set; }
    }
}
