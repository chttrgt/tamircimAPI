using TamircimAPI.Models.DTOs.Device;

namespace TamircimAPI.Services.Device
{
    public interface IDeviceCommandService
    {
        Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto);
        Task<DeviceDTO> UpdateAsync(int id, UpdateDeviceDTO dto);
        Task DeleteAsync(int id);
    }
}
