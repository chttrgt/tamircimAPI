namespace TamircimAPI.Models.DTOs.Auth
{
    public class LoginDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDTO
    {
        // Yeni teknik servisin (tenant) adı — kaydeden kişi bu dükkânın sahibi olur.
        public string ShopName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        // Cloudflare Turnstile token'ı (istemcide widget üretir, sunucuda doğrulanır).
        public string? CaptchaToken { get; set; }
    }

    // Kayıt artık otomatik giriş yapmaz: e-posta doğrulanana kadar tenant pasiftir.
    public class RegisterResponseDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class ResendVerificationDTO
    {
        public string Email { get; set; } = string.Empty;
    }

    public class LoginResponseDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        // "Owner" | "Employee" — istemci sahibe özel ekranları (personel) buna göre gösterir.
        public string Role { get; set; } = string.Empty;
        // Çalışanın sahip olduğu izinler (Sahip için boş — örtük tüm izinler).
        public List<string> Permissions { get; set; } = new();
        // true ise istemci, app'e girmeden önce zorunlu şifre değişimi ekranı gösterir.
        public bool MustChangePassword { get; set; }
    }

    public class RefreshTokenDTO
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UpdateProfileDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    public class UpdateProfileResponseDTO
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        // E-posta değişince mevcut token geçersizleşir → yeni token döndür
        public string? NewAccessToken { get; set; }
        public string? NewRefreshToken { get; set; }
    }
}
