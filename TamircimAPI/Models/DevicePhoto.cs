using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    // Bir cihaza (asset) ait fotoğraf. Görsel byte'ları sunucu diskinde tutulur;
    // bu kayıt yalnızca metadata ve disk dosyalarının adlarını saklar.
    // Hibrit silme: kullanıcı silince soft-delete; arka plan görevi retention
    // süresi dolunca diskten + DB'den kalıcı temizler.
    public class DevicePhoto : IAuditable, ISoftDeletable
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }

        // Diskteki dosya adları (guid.jpg / guid_thumb.jpg). Yol deviceId'den türetilir.
        public string FileName { get; set; } = string.Empty;
        public string ThumbnailFileName { get; set; } = string.Empty;

        public string ContentType { get; set; } = "image/jpeg";
        public long SizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

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
