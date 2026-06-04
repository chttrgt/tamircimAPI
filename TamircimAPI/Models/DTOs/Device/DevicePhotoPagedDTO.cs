namespace TamircimAPI.Models.DTOs.Device
{
    public class DevicePhotoPagedDTO
    {
        public List<DevicePhotoDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }

    public class BulkDeletePhotosDTO
    {
        public List<int> Ids { get; set; } = new();
    }
}
