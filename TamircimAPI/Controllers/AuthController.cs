using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TamircimAPI.Models.DTOs.Auth;
using TamircimAPI.Services.Auth;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // Accept-Language header'ından desteklenen dile indirger (tr|en|de, varsayılan tr).
        // Frontend her isteğe aktif dili ekler; e-posta bu dile göre üretilir.
        private string ResolveLang()
        {
            var raw = Request.Headers.AcceptLanguage.ToString();
            var code = raw.Split(',').FirstOrDefault()?.Trim().Split('-')[0].ToLowerInvariant();
            return code is "en" or "de" ? code : "tr";
        }

        // Açık self-servis kayıt: yeni tenant (dükkân) + sahip oluşturur, doğrulama
        // e-postası gönderir. Giriş token'ı dönmez — önce e-posta doğrulanmalı.
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.RegisterAsync(dto, ipAddress, ResolveLang());
            return Ok(result);
        }

        // E-posta bağlantısından açılır (GET). Token'ı doğrular, tenant'ı aktifleştirir
        // ve basit bir HTML sayfası döner. Public.
        [HttpGet("verify-email")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            var ok = await _authService.VerifyEmailAsync(token);
            var body = ok
                ? "<h2>E-posta doğrulandı ✓</h2><p>Artık Tamircim uygulamasına giriş yapabilirsin.</p>"
                : "<h2>Bağlantı geçersiz veya süresi dolmuş</h2><p>Lütfen uygulamadan yeni doğrulama e-postası iste.</p>";
            var html = $"<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"></head><body style=\"font-family:sans-serif;text-align:center;padding:40px;color:#0f172a\">{body}</body></html>";
            // charset=utf-8 → Türkçe karakterler (ş, ç, ı, ğ) bozulmaz.
            return Content(html, "text/html; charset=utf-8");
        }

        [HttpPost("resend-verification")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDTO dto)
        {
            await _authService.ResendVerificationAsync(dto.Email, ResolveLang());
            // Numaralandırma sızdırmamak için her zaman aynı yanıt.
            return Ok(new { message = "Hesap doğrulanmamışsa yeni bir doğrulama e-postası gönderildi." });
        }

        // Şifremi unuttum: e-postaya 6 haneli kod gönderir. Numaralandırma sızdırmaz
        // (her zaman aynı yanıt). Captcha + rate-limit ile bot/spam korumalı.
        [HttpPost("forgot-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _authService.ForgotPasswordAsync(dto, ipAddress, ResolveLang());
            return Ok(new { message = "E-posta kayıtlıysa şifre sıfırlama kodu gönderildi." });
        }

        // Kod + yeni şifre ile sıfırlar. Başarılı olunca tüm oturumlar iptal edilir.
        [HttpPost("reset-password")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _authService.ResetPasswordAsync(dto, ipAddress);
            return Ok(new { message = "Şifren güncellendi. Yeni şifrenle giriş yapabilirsin." });
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.LoginAsync(dto, ipAddress, ResolveLang());
            return Ok(result);
        }

        // ── İki adımlı doğrulama (e-posta OTP) ──

        // Login 2. adımı — public (token henüz yok): challenge + kod → token'lar.
        [HttpPost("2fa/verify")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerifyDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.VerifyTwoFactorLoginAsync(dto, ipAddress);
            return Ok(result);
        }

        [HttpPost("2fa/resend")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResendTwoFactor([FromBody] TwoFactorResendDTO dto)
        {
            await _authService.ResendTwoFactorAsync(dto.ChallengeToken, ResolveLang());
            return Ok(new { message = "Kod tekrar gönderildi." });
        }

        // Açma/kapama — yalnızca Sahip, kimlik doğrulamalı.
        [HttpPost("2fa/enable/request")]
        [Authorize(Roles = "Owner")]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> RequestEnableTwoFactor()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var result = await _authService.RequestEnableTwoFactorAsync(userId, ResolveLang());
            return Ok(result);
        }

        [HttpPost("2fa/enable/confirm")]
        [Authorize(Roles = "Owner")]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> ConfirmEnableTwoFactor([FromBody] TwoFactorVerifyDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            await _authService.ConfirmEnableTwoFactorAsync(userId, dto);
            return Ok(new { message = "İki adımlı doğrulama açıldı." });
        }

        [HttpPost("2fa/disable")]
        [Authorize(Roles = "Owner")]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            await _authService.DisableTwoFactorAsync(userId, dto.Password);
            return Ok(new { message = "İki adımlı doğrulama kapatıldı." });
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        [HttpPost("refresh-token")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken, ipAddress);
            return Ok(result);
        }

        [HttpPut("profile")]
        [Authorize]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDTO dto)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.UpdateProfileAsync(userId, dto, ipAddress);
            return Ok(result);
        }

        [HttpPost("revoke-token")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _authService.RevokeTokenAsync(dto.RefreshToken, ipAddress);
            return Ok(new { message = "Token iptal edildi." });
        }
    }
}
