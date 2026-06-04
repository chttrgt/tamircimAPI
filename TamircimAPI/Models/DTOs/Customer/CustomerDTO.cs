namespace TamircimAPI.Models.DTOs.Customer
{
    public class CustomerDTO
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string Phone1 { get; set; } = string.Empty;
        public string? Phone2 { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DeviceCount { get; set; }
    }

    public class CreateCustomerDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string Phone1 { get; set; } = string.Empty;
        public string? Phone2 { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateCustomerDTO
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? Address { get; set; }
        public string? Email { get; set; }
        public string Phone1 { get; set; } = string.Empty;
        public string? Phone2 { get; set; }
        public string? Notes { get; set; }
    }

    public class CustomerListDTO
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone1 { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? NationalId { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DeviceCount { get; set; }
        public string? PrimaryDeviceType { get; set; }
    }

    // Sayfalı müşteri listesi (offset pagination). DevicePhotoPagedDTO ile aynı şekil.
    public class CustomerPagedDTO
    {
        public List<CustomerListDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }
}
