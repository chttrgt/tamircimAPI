namespace TamircimAPI.Services.Captcha
{
    public interface ICaptchaVerifier
    {
        // İstemciden gelen captcha token'ını sağlayıcıda doğrular. remoteIp opsiyonel
        // (ek doğrulama için). Geçerli/insan → true, aksi → false.
        Task<bool> VerifyAsync(string? token, string? remoteIp);
    }
}
