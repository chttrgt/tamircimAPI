using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TamircimAPI.Models.DTOs.Staff;
using TamircimAPI.Services.Staff;

namespace TamircimAPI.Controllers
{
    // Personel yönetimi yalnızca Sahibe açıktır (izin olarak dağıtılmaz).
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "Owner")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffService _staff;

        public StaffController(IStaffService staff)
        {
            _staff = staff;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;
            return Ok(await _staff.GetPagedAsync(page, pageSize));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateStaffDTO dto)
            => Ok(await _staff.CreateAsync(dto));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateStaffDTO dto)
            => Ok(await _staff.UpdateAsync(id, dto, GetUserId()));

        // Pasifleştirme (soft delete): kullanıcı silinmez, IsActive=false yapılır.
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _staff.DeleteAsync(id);
            return NoContent();
        }

        [HttpPost("{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetStaffPasswordDTO dto)
        {
            await _staff.ResetPasswordAsync(id, dto.TempPassword);
            return NoContent();
        }

        private int GetUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }
}
