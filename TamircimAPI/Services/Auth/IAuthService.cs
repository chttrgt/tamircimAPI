using TamircimAPI.Models.DTOs.Auth;

namespace TamircimAPI.Services.Auth
{
    public interface IAuthService
    {
        // lang: 2FA açıksa e-postaya kod gönderiminin dili ("tr"|"en"|"de").
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress, string lang);
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
        // "Şifremi unuttum": e-postaya 6 haneli kod gönderir (numaralandırma sızdırmaz).
        Task ForgotPasswordAsync(ForgotPasswordDTO dto, string ipAddress, string lang);
        // Kod + yeni şifre ile sıfırlar; tüm aktif oturumları iptal eder.
        Task ResetPasswordAsync(ResetPasswordRequestDTO dto, string ipAddress);

        // ── İki adımlı doğrulama (e-posta OTP, yalnızca Sahip) ──
        // Login 2. adımı: challenge + kod doğrulanınca token'ları verir (girişi tamamlar).
        Task<LoginResponseDTO> VerifyTwoFactorLoginAsync(TwoFactorVerifyDTO dto, string ipAddress);
        // Geçerli challenge için yeni kod gönderir (cooldown'lu).
        Task ResendTwoFactorAsync(string challengeToken, string lang);
        // 2FA açma — 1) kod iste (e-posta), 2) kodu onayla → etkinleştir.
        Task<TwoFactorChallengeResponseDTO> RequestEnableTwoFactorAsync(int userId, string lang);
        Task ConfirmEnableTwoFactorAsync(int userId, TwoFactorVerifyDTO dto);
        // 2FA kapatma — şifre teyidiyle.
        Task DisableTwoFactorAsync(int userId, string password);
    }
}
