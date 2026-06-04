namespace TamircimAPI.Models.Enums
{
    // Sahip: tüm izinlere örtük sahip + personel yönetir.
    // Çalışan: yalnızca kendisine atanmış izinlere (UserPermission) sahip.
    public enum UserRole
    {
        Owner = 0,
        Employee = 1
    }
}
