using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Services.Customer
{
    public interface ICustomerQueryService
    {
        Task<IEnumerable<CustomerListDTO>> GetAllAsync(string? search = null);
        Task<CustomerDTO?> GetByIdAsync(int id);
    }
}
