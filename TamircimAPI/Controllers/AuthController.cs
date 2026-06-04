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

        // İlk-kurulum kontrolü: istemci açılışta sahip kurulumu mu yoksa giriş mi
        // göstereceğine buna göre karar verir. Public (auth gerektirmez).
        [HttpGet("setup-status")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> SetupStatus()
        {
            var initialized = await _authService.IsInitializedAsync();
            return Ok(new { initialized });
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.RegisterAsync(dto, ipAddress);
            return Ok(result);
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
