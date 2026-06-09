using TamircimAPI.Models.Enums;
using TamircimAPI.Models.Interfaces;

namespace TamircimAPI.Models
{
    public class User : ITenantOwned, ISoftDeletable
    {
        public int Id { get; set; }

        // Kullanıcının ait olduğu teknik servis (tenant). Sahip ve personeller aynı
        // TenantId'yi paylaşır. Insert'te sunucu tarafında set edilir.
        public int TenantId { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Title { get; set; }
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

        // ISoftDeletable — personel "silme" = soft delete. Kullanıcı fiziksel silinmez
        // (oluşturduğu iş emri/audit kayıtları korunur) ama listeden ve sorgulardan
        // global query filter ile elenir → müşteri silme gibi görünür.
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public Tenant Tenant { get; set; } = null!;
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
    }
}
