using TamircimAPI.Models.Enums;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    // Fiziksel cihaz (asset). Ömür boyu tek satır; her getirilişte yeni satır AÇILMAZ.
    // Bir cihazın birden fazla servis kaydı (RepairRecord) olabilir.
    public class Device : IAuditable, ISoftDeletable
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }

        // Dahili benzersiz kimlik (asset tag), örn. "CHZ-000123". Seri no olsun olmasın her cihazda bulunur.
        public string DeviceCode { get; set; } = string.Empty;

        public DeviceType DeviceType { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? ExtraFields { get; set; }

        // Cihazın kendisine ait kalıcı not (arızaya değil, cihaza dair).
        public string? Notes { get; set; }

        // IAuditable — CreatedAt = cihazın ilk kayıt tarihi
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
