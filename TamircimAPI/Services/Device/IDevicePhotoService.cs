using TamircimAPI.Models.DTOs.Device;

namespace TamircimAPI.Services.Device
{
    public interface IDevicePhotoService
    {
        Task<List<DevicePhotoDTO>> GetByDeviceAsync(int deviceId);

        // İstemci sıkıştırılmış ana görsel + thumbnail yükler (EXIF istemcide temizlenir).
        Task<DevicePhotoDTO> UploadAsync(
            int deviceId, Stream main, Stream thumbnail,
            int width, int height, CancellationToken ct = default);

        // Dosya byte'larını servis eder. Fotoğraf yok/erişilemezse null.
        Task<(Stream Stream, string ContentType)?> GetFileAsync(int deviceId, int photoId, bool thumb);

        // Soft-delete (hibrit: GC görevi retention sonrası diskten kalıcı siler).
        Task DeleteAsync(int deviceId, int photoId);
    }
}
