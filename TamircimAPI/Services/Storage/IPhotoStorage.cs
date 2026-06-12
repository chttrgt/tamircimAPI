namespace TamircimAPI.Services.Storage
{
    // Görsel byte'larının fiziksel depolanmasını soyutlar.
    // Şu an LocalPhotoStorage (sunucu diski); ileride S3/R2 için tek implementasyon eklenir.
    // Tenant izolasyonu DİSK seviyesinde: {root}/devices/{tenantId}/{deviceId}/{fileName}.
    public interface IPhotoStorage
    {
        Task SaveAsync(int tenantId, int deviceId, string fileName, byte[] bytes, CancellationToken ct = default);

        // Dosya yoksa null döner.
        Stream? OpenRead(int tenantId, int deviceId, string fileName);

        void Delete(int tenantId, int deviceId, string fileName);

        // Bir tenant'ın TÜM fotoğraf klasörünü ({root}/devices/{tenantId}) kalıcı siler.
        // Hesap silmede tek hamlede kullanılır. Klasör yoksa sessizce geçer.
        void DeleteTenant(int tenantId);
    }
}
