using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Device
{
    // Cihaz (fiziksel varlık) detayı + servis özeti.
    public class DeviceDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? ExtraFields { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }

        // Servis özeti
        public int RepairCount { get; set; }
        public DateTime? LastReceivedAt { get; set; }
        public string? CurrentStatus { get; set; }
        public bool HasOpenTicket { get; set; }
        public int PhotoCount { get; set; }
    }

    // Yeni cihaz + ilk servis kaydını birlikte oluşturur.
    public class CreateDeviceDTO
    {
        public int CustomerId { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? ExtraFields { get; set; }
        public string? Notes { get; set; }

        // İlk servis kaydı (geliş) bilgileri
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string? InitialWaitingReason { get; set; }
        public string? InitialRepairNotes { get; set; }
    }

    // Cihazın yalnızca kalıcı (varlık) bilgilerini günceller. Arıza/teslim servis kaydındadır.
    public class UpdateDeviceDTO
    {
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? ExtraFields { get; set; }
        public string? Notes { get; set; }
    }

    public class DeviceListDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerFullName { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;

        // Servis özeti
        public int RepairCount { get; set; }
        public DateTime? LastReceivedAt { get; set; }
        public string? CurrentStatus { get; set; }
        public bool HasOpenTicket { get; set; }
    }

    // Seri no çakışma kontrolü sonucu.
    public class SerialCheckResultDTO
    {
        public bool Exists { get; set; }
        public int? DeviceId { get; set; }
        public string? DeviceCode { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerFullName { get; set; }
        public bool SameCustomer { get; set; }
    }
}
