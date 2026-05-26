using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Device
{
    public class DeviceDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CurrentStatus { get; set; }
    }

    public class CreateDeviceDTO
    {
        public int CustomerId { get; set; }
        public DeviceType DeviceType { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateDeviceDTO
    {
        public DeviceType DeviceType { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class DeviceListDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public string? CurrentStatus { get; set; }
    }
}
