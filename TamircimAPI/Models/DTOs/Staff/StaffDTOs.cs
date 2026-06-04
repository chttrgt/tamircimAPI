namespace TamircimAPI.Models.DTOs.Staff
{
    public class StaffPagedDTO
    {
        public List<StaffListDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }


    public class StaffListDTO
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Role { get; set; } = string.Empty;       // "Owner" | "Employee"
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }
        public List<string> Permissions { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStaffDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        public string TempPassword { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class UpdateStaffDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public bool IsActive { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class ResetStaffPasswordDTO
    {
        public string TempPassword { get; set; } = string.Empty;
    }
}
