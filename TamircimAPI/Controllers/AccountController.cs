using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TamircimAPI.Models.DTOs.Account;
using TamircimAPI.Services.Account;

namespace TamircimAPI.Controllers
{
    // Hesap (tenant) yaşam döngüsü — yalnızca dükkân sahibi (Owner).
    [ApiController]
    [Route("api/account")]
    [Authorize(Roles = "Owner")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _account;

        public AccountController(IAccountService account)
        {
            _account = account;
        }

        // Hesabı silme talebi: şifre teyidiyle grace süresi başlatır, hesabı askıya alır.
        [HttpPost("deletion/request")]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> RequestDeletion([FromBody] AccountDeletionRequestDTO dto)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var scheduledAt = await _account.RequestDeletionAsync(userId, dto.Password);
            return Ok(new AccountDeletionResponseDTO { ScheduledAt = scheduledAt });
        }

        // Grace süresi içindeyken silmeyi iptal eder.
        [HttpPost("deletion/cancel")]
        [EnableRateLimiting("profile")]
        public async Task<IActionResult> CancelDeletion()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            await _account.CancelDeletionAsync(userId);
            return Ok(new { message = "Hesap silme iptal edildi." });
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
