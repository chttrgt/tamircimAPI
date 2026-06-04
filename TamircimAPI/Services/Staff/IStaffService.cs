using TamircimAPI.Models.DTOs.Staff;

namespace TamircimAPI.Services.Staff
{
    public interface IStaffService
    {
        Task<List<StaffListDTO>> GetAllAsync();
        Task<StaffListDTO> CreateAsync(CreateStaffDTO dto);
        Task<StaffListDTO> UpdateAsync(int id, UpdateStaffDTO dto, int actingUserId);
        Task ResetPasswordAsync(int id, string tempPassword);
    }
}
