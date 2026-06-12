using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TamircimAPI.Data;
using TamircimAPI.Exceptions;
using TamircimAPI.Models;
using TamircimAPI.Models.DTOs.Auth;
using TamircimAPI.Models.Enums;
using TamircimAPI.Services.Captcha;
using TamircimAPI.Services.Email;
using TamircimAPI.Services.Token;

namespace TamircimAPI.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly ICaptchaVerifier _captcha;
        private readonly ILogger<AuthService> _logger;

        // Login zamanlama yan-kanalını kapatır: kullanıcı bulunamadığında da sabit bir
        // BCrypt doğrulaması çalıştırılır → "bu e-posta kayıtlı mı" yanıt süresi farkından
        // sızdırılamaz. Tür ilk yüklenirken bir kez hesaplanır.
        private static readonly string DummyPasswordHash =
            BCrypt.Net.BCrypt.HashPassword("timing-equalizer", BCrypt.Net.BCrypt.GenerateSalt(12));

        public AuthService(
            ApplicationDbContext db,
            ITokenService tokenService,
            IConfiguration configuration,
            IEmailSender emailSender,
            ICaptchaVerifier captcha,
            ILogger<AuthService> logger)
        {
            _db = db;
            _tokenService = tokenService;
            _configuration = configuration;
            _emailSender = emailSender;
            _captcha = captcha;
            _logger = logger;
        }

        private static LoginResponseDTO BuildLoginResponse(User user, string accessToken, string refreshToken) => new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            FullName = user.FullName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Title = user.Title,
            Email = user.Email,
            Role = user.Role.ToString(),
            Permissions = user.Permissions.Select(p => p.Permission).ToList(),
            MustChangePassword = user.MustChangePassword,
            TwoFactorEnabled = user.TwoFactorEnabled,
            // Yalnızca Owner için anlamlı; istemci bunu görünce silme-bekliyor ekranına yönlendirir.
            AccountDeletionScheduledAt = user.Tenant?.DeletionScheduledAt,
        };

        // Açık self-servis kayıt: yeni tenant (dükkân) + sahip kullanıcı oluşturur.
        // Tenant IsActive=false → e-posta doğrulanana kadar giriş yapılamaz. Otomatik
        // giriş YAPILMAZ. E-posta GLOBAL benzersizdir (tenant filtresini bypass eden
        // IgnoreQueryFilters ile kontrol edilir).
        public async Task<RegisterResponseDTO> RegisterAsync(RegisterDTO dto, string ipAddress, string lang)
        {
            // Bot/sahte kayıt koruması: her şeyden önce captcha'yı doğrula (fail-closed).
            if (!await _captcha.VerifyAsync(dto.CaptchaToken, ipAddress))
                throw new BusinessRuleException(
                    "Doğrulama başarısız. Lütfen tekrar deneyin.", "CAPTCHA_FAILED");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-posta zorunludur.");
            if (string.IsNullOrWhiteSpace(dto.ShopName))
                throw new ArgumentException("Dükkân adı zorunludur.");
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
                throw new ArgumentException("Şifre en az 8 karakter olmalıdır.");

            // Tenant filtresini bypass et: e-posta tüm sistemde benzersiz olmalı.
            var emailTaken = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
            if (emailTaken)
                throw new BusinessRuleException("Bu e-posta adresi zaten kayıtlı.", "EMAIL_ALREADY_EXISTS");

            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password, salt);
            var verificationToken = GenerateUrlSafeToken();

            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync();

                var tenant = new Models.Tenant
                {
                    Name = dto.ShopName.Trim(),
                    Branch = dto.Branch.Trim(),
                    IsActive = false,
                };
                _db.Tenants.Add(tenant);
                await _db.SaveChangesAsync(); // tenant.Id üret

                var user = new User
                {
                    TenantId = tenant.Id, // bağlam yok → açıkça set (SetTenantIdFields izin verir)
                    FirstName = dto.FirstName.Trim(),
                    LastName = dto.LastName.Trim(),
                    Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim(),
                    Email = email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    IsActive = true,
                    Role = UserRole.Owner, // ilk kayıt = dükkân sahibi
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync(); // user.Id üret

                _db.EmailVerificationTokens.Add(new EmailVerificationToken
                {
                    UserId = user.Id,
                    Token = _tokenService.HashToken(verificationToken), // DB'de hash; düz metin e-postada
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                });
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
            });

            await SendVerificationAsync(email, $"{dto.FirstName} {dto.LastName}".Trim(), verificationToken, lang);

            return new RegisterResponseDTO
            {
                Email = email,
                Message = "Kayıt alındı. Hesabını etkinleştirmek için e-postandaki doğrulama bağlantısına tıkla.",
            };
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            // EmailVerificationToken'ın tenant filtresi yok; User filtreli olduğundan
            // (bağlam=0) IgnoreQueryFilters ile yükle.
            // Gelen düz metin token'ın hash'ini hesaplayıp DB'deki hash ile eşleştir.
            var tokenHash = _tokenService.HashToken(token);
            var vt = await _db.EmailVerificationTokens
                .IgnoreQueryFilters()
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.Token == tokenHash);

            if (vt == null || !vt.IsValid) return false;

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == vt.User.TenantId);
            if (tenant == null) return false;

            vt.ConsumedAt = DateTime.UtcNow;
            tenant.IsActive = true;
            await _db.SaveChangesAsync();
            return true;
        }

        // Numaralandırma sızıntısı yapmaz: e-posta yoksa/doğrulanmışsa sessizce başarı döner.
        public async Task ResendVerificationAsync(string email, string lang)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var user = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == normalized && u.Role == UserRole.Owner);

            if (user == null || user.Tenant.IsActive) return;

            // E-posta başına cooldown: IP-bazlı rate limit'in ötesinde, aynı adrese çok
            // sık mail gönderimini (spam + SMTP kotası tüketimi) engeller. Son token 2 dk
            // içinde üretildiyse sessizce çık (bilgi sızdırmamak için yine başarı dönülür).
            var lastSentAt = await _db.EmailVerificationTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => (DateTime?)t.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastSentAt.HasValue && DateTime.UtcNow - lastSentAt.Value < TimeSpan.FromMinutes(2))
                return;

            // Eski geçerli token'ları tüket, yeni üret.
            var oldTokens = await _db.EmailVerificationTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id && t.ConsumedAt == null)
                .ToListAsync();
            foreach (var t in oldTokens)
                t.ConsumedAt = DateTime.UtcNow;

            var newToken = GenerateUrlSafeToken();
            _db.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                UserId = user.Id,
                Token = _tokenService.HashToken(newToken), // DB'de hash; düz metin e-postada
                ExpiresAt = DateTime.UtcNow.AddHours(24),
            });
            await _db.SaveChangesAsync();

            await SendVerificationAsync(user.Email, user.FullName, newToken, lang);
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string ipAddress, string lang)
        {
            var email = dto.Email.Trim().ToLowerInvariant();

            // Login tenant bağlamından önce çalışır → global arama için IgnoreQueryFilters.
            var user = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.Permissions)
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            // Zamanlama eşitleme: kullanıcı yoksa da bir BCrypt doğrulaması çalıştır
            // (sabit hash) → "e-posta kayıtlı mı" yanıt süresinden anlaşılamaz.
            if (user == null)
            {
                BCrypt.Net.BCrypt.Verify(dto.Password, DummyPasswordHash);
                throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

            if (!user.Tenant.IsActive)
                throw new BusinessRuleException(
                    "Hesabın henüz doğrulanmadı. Lütfen e-postandaki doğrulama bağlantısına tıkla.",
                    "EMAIL_NOT_VERIFIED");

            // Hesap silme bekliyorsa: grace süresince hesap askıdadır. Personel giriş yapamaz;
            // yalnızca Owner girebilir (silmeyi iptal edebilsin). Owner girişinde token verilir
            // ve yanıt AccountDeletionScheduledAt taşır → istemci silme-bekliyor ekranına yönlendirir.
            if (user.Tenant.DeletionScheduledAt != null && user.Role != UserRole.Owner)
                throw new BusinessRuleException(
                    "Bu hesap silinmek üzere. Yalnızca dükkân sahibi işlem yapabilir.",
                    "ACCOUNT_PENDING_DELETION");

            // İki adımlı doğrulama açıksa token verme: e-postaya kod gönder, challenge dön.
            if (user.TwoFactorEnabled)
            {
                var challengeToken = await CreateChallengeAsync(user, TwoFactorPurpose.Login, lang);
                return new LoginResponseDTO
                {
                    TwoFactorRequired = true,
                    ChallengeToken = challengeToken,
                    Email = MaskEmail(user.Email),
                    TwoFactorEnabled = true,
                };
            }

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            _db.RefreshTokens.Add(new RefreshToken
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                Token = _tokenService.HashToken(refreshToken), // DB'de hash; düz metin istemcide
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });
            await _db.SaveChangesAsync();

            return BuildLoginResponse(user, accessToken, refreshToken);
        }

        public async Task<LoginResponseDTO> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            // Refresh public akış: User tenant-filtreli olduğundan IgnoreQueryFilters şart.
            // Gelen düz metin token'ın hash'i ile aranır (DB'de yalnızca hash saklanır).
            var tokenHash = _tokenService.HashToken(refreshToken);
            var token = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Include(t => t.User)
                    .ThenInclude(u => u.Permissions)
                .Include(t => t.User)
                    .ThenInclude(u => u.Tenant)
                .FirstOrDefaultAsync(t => t.Token == tokenHash);

            if (token == null)
                throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

            // S5 — Yeniden-kullanım (theft) tespiti: zaten iptal edilmiş (rotasyona uğramış)
            // bir token tekrar kullanılıyorsa çalınmış token sinyalidir → kullanıcının tüm
            // aktif token ailesini iptal et (tüm oturumları kapat) ve reddet.
            if (token.IsRevoked)
            {
                var family = await _db.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(t => t.UserId == token.UserId && t.RevokedAt == null)
                    .ToListAsync();
                foreach (var t in family)
                {
                    t.RevokedAt = DateTime.UtcNow;
                    t.RevokedByIp = ipAddress;
                    t.RevokeReason = "Reuse detected — family revoked";
                }
                await _db.SaveChangesAsync();
                throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");
            }

            if (token.IsExpired)
                throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş refresh token.");

            // S4 — Hesap hâlâ aktif mi? Pasifleştirilmiş kullanıcı / doğrulanmamış tenant
            // refresh ile erişimini sürdüremesin (defense-in-depth).
            if (!token.User.IsActive || !token.User.Tenant.IsActive)
                throw new UnauthorizedAccessException("Hesap erişimi devre dışı.");

            // Hesap silme bekliyorsa personel oturumu yenilenmesin (askıya alma) → en geç
            // access token ömrü kadar sürede oturumu kapanır. Owner yenileyebilir (iptal için).
            if (token.User.Tenant.DeletionScheduledAt != null && token.User.Role != UserRole.Owner)
                throw new UnauthorizedAccessException("Hesap erişimi devre dışı.");

            var newRefreshToken = _tokenService.GenerateRefreshToken();
            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.ReplacedByToken = _tokenService.HashToken(newRefreshToken);
            token.RevokeReason = "Replaced by new token";

            _db.RefreshTokens.Add(new RefreshToken
            {
                TenantId = token.User.TenantId,
                UserId = token.UserId,
                Token = _tokenService.HashToken(newRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });
            await _db.SaveChangesAsync();

            var accessToken = _tokenService.GenerateAccessToken(token.User);
            return BuildLoginResponse(token.User, accessToken, newRefreshToken);
        }

        public async Task<UpdateProfileResponseDTO> UpdateProfileAsync(int userId, UpdateProfileDTO dto, string ipAddress)
        {
            // Kimlikli istek → tenant bağlamı var; kullanıcı kendi tenant'ında, normal filtre yeterli.
            var user = await _db.Users
                .Include(u => u.Permissions)
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var originalEmail = user.Email;
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            if (normalizedEmail != originalEmail)
            {
                // E-posta global benzersiz → tüm sistemde kontrol.
                var emailTaken = await _db.Users.IgnoreQueryFilters()
                    .AnyAsync(u => u.Email == normalizedEmail && u.Id != userId);
                if (emailTaken)
                    throw new ArgumentException("Bu e-posta adresi zaten kayıtlı.");
            }

            bool passwordChanged = !string.IsNullOrWhiteSpace(dto.NewPassword);

            if (passwordChanged)
            {
                if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
                    throw new ArgumentException("Mevcut şifre zorunludur.");
                if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                    throw new UnauthorizedAccessException("Mevcut şifre hatalı.");

                var newSalt = BCrypt.Net.BCrypt.GenerateSalt(12);
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, newSalt);
                user.PasswordSalt = newSalt;
                user.MustChangePassword = false;
            }

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            user.Title = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title.Trim();
            user.Email = normalizedEmail;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            string? newAccessToken = null;
            string? newRefreshToken = null;

            if (normalizedEmail != originalEmail || passwordChanged)
            {
                var revokeReason = passwordChanged && normalizedEmail != originalEmail
                    ? "Email and password changed"
                    : passwordChanged ? "Password changed" : "Email changed";

                var oldTokens = await _db.RefreshTokens
                    .IgnoreQueryFilters()
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
                    TenantId = user.TenantId,
                    UserId = user.Id,
                    Token = _tokenService.HashToken(newRefreshTokenValue),
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
                Branch = user.Tenant.Branch,
                NewAccessToken = newAccessToken,
                NewRefreshToken = newRefreshToken,
            };
        }

        public async Task RevokeTokenAsync(string refreshToken, string ipAddress)
        {
            var tokenHash = _tokenService.HashToken(refreshToken);
            var token = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Token == tokenHash);

            if (token == null || !token.IsActive)
                throw new KeyNotFoundException("Token bulunamadı veya zaten iptal edilmiş.");

            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.RevokeReason = "Revoked by user";

            await _db.SaveChangesAsync();
        }

        private async Task SendVerificationAsync(string email, string name, string token, string lang)
        {
            var baseUrl = (Environment.GetEnvironmentVariable("APP_PUBLIC_URL")
                ?? _configuration["App:PublicUrl"]
                ?? "").TrimEnd('/');
            // lang'i linke de taşı → doğrulama linkine tıklanınca açılan HTML sayfası da
            // (tarayıcı dilinden bağımsız) kullanıcının seçtiği dilde gösterilir.
            var link = $"{baseUrl}/api/auth/verify-email?token={Uri.EscapeDataString(token)}&lang={Uri.EscapeDataString(lang)}";
            await _emailSender.SendVerificationEmailAsync(email, name, link, lang);
        }

        private static string GenerateUrlSafeToken()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        // ── Şifremi unuttum / sıfırla ─────────────────────────────────────────────

        public async Task ForgotPasswordAsync(ForgotPasswordDTO dto, string ipAddress, string lang)
        {
            // Bot/spam koruması: captcha'yı her şeyden önce doğrula (fail-closed).
            if (!await _captcha.VerifyAsync(dto.CaptchaToken, ipAddress))
                throw new BusinessRuleException("Doğrulama başarısız. Lütfen tekrar deneyin.", "CAPTCHA_FAILED");

            var email = dto.Email.Trim().ToLowerInvariant();

            // Tenant bağlamı yok → IgnoreQueryFilters. Aktif kullanıcı.
            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            // Numaralandırma sızıntısı yok: e-posta yoksa sessizce başarı dön.
            if (user == null) return;

            // E-posta cooldown: son kod 2 dk içinde üretildiyse sessizce çık (spam + SMTP kotası).
            var lastSentAt = await _db.PasswordResetTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => (DateTime?)t.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastSentAt.HasValue && DateTime.UtcNow - lastSentAt.Value < TimeSpan.FromMinutes(2))
                return;

            // Eski kullanılmamış kodları tüket → aynı anda tek geçerli kod.
            var oldTokens = await _db.PasswordResetTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id && t.ConsumedAt == null)
                .ToListAsync();
            foreach (var t in oldTokens) t.ConsumedAt = DateTime.UtcNow;

            var code = GenerateNumericCode(6);
            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                CodeHash = _tokenService.HashToken(code), // DB'de hash; düz kod yalnızca e-postada
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            });
            await _db.SaveChangesAsync();

            // E-posta hatası akışı bozmasın (numaralandırma tutarlılığı) — logla, sessiz geç.
            try
            {
                await _emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, code, lang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şifre sıfırlama e-postası gönderilemedi: {Email}", user.Email);
            }
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDTO dto, string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                throw new ArgumentException("Şifre en az 8 karakter olmalıdır.");

            var code = (dto.Code ?? string.Empty).Trim();
            var email = dto.Email.Trim().ToLowerInvariant();

            // Hata mesajları kasıtlı genel: "kod yanlış mı, süresi mi doldu, e-posta var mı"
            // ayrımı sızdırılmaz.
            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
            if (user == null)
                throw new BusinessRuleException("Kod geçersiz veya süresi dolmuş.", "RESET_INVALID");

            var token = await _db.PasswordResetTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id && t.ConsumedAt == null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (token == null || token.IsExpired)
                throw new BusinessRuleException("Kod geçersiz veya süresi dolmuş.", "RESET_INVALID");

            // Brute-force koruması: çok yanlış deneme → token'ı geçersiz kıl.
            if (token.AttemptCount >= 5)
            {
                token.ConsumedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                throw new BusinessRuleException("Çok fazla yanlış deneme. Lütfen yeni kod iste.", "RESET_TOO_MANY");
            }

            if (token.CodeHash != _tokenService.HashToken(code))
            {
                token.AttemptCount++;
                await _db.SaveChangesAsync();
                throw new BusinessRuleException("Kod geçersiz veya süresi dolmuş.", "RESET_INVALID");
            }

            // Kod doğru → şifreyi güncelle + token'ı tüket.
            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword, salt);
            user.PasswordSalt = salt;
            user.MustChangePassword = false;
            user.UpdatedAt = DateTime.UtcNow;
            token.ConsumedAt = DateTime.UtcNow;

            // Tüm aktif oturumları iptal et → eski şifreyle açılmış cihazlar düşer (güvenlik).
            var activeTokens = await _db.RefreshTokens
                .IgnoreQueryFilters()
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ToListAsync();
            foreach (var rt in activeTokens)
            {
                rt.RevokedAt = DateTime.UtcNow;
                rt.RevokedByIp = ipAddress;
                rt.RevokeReason = "Password reset";
            }

            await _db.SaveChangesAsync();
        }

        // ── İki adımlı doğrulama (e-posta OTP) ────────────────────────────────────

        public async Task<LoginResponseDTO> VerifyTwoFactorLoginAsync(TwoFactorVerifyDTO dto, string ipAddress)
        {
            var challenge = await VerifyChallengeAsync(dto.ChallengeToken, dto.Code, TwoFactorPurpose.Login, null);

            // Token üretimi için kullanıcıyı izinleri + tenant'ıyla yükle (pre-auth → IgnoreQueryFilters).
            var user = await _db.Users
                .IgnoreQueryFilters()
                .Include(u => u.Permissions)
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == challenge.UserId && u.IsActive);
            if (user == null || !user.Tenant.IsActive)
                throw new UnauthorizedAccessException("E-posta veya şifre hatalı.");

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var expireDays = int.Parse(_configuration["Jwt:RefreshTokenExpireDays"] ?? "7");
            _db.RefreshTokens.Add(new RefreshToken
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                Token = _tokenService.HashToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(expireDays),
                CreatedByIp = ipAddress
            });
            await _db.SaveChangesAsync();

            return BuildLoginResponse(user, accessToken, refreshToken);
        }

        public async Task ResendTwoFactorAsync(string challengeToken, string lang)
        {
            var hash = _tokenService.HashToken(challengeToken ?? string.Empty);
            var challenge = await _db.TwoFactorChallenges
                .IgnoreQueryFilters()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.ChallengeHash == hash && c.ConsumedAt == null);

            if (challenge == null || challenge.IsExpired)
                throw new BusinessRuleException("Doğrulama geçersiz veya süresi dolmuş.", "TFA_INVALID");

            // Cooldown: çok sık yeni kod gönderimini engelle (e-posta spam + SMTP kotası).
            if (DateTime.UtcNow - challenge.CreatedAt < TimeSpan.FromSeconds(60))
                throw new BusinessRuleException("Yeni kod istemek için biraz bekle.", "TFA_COOLDOWN");

            var code = GenerateNumericCode(6);
            challenge.CodeHash = _tokenService.HashToken(code);
            challenge.AttemptCount = 0;
            challenge.CreatedAt = DateTime.UtcNow;
            challenge.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
            await _db.SaveChangesAsync();

            try { await _emailSender.SendTwoFactorCodeEmailAsync(challenge.User.Email, challenge.User.FullName, code, lang); }
            catch (Exception ex) { _logger.LogError(ex, "2FA kodu yeniden gönderilemedi: {Email}", challenge.User.Email); }
        }

        public async Task<TwoFactorChallengeResponseDTO> RequestEnableTwoFactorAsync(int userId, string lang)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new UnauthorizedAccessException();
            if (user.Role != UserRole.Owner)
                throw new BusinessRuleException("Bu işlem yalnızca sahip içindir.", "FORBIDDEN");
            if (user.TwoFactorEnabled)
                throw new BusinessRuleException("İki adımlı doğrulama zaten açık.", "TFA_ALREADY");

            var challengeToken = await CreateChallengeAsync(user, TwoFactorPurpose.Enable, lang, surfaceEmailError: true);
            return new TwoFactorChallengeResponseDTO
            {
                ChallengeToken = challengeToken,
                MaskedEmail = MaskEmail(user.Email),
            };
        }

        public async Task ConfirmEnableTwoFactorAsync(int userId, TwoFactorVerifyDTO dto)
        {
            var challenge = await VerifyChallengeAsync(dto.ChallengeToken, dto.Code, TwoFactorPurpose.Enable, userId);

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Id == challenge.UserId)
                ?? throw new UnauthorizedAccessException();
            if (user.Role != UserRole.Owner)
                throw new BusinessRuleException("Bu işlem yalnızca sahip içindir.", "FORBIDDEN");

            user.TwoFactorEnabled = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task DisableTwoFactorAsync(int userId, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new UnauthorizedAccessException();

            // Kapatma şifre teyidi ister → oturum ele geçirilse bile (şifresiz) kapatılamaz.
            if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, user.PasswordHash))
                throw new BusinessRuleException("Şifre hatalı.", "INVALID_PASSWORD");

            user.TwoFactorEnabled = false;
            user.UpdatedAt = DateTime.UtcNow;

            // Bekleyen challenge'ları temizle.
            var pending = await _db.TwoFactorChallenges
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId && c.ConsumedAt == null)
                .ToListAsync();
            foreach (var c in pending) c.ConsumedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // Yeni challenge üretir: aynı amaçtaki eski kodları tüketir, kodu e-postayla yollar,
        // istemciye opak challenge token döner (DB'de yalnızca hash'leri tutulur).
        private async Task<string> CreateChallengeAsync(User user, TwoFactorPurpose purpose, string lang, bool surfaceEmailError = false)
        {
            var old = await _db.TwoFactorChallenges
                .IgnoreQueryFilters()
                .Where(c => c.UserId == user.Id && c.Purpose == purpose && c.ConsumedAt == null)
                .ToListAsync();
            foreach (var c in old) c.ConsumedAt = DateTime.UtcNow;

            var challengeToken = GenerateUrlSafeToken();
            var code = GenerateNumericCode(6);
            _db.TwoFactorChallenges.Add(new TwoFactorChallenge
            {
                UserId = user.Id,
                ChallengeHash = _tokenService.HashToken(challengeToken),
                CodeHash = _tokenService.HashToken(code),
                Purpose = purpose,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            });
            await _db.SaveChangesAsync();

            try
            {
                await _emailSender.SendTwoFactorCodeEmailAsync(user.Email, user.FullName, code, lang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "2FA kodu gönderilemedi: {Email}", user.Email);
                // Public akışlar (login) sessiz kalır; kimlik doğrulamalı "açma" akışında
                // (numaralandırma riski yok) hata kullanıcıya gösterilir → yanıltıcı "gönderildi" olmaz.
                if (surfaceEmailError)
                    throw new BusinessRuleException("Kod e-postası gönderilemedi. Lütfen tekrar deneyin.", "EMAIL_SEND_FAILED");
            }

            return challengeToken;
        }

        // Challenge + kodu doğrular; başarılıysa challenge'ı tüketir ve döner. Hata mesajları
        // genel (kod yanlış mı / süresi mi doldu ayrımı sızmaz). Brute-force: AttemptCount.
        private async Task<TwoFactorChallenge> VerifyChallengeAsync(
            string challengeToken, string code, TwoFactorPurpose purpose, int? expectedUserId)
        {
            var hash = _tokenService.HashToken(challengeToken ?? string.Empty);
            var challenge = await _db.TwoFactorChallenges
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.ChallengeHash == hash && c.Purpose == purpose && c.ConsumedAt == null);

            if (challenge == null || challenge.IsExpired
                || (expectedUserId.HasValue && challenge.UserId != expectedUserId.Value))
                throw new BusinessRuleException("Kod geçersiz veya süresi dolmuş.", "TFA_INVALID");

            if (challenge.AttemptCount >= 5)
            {
                challenge.ConsumedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                throw new BusinessRuleException("Çok fazla yanlış deneme. Lütfen yeni kod iste.", "TFA_TOO_MANY");
            }

            if (challenge.CodeHash != _tokenService.HashToken((code ?? string.Empty).Trim()))
            {
                challenge.AttemptCount++;
                await _db.SaveChangesAsync();
                throw new BusinessRuleException("Kod geçersiz veya süresi dolmuş.", "TFA_INVALID");
            }

            challenge.ConsumedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return challenge;
        }

        // E-postayı maskele: "ahmet@x.com" → "ah***@x.com" (kodun nereye gittiğini gösterir).
        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            if (at <= 0) return "***";
            var local = email[..at];
            var domain = email[at..];
            var visible = local.Length <= 2 ? local[..1] : local[..2];
            return $"{visible}{new string('*', Math.Max(1, local.Length - visible.Length))}{domain}";
        }

        // Kriptografik rastgele 6 haneli kod (000000–999999; baştaki sıfırlar korunur).
        private static string GenerateNumericCode(int digits)
        {
            var max = (int)Math.Pow(10, digits);
            var n = RandomNumberGenerator.GetInt32(max);
            return n.ToString(new string('0', digits));
        }
    }
}
