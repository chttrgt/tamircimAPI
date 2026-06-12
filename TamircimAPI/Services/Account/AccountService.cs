using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Exceptions;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Account
{
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _db;
        private readonly int _graceDays;

        public AccountService(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db;
            _graceDays = int.TryParse(configuration["Account:DeletionGraceDays"], out var d) ? d : 14;
        }

        public async Task<DateTime> RequestDeletionAsync(int userId, string password)
        {
            // İstek tenant bağlamında çalışır → Users query filter'ı zaten owner'ın tenant'ına sınırlar.
            var user = await _db.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

            // Controller [Authorize(Roles="Owner")] ile korur; defense-in-depth olarak burada da.
            if (user.Role != UserRole.Owner)
                throw new BusinessRuleException(
                    "Yalnızca dükkân sahibi hesabı silebilir.", "FORBIDDEN");

            // Kimlik teyidi — oturum ele geçirilse bile şifre olmadan silinemez.
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Şifre hatalı.");

            // Zaten silme bekliyorsa idempotent: mevcut tarihi döndür (yeniden başlatma yok).
            if (user.Tenant.DeletionScheduledAt is { } existing)
                return existing;

            var scheduledAt = DateTime.UtcNow.AddDays(_graceDays);
            user.Tenant.DeletionScheduledAt = scheduledAt;

            // Hesabı askıya al: personelin aktif oturumlarını iptal et (refresh artık çalışmaz →
            // en geç access token ömrü kadar sürede çıkış olur). Owner oturumu korunur (iptal için).
            var staffIds = await _db.Users
                .Where(u => u.Role != UserRole.Owner)
                .Select(u => u.Id)
                .ToListAsync();

            if (staffIds.Count > 0)
            {
                var staffTokens = await _db.RefreshTokens
                    .Where(t => t.TenantId == user.TenantId
                        && t.RevokedAt == null
                        && staffIds.Contains(t.UserId))
                    .ToListAsync();
                foreach (var t in staffTokens)
                {
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokeReason = "Account deletion requested — staff suspended";
                }
            }

            await _db.SaveChangesAsync();
            return scheduledAt;
        }

        public async Task CancelDeletionAsync(int userId)
        {
            var user = await _db.Users
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");

            if (user.Role != UserRole.Owner)
                throw new BusinessRuleException(
                    "Yalnızca dükkân sahibi işlem yapabilir.", "FORBIDDEN");

            user.Tenant.DeletionScheduledAt = null;
            await _db.SaveChangesAsync();
        }
    }
}
