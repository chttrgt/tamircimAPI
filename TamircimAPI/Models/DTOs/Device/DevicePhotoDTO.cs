namespace TamircimAPI.Models.DTOs.Device
{
    // Cihaz fotoğrafı metadata + erişim URL'leri.
    // URL'ler auth'lu endpoint'i işaret eder (public dosya servisi yok).
    public class DevicePhotoDTO
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
