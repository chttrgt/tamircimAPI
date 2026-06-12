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

        // İki adımlı doğrulama açıksa, login token vermez: TwoFactorRequired=true +
        // ChallengeToken döner; istemci e-postaya gelen kodu /2fa/verify ile gönderir.
        public bool TwoFactorRequired { get; set; }
        public string? ChallengeToken { get; set; }
        // Kullanıcının 2FA durumu (istemci profil ekranında anahtarı gösterir).
        public bool TwoFactorEnabled { get; set; }

        // Hesap silme bekliyorsa (yalnızca Owner girişinde dolu): kalıcı silme tarihi.
        // İstemci bunu görünce sahibi "silme bekliyor + iptal" ekranına yönlendirir.
        public DateTime? AccountDeletionScheduledAt { get; set; }
    }

    // 2FA challenge yanıtı (login 2. adım için ChallengeToken + maskeli e-posta).
    public class TwoFactorChallengeResponseDTO
    {
        public string ChallengeToken { get; set; } = string.Empty;
        // "a***@x.com" gibi maskeli e-posta — kullanıcıya kodun nereye gittiğini gösterir.
        public string MaskedEmail { get; set; } = string.Empty;
    }

    // Login 2. adımı / 2FA açma onayı: challenge + 6 haneli kod.
    public class TwoFactorVerifyDTO
    {
        public string ChallengeToken { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    // Kod tekrar gönder.
    public class TwoFactorResendDTO
    {
        public string ChallengeToken { get; set; } = string.Empty;
    }

    // 2FA kapatma — şifre teyidi gerekir (oturum ele geçirilse bile kapatılamasın).
    public class TwoFactorDisableDTO
    {
        public string Password { get; set; } = string.Empty;
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
