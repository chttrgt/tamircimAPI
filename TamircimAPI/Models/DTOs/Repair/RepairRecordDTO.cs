using TamircimAPI.Models.Enums;

namespace TamircimAPI.Models.DTOs.Repair
{
    public class RepairRecordDTO
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
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

    public class CreateRepairRecordDTO
    {
        public int DeviceId { get; set; }
        public RepairStatus Status { get; set; } = RepairStatus.Waiting;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateRepairRecordDTO
    {
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
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public RepairStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string? WorkDone { get; set; }
        public string? NotRepairedReason { get; set; }
        public string? WaitingReason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
