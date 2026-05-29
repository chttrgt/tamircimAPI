using TamircimAPI.Models.Enums;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    public class Device : IAuditable, ISoftDeletable
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeliveryDate { get; set; }
        public bool IsDelivered { get; set; } = false;
        public DateTime? DeliveredAt { get; set; }
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

        public Customer Customer { get; set; } = null!;
        public User? CreatedByUser { get; set; }
        public User? UpdatedByUser { get; set; }
        public User? DeletedByUser { get; set; }

        public ICollection<RepairRecord> RepairRecords { get; set; } = new List<RepairRecord>();
    }
}
