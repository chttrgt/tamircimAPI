namespace TamircimAPI.Models.DTOs.Auth
{
    // "Şifremi unuttum" — e-posta + captcha. Yanıt her zaman aynıdır (numaralandırma yok).
    public class ForgotPasswordDTO
    {
        public string Email { get; set; } = string.Empty;
        // Cloudflare Turnstile token'ı (bot/spam e-posta gönderimini engeller).
        public string? CaptchaToken { get; set; }
    }

    // Şifre sıfırlama — e-posta + 6 haneli kod + yeni şifre.
    public class ResetPasswordRequestDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
