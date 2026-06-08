namespace TamircimAPI.Models.DTOs.Device
{
    public class DevicePagedDTO
    {
        public List<DeviceListDTO> Items { get; set; } = new();
        public bool HasMore { get; set; }
    }
}
