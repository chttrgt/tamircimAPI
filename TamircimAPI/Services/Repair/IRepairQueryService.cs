using TamircimAPI.Models.DTOs.Repair;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Services.Repair
{
    public interface IRepairQueryService
    {
        Task<IEnumerable<RepairRecordListDTO>> GetAllAsync(int? deviceId = null, RepairStatus? status = null);
        Task<RepairRecordDTO?> GetByIdAsync(int id);
    }
}
