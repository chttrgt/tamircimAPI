using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Services.Storage;

namespace TamircimAPI.Services.Device
{
    public class DevicePhotoService : IDevicePhotoService
    {
        private readonly ApplicationDbContext _db;
        private readonly IPhotoStorage _storage;

        public DevicePhotoService(ApplicationDbContext db, IPhotoStorage storage)
        {
            _db = db;
            _storage = storage;
        }

        public async Task<List<DevicePhotoDTO>> GetByDeviceAsync(int deviceId)
        {
            var photos = await _db.DevicePhotos
                .Where(p => p.DeviceId == deviceId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return photos.Select(ToDto).ToList();
        }

        public async Task<DevicePhotoDTO> UploadAsync(
            int deviceId, Stream main, Stream thumbnail,
            int width, int height, CancellationToken ct = default)
        {
            var deviceExists = await _db.Devices.AnyAsync(d => d.Id == deviceId, ct);
            if (!deviceExists)
                throw new KeyNotFoundException("Cihaz bulunamadı.");

            var mainBytes = await ReadAndValidateAsync(main, ct);
            var thumbBytes = await ReadAndValidateAsync(thumbnail, ct);

            var guid = Guid.NewGuid().ToString("N");
            var fileName = $"{guid}.jpg";
            var thumbName = $"{guid}_thumb.jpg";

            await _storage.SaveAsync(deviceId, fileName, mainBytes, ct);
            await _storage.SaveAsync(deviceId, thumbName, thumbBytes, ct);

            var photo = new DevicePhoto
            {
                DeviceId = deviceId,
                FileName = fileName,
                ThumbnailFileName = thumbName,
                ContentType = "image/jpeg",
                SizeBytes = mainBytes.LongLength,
                Width = width > 0 ? width : 0,
                Height = height > 0 ? height : 0,
            };

            _db.DevicePhotos.Add(photo);
            await _db.SaveChangesAsync(ct);

            return ToDto(photo);
        }

        public async Task<(Stream Stream, string ContentType)?> GetFileAsync(int deviceId, int photoId, bool thumb)
        {
            var photo = await _db.DevicePhotos
                .FirstOrDefaultAsync(p => p.Id == photoId && p.DeviceId == deviceId);
            if (photo == null) return null;

            var name = thumb ? photo.ThumbnailFileName : photo.FileName;
            var stream = _storage.OpenRead(deviceId, name);
            if (stream == null) return null;

            return (stream, photo.ContentType);
        }

        public async Task DeleteAsync(int deviceId, int photoId)
        {
            var photo = await _db.DevicePhotos
                .FirstOrDefaultAsync(p => p.Id == photoId && p.DeviceId == deviceId);
            if (photo == null)
                throw new KeyNotFoundException("Fotoğraf bulunamadı.");

            // Soft-delete — DeletedAt/DeletedByUserId DbContext tarafından otomatik set edilir.
            photo.IsDeleted = true;
            await _db.SaveChangesAsync();
        }

        // Akışı belleğe alır + gerçekten resim mi diye magic-byte doğrular (JPEG/PNG).
        private static async Task<byte[]> ReadAndValidateAsync(Stream stream, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (!IsJpeg(bytes) && !IsPng(bytes))
                throw new ArgumentException("Geçersiz veya desteklenmeyen görsel dosyası.");

            return bytes;
        }

        private static bool IsJpeg(byte[] b) =>
            b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

        private static bool IsPng(byte[] b) =>
            b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

        private static DevicePhotoDTO ToDto(DevicePhoto p) => new()
        {
            Id = p.Id,
            Url = $"/api/devices/{p.DeviceId}/photos/{p.Id}/file",
            ThumbnailUrl = $"/api/devices/{p.DeviceId}/photos/{p.Id}/file?thumb=true",
            Width = p.Width,
            Height = p.Height,
            SizeBytes = p.SizeBytes,
            CreatedAt = p.CreatedAt,
        };
    }
}
