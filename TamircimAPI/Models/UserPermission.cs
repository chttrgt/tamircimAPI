namespace TamircimAPI.Models
{
    // Bir çalışana atanmış tek bir izin. Sahip için kullanılmaz (Owner örtük olarak hepsine sahip).
    public class UserPermission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Permission { get; set; } = string.Empty;

        public User User { get; set; } = null!;
    }
}
