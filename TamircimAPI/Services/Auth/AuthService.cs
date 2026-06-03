using Microsoft.EntityFrameworkCore;
using TamircimAPI.Data;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Auth;
using TamircimAPI.Services.Token;

namespace TamircimAPI.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext db, ITokenService tokenService, IConfiguration configuration)
        {
            _db = db;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        public async Task<LoginResponseDTO> RegisterAsync(RegisterDTO dto, string ipAddress)
        {
            var exists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
            if (exists)
                throw new ArgumentException("Bu e-posta adresi zaten kayıtlı.");

            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, salt);

            var user = new User
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
                Branch = dto.Branch.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });

            await _db.SaveChangesAsync();

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                Email = user.Email
            };
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive);

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });

            await _db.SaveChangesAsync();

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                Email = user.Email
            };
        }

        public async Task<LoginResponseDTO> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            var token = await _db.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (token == null || !token.IsActive)
                throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

            var newRefreshToken = _tokenService.GenerateRefreshToken();
            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReplacedByToken = newRefreshToken;
            token.RevokeReason = "Replaced by new token";

            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = token.UserId,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });

            await _db.SaveChangesAsync();

            var accessToken = _tokenService.GenerateAccessToken(token.User);

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                UserId = token.User.Id,
                FullName = token.User.FullName,
                FirstName = token.User.FirstName,
                LastName = token.User.LastName,
                Title = token.User.Title,
                Email = token.User.Email
            };
        }

        public async Task<UpdateProfileResponseDTO> UpdateProfileAsync(int userId, UpdateProfileDTO dto, string ipAddress)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var originalEmail = user.Email;
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (normalizedEmail != originalEmail)
            {
                var emailTaken = await _db.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId);
                if (emailTaken)
                    throw new ArgumentException("Bu e-posta adresi zaten kayıtlı.");
            }

            bool passwordChanged = !string.IsNullOrWhiteSpace(dto.NewPassword);

            // Şifre değişimi isteniyorsa mevcut şifreyi doğrula
            if (passwordChanged)
            {
                if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                    throw new ArgumentException("Mevcut şifre zorunludur.");
                if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                    throw new UnauthorizedAccessException("Mevcut şifre hatalı.");

                var newSalt = BCrypt.Net.BCrypt.GenerateSalt(12);
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, newSalt);
                user.PasswordSalt = newSalt;
            }

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
            user.Email = normalizedEmail;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            string? newAccessToken = null;
            string? newRefreshToken = null;

            // E-posta veya şifre değişti → tüm oturumları kapat, mevcut cihaza yeni token ver.
            // E-posta: JWT claim'i güncellenmesi gerekir.
            // Şifre: diğer cihazlardaki oturumlar geçersizleşmelidir.
            if (normalizedEmail != originalEmail || passwordChanged)
            {
                var revokeReason = passwordChanged && normalizedEmail != originalEmail
                    ? "Email and password changed"
                    : passwordChanged ? "Password changed" : "Email changed";

                var oldTokens = await _db.RefreshTokens
                    .Where(t => t.UserId == userId && t.RevokedAt == null)
                    .ToListAsync();
                foreach (var rt in oldTokens)
                {
                    rt.RevokedAt = DateTime.UtcNow;
                    rt.RevokedByIp = ipAddress;
                    rt.RevokeReason = revokeReason;
                }

                newAccessToken = _tokenService.GenerateAccessToken(user);
                var newRefreshTokenValue = _tokenService.GenerateRefreshToken();
                var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");
                _db.RefreshTokens.Add(new RefreshToken
                {
                    UserId = user.Id,
                    Token = newRefreshTokenValue,
                    ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                    CreatedByIp = ipAddress,
                });
                await _db.SaveChangesAsync();
                newRefreshToken = newRefreshTokenValue;
            }

            return new UpdateProfileResponseDTO
            {
                UserId = user.Id,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Title = user.Title,
                Email = user.Email,
                Branch = user.Branch,
                NewAccessToken = newAccessToken,
                NewRefreshToken = newRefreshToken,
            };
        }

        public async Task RevokeTokenAsync(string refreshToken, string ipAddress)
        {
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);

            if (token == null || !token.IsActive)
                throw new KeyNotFoundException("Token bulunamadı veya zaten iptal edilmiş.");

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.RevokeReason = "Revoked by user";

            await _db.SaveChangesAsync();
        }
    }
}
