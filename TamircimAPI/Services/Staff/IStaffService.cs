using TamircimAPI.Models.DTOs.Staff;

namespace TamircimAPI.Services.Staff
{
    public interface IStaffService
    {
        Task<StaffPagedDTO> GetPagedAsync(int page, int pageSize);
        Task<StaffListDTO> CreateAsync(CreateStaffDTO dto);
        Task<StaffListDTO> UpdateAsync(int id, UpdateStaffDTO dto, int actingUserId);
        Task DeleteAsync(int id);
        Task ResetPasswordAsync(int id, string tempPassword);
    }
}
