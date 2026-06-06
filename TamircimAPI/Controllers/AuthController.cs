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

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.LoginAsync(dto, ipAddress);
            return Ok(result);
        }

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
