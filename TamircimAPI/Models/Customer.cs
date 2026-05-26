using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    public class Customer : IAuditable, ISoftDeletable
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string Phone1 { get; set; } = string.Empty;
        public string? Phone2 { get; set; }
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

        public string FullName => $"{FirstName} {LastName}";

        public User? CreatedByUser { get; set; }
        public User? UpdatedByUser { get; set; }
        public User? DeletedByUser { get; set; }

        public ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
