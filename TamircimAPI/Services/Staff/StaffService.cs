using Microsoft.EntityFrameworkCore;
using TamircimAPI.Authorization;
using TamircimAPI.Data;
using TamircimAPI.Exceptions;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Staff;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Staff
{
    public class StaffService : IStaffService
    {
        private readonly ApplicationDbContext _db;

        public StaffService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<StaffPagedDTO> GetPagedAsync(int page, int pageSize)
        {
            var query = _db.Users
                .Include(u => u.Permissions)
                .OrderBy(u => u.Role)        // Sahip (0) önce
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.Id);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new StaffPagedDTO
            {
                Items = items.Select(MapToDto).ToList(),
                Total = total,
                HasMore = page * pageSize < total,
            };
        }

        public async Task<StaffListDTO> CreateAsync(CreateStaffDTO dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                throw new ArgumentException("Ad ve soyad zorunludur.");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-posta zorunludur.");

            ValidatePassword(dto.TempPassword);
            var perms = ValidatePermissions(dto.Permissions);

            if (await _db.Users.AnyAsync(u => u.Email == email))
                throw new BusinessRuleException("Bu e-posta adresi zaten kayıtlı.", "EMAIL_ALREADY_EXISTS");

            // Çalışan, dükkânın (sahibin) branch'ini devralır → cihaz tipi türetimi tutarlı kalır.
            var branch = await _db.Users
                .Where(u => u.Role == UserRole.Owner)
                .Select(u => u.Branch)
                .FirstOrDefaultAsync() ?? string.Empty;

            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            var user = new User
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
                Email = email,
                Branch = branch,
                Role = UserRole.Employee,
                IsActive = true,
                MustChangePassword = true,
                PasswordSalt = salt,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TempPassword, salt),
            };

            foreach (var p in perms)
                user.Permissions.Add(new UserPermission { Permission = p });

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return MapToDto(user);
        }

        public async Task<StaffListDTO> UpdateAsync(int id, UpdateStaffDTO dto, int actingUserId)
        {
            var user = await _db.Users
                .Include(u => u.Permissions)
                .FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

            if (user.Role == UserRole.Owner)
                throw new BusinessRuleException("Sahip hesabı buradan düzenlenemez.", "OWNER_NOT_EDITABLE");

            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                throw new ArgumentException("Ad ve soyad zorunludur.");

            var target = ValidatePermissions(dto.Permissions).ToHashSet();

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
            user.IsActive = dto.IsActive;

            // İzin farkını uygula (sil/ekle) — birebir replace yerine diff, böylece
            // (UserId, Permission) unique kısıtı çakışmaz.
            var current = user.Permissions.Select(p => p.Permission).ToHashSet();
            var toRemove = user.Permissions.Where(p => !target.Contains(p.Permission)).ToList();
            _db.UserPermissions.RemoveRange(toRemove);
            foreach (var p in target.Where(p => !current.Contains(p)))
                user.Permissions.Add(new UserPermission { UserId = user.Id, Permission = p });

            await _db.SaveChangesAsync();

            return MapToDto(user);
        }

        public async Task ResetPasswordAsync(int id, string tempPassword)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new KeyNotFoundException($"Kullanıcı bulunamadı: {id}");

            if (user.Role == UserRole.Owner)
                throw new BusinessRuleException("Sahip şifresi buradan sıfırlanamaz.", "OWNER_NOT_EDITABLE");

            ValidatePassword(tempPassword);

            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword, salt);
            user.PasswordSalt = salt;
            user.MustChangePassword = true;

            // Çalışanın aktif oturumlarını kapat → yalnızca yeni şifreyle girebilir.
            var activeTokens = await _db.RefreshTokens
                .Where(t => t.UserId == id && t.RevokedAt == null)
                .ToListAsync();
            foreach (var rt in activeTokens)
            {
                rt.RevokedAt = DateTime.UtcNow;
                rt.RevokeReason = "Password reset by owner";
            }

            await _db.SaveChangesAsync();
        }

        private static void ValidatePassword(string pw)
        {
            if (string.IsNullOrWhiteSpace(pw) || pw.Length < 8)
                throw new ArgumentException("Geçici şifre en az 8 karakter olmalıdır.");
        }

        // Gelen izinleri doğrular (uydurma/geçersiz izin kaydedilemez) ve tekilleştirir.
        private static List<string> ValidatePermissions(IEnumerable<string> permissions)
        {
            var distinct = permissions.Distinct().ToList();
            foreach (var p in distinct)
                if (!Permissions.IsValid(p))
                    throw new ArgumentException($"Geçersiz izin: {p}");
            return distinct;
        }

        private static StaffListDTO MapToDto(User u) => new()
        {
            Id = u.Id,
            FullName = u.FullName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Title = u.Title,
            Role = u.Role.ToString(),
            IsActive = u.IsActive,
            MustChangePassword = u.MustChangePassword,
            Permissions = u.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList(),
            CreatedAt = u.CreatedAt,
        };
    }
}
