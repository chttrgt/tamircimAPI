using TamircimAPI.Models.DTOs.Device;

namespace TamircimAPI.Services.Device
{
    public interface IDeviceQueryService
    {
        Task<IEnumerable<DeviceListDTO>> GetAllAsync(int? customerId = null, string? search = null);
        Task<DeviceDTO?> GetByIdAsync(int id);
    }
}
