using TamircimAPI.Models.DTOs.Device;
using TamircimAPI.Models.DTOs.Repair;

namespace TamircimAPI.Services.Device
{
    public interface IDeviceQueryService
    {
        // filter: null/"" → tümü, "active" → açık + Beklemede, "overdue" → açık + 7 günü geçmiş (durum fark etmez)
        Task<IEnumerable<DeviceListDTO>> GetAllAsync(int? customerId = null, string? search = null, string? filter = null);
        Task<DeviceDTO?> GetByIdAsync(int id);

        // Cihaz koduna (DeviceCode) göre birebir cihaz bulur — barkod okutma için.
        Task<DeviceDTO?> GetByCodeAsync(string code);

        // Bir müşterinin tüm servis kayıtlarını (gelişlerini) kronolojik döndürür.
        Task<IEnumerable<CustomerVisitDTO>> GetCustomerHistoryAsync(int customerId);

        // Seri numarası başka bir cihazda kayıtlı mı kontrol eder.
        Task<SerialCheckResultDTO> CheckSerialAsync(string serialNumber, int? excludeDeviceId = null);
    }
}
