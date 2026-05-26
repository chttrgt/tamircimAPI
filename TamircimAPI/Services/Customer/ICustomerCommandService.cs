using TamircimAPI.Models.DTOs.Customer;

namespace TamircimAPI.Services.Customer
{
    public interface ICustomerCommandService
    {
        Task<CustomerDTO> CreateAsync(CreateCustomerDTO dto);
        Task<CustomerDTO> UpdateAsync(int id, UpdateCustomerDTO dto);
        Task DeleteAsync(int id);
    }
}
