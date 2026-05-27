namespace TamircimAPI.Models.DTOs.Dashboard
{
    public class DashboardStatsDTO
    {
        public int TotalCustomers { get; set; }
        public int TotalDevices { get; set; }
        public int TotalWaiting { get; set; }
        public int TotalOverdue { get; set; }
    }

    public class DashboardDeviceDTO
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int DeviceId { get; set; }
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        // Teslimat tarihi geçmiş
        public DateTime? DeliveryDate { get; set; }
        public int? OverdueDays { get; set; }
        // Yedek parça bekleyen
        public DateTime? WaitingSince { get; set; }
        public int? WaitingDays { get; set; }
    }

    public class DashboardResponseDTO
    {
        public DashboardStatsDTO Stats { get; set; } = new();
        public List<DashboardDeviceDTO> RecentCustomers { get; set; } = new();
        public List<DashboardDeviceDTO> OverdueDevices { get; set; } = new();
        public List<DashboardDeviceDTO> WaitingForParts { get; set; } = new();
    }
}
