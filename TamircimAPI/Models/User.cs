using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Branch { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Yetkilendirme: Sahip örtük olarak tüm izinlere sahiptir; Çalışan yalnızca
        // Permissions koleksiyonundaki izinlere sahiptir.
        public UserRole Role { get; set; } = UserRole.Employee;

        // Sahip tarafından geçici şifreyle oluşturulan çalışan, ilk girişte şifresini
        // değiştirmeden devam edemez.
        public bool MustChangePassword { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string FullName => $"{FirstName} {LastName}";

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
    }
}
