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
