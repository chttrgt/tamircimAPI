using TamircimAPI.Models.DTOs.Device;

namespace TamircimAPI.Services.Device
{
    public interface IDeviceCommandService
    {
        Task<DeviceDTO> CreateAsync(CreateDeviceDTO dto);
        Task<DeviceDTO> UpdateAsync(int id, UpdateDeviceDTO dto);
        Task<DeviceDTO> MarkDeliveredAsync(int id, DateTime? deliveredAt = null);
        Task<DeviceDTO> UndoDeliveryAsync(int id);
        Task DeleteAsync(int id);
    }
}
