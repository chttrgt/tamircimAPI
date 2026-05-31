using TamircimAPI.Models.DTOs.Repair;

namespace TamircimAPI.Services.Repair
{
    public interface IRepairCommandService
    {
        Task<RepairRecordDTO> CreateAsync(CreateRepairRecordDTO dto);
        Task<RepairRecordDTO> UpdateAsync(int id, UpdateRepairRecordDTO dto);
        Task<RepairRecordDTO> MarkDeliveredAsync(int id, DateTime? deliveredAt = null);
        Task<RepairRecordDTO> UndoDeliveryAsync(int id);
        Task DeleteAsync(int id);
    }
}
