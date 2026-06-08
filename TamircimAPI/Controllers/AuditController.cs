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

        [HttpGet("counts")]
        public async Task<IActionResult> GetCounts([FromQuery] int? userId)
        {
            var query = _db.AuditLogs.AsQueryable();
            if (userId.HasValue)
                query = query.Where(a => a.UserId == userId.Value);

            var counts = await query
                .GroupBy(a => a.Action)
                .Select(g => new { Action = g.Key, Count = g.Count() })
                .ToListAsync();

            var total = counts.Sum(c => c.Count);
            var result = new Dictionary<string, int>
            {
                ["total"]  = total,
                ["Create"] = counts.FirstOrDefault(c => c.Action == "Create")?.Count ?? 0,
                ["Update"] = counts.FirstOrDefault(c => c.Action == "Update")?.Count ?? 0,
                ["Delete"] = counts.FirstOrDefault(c => c.Action == "Delete")?.Count ?? 0,
            };

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            [FromQuery] int? userId,
            [FromQuery] string? entityType,
            [FromQuery] string? action,
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

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

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
