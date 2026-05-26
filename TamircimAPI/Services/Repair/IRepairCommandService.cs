using TamircimAPI.Models.DTOs.Repair;

namespace TamircimAPI.Services.Repair
{
    public interface IRepairCommandService
    {
        Task<RepairRecordDTO> CreateAsync(CreateRepairRecordDTO dto);
        Task<RepairRecordDTO> UpdateAsync(int id, UpdateRepairRecordDTO dto);
        Task DeleteAsync(int id);
    }
}
