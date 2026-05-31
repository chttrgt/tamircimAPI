using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Repair
{
    public class RepairRecordDTO
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Var olan bir cihaza yeni servis kaydı (geliş) açar.
    public class CreateRepairRecordDTO
    {
        public int DeviceId { get; set; }
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.Waiting;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class MarkDeliveredDTO
    {
        public DateTime? DeliveredAt { get; set; }
    }

    public class UpdateRepairRecordDTO
    {
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public RepairStatus Status { get; set; }
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class RepairRecordListDTO
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // Müşteri geçmişi için: bir gelişin cihaz bilgisiyle birlikte özeti.
    public class CustomerVisitDTO
    {
        public int RepairRecordId { get; set; }
        public int DeviceId { get; set; }
        public string TicketNo { get; set; } = string.Empty;
        public string DeviceCode { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public DeviceType DeviceType { get; set; }
        public string DeviceTypeLabel { get; set; } = string.Empty;
        public string FaultDescription { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public bool IsDelivered { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        // Bu cihazın kaçıncı gelişi ve toplam geliş sayısı
        public int VisitNo { get; set; }
        public int TotalVisits { get; set; }
    }
}
