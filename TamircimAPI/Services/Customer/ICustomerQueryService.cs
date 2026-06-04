using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Services.Customer
{
    public interface ICustomerQueryService
    {
        Task<CustomerPagedDTO> GetPagedAsync(string? search, int page, int pageSize);
        Task<CustomerDTO?> GetByIdAsync(int id);
    }
}
