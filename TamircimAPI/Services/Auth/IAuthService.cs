using TamircimAPI.Models.DTOs.Auth;

namespace TamircimAPI.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress);
        // Yeni tenant + sahip oluşturur ve doğrulama e-postası gönderir. Giriş token'ı DÖNMEZ.
        // lang: e-posta dili ("tr"|"en"|"de").
        Task<RegisterResponseDTO> RegisterAsync(RegisterDTO dto, string ipAddress, string lang);
        // Doğrulama token'ını tüketir → tenant aktifleşir. Başarı/başarısızlık döner.
        Task<bool> VerifyEmailAsync(string token);
        // Doğrulanmamış hesaba yeni doğrulama e-postası gönderir (numaralandırma sızdırmaz).
        Task ResendVerificationAsync(string email, string lang);
        Task<LoginResponseDTO> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task RevokeTokenAsync(string refreshToken, string ipAddress);
        Task<UpdateProfileResponseDTO> UpdateProfileAsync(int userId, UpdateProfileDTO dto, string ipAddress);
    }
}
