namespace TamircimAPI.Services.Storage
{
    // Görsel byte'larının fiziksel depolanmasını soyutlar.
    // Şu an LocalPhotoStorage (sunucu diski); ileride S3/R2 için tek implementasyon eklenir.
    public interface IPhotoStorage
    {
        Task SaveAsync(int deviceId, string fileName, byte[] bytes, CancellationToken ct = default);

        // Dosya yoksa null döner.
        Stream? OpenRead(int deviceId, string fileName);

        void Delete(int deviceId, string fileName);
    }
}
