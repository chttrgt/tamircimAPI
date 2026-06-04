namespace TamircimAPI.Services.Storage
{
    // Görselleri sunucu diskinde {root}/devices/{deviceId}/{fileName} altında tutar.
    // root, appsettings "Storage:PhotosPath" (varsayılan "uploads"); wwwroot DEĞİL
    // → public statik servis yok, erişim yalnızca auth'lu controller üzerinden.
    public class LocalPhotoStorage : IPhotoStorage
    {
        private readonly string _root;

        public LocalPhotoStorage(IConfiguration configuration, IWebHostEnvironment env)
        {
            var configured = configuration["Storage:PhotosPath"];
            if (string.IsNullOrWhiteSpace(configured))
                configured = "uploads";

            // Göreli yol verilmişse uygulama köküne göre çöz
            _root = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(env.ContentRootPath, configured);
        }

        private string DeviceDir(int deviceId) => Path.Combine(_root, "devices", deviceId.ToString());

        // Path traversal koruması: dosya adı yalnızca üretilen guid tabanlı isim olmalı.
        private static void EnsureSafeName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.Contains('/') || fileName.Contains('\\')
                || fileName.Contains("..")
                || Path.GetFileName(fileName) != fileName)
            {
                throw new ArgumentException("Geçersiz dosya adı.", nameof(fileName));
            }
        }

        public async Task SaveAsync(int deviceId, string fileName, byte[] bytes, CancellationToken ct = default)
        {
            EnsureSafeName(fileName);
            var dir = DeviceDir(deviceId);
            Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(Path.Combine(dir, fileName), bytes, ct);
        }

        public Stream? OpenRead(int deviceId, string fileName)
        {
            EnsureSafeName(fileName);
            var path = Path.Combine(DeviceDir(deviceId), fileName);
            if (!File.Exists(path)) return null;
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Delete(int deviceId, string fileName)
        {
            EnsureSafeName(fileName);
            var path = Path.Combine(DeviceDir(deviceId), fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
