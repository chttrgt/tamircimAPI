using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;

namespace TamircimAPI.Controllers
{
    [ApiController]
    [Route("api/audit")]
    [Authorize(Roles = "Owner")]
    public class AuditController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AuditController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Tenant içindeki tüm kullanıcıları döner (kullanıcı seçici için).
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _db.Users
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new
                {
                    u.Id,
                    FullName = u.FirstName + " " + u.LastName,
                    Role = u.Role.ToString(),
                    u.IsActive,
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int? userId,
            [FromQuery] string? entityType,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 30;

            var query = _db.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);

            if (!string.IsNullOrWhiteSpace(entityType))
                query = query.Where(a => a.EntityType == entityType);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.EntityType,
                    a.EntityId,
                    a.Action,
                    a.Timestamp,
                    a.ChangedFields,
                    UserName = a.User != null ? a.User.FirstName + " " + a.User.LastName : null,
                    a.UserId,
                })
                .ToListAsync();

            return Ok(new { items, total, hasMore = page * pageSize < total });
        }
    }
}
